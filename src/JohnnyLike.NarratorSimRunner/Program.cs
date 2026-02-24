using System.Collections.Concurrent;
using System.Diagnostics;
using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.Domain.Office;
using JohnnyLike.Engine;
using JohnnyLike.Llm;
using JohnnyLike.Narration;
using JohnnyLike.SimRunner;
using JohnnyLike.Tts;

// ── CLI parsing ────────────────────────────────────────────────────────────────
var domain = "island";
var seed = 42;
var duration = 120.0;
var realtimeFactor = 1.0;
var model = "llama3";
var piperExe = "piper.exe";
var voicePath = "en_US-lessac-medium.onnx";
var lowWater = 5.0;
var highWater = 15.0;
var slowdownFactor = 0.7;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--domain": domain = args[++i]; break;
        case "--seed": seed = int.Parse(args[++i]); break;
        case "--duration": duration = double.Parse(args[++i]); break;
        case "--realtime-factor": realtimeFactor = double.Parse(args[++i]); break;
        case "--model": model = args[++i]; break;
        case "--piper-exe": piperExe = args[++i]; break;
        case "--voice": voicePath = args[++i]; break;
        case "--low-water": lowWater = double.Parse(args[++i]); break;
        case "--high-water": highWater = double.Parse(args[++i]); break;
        case "--slowdown": slowdownFactor = double.Parse(args[++i]); break;
        case "--help":
            PrintHelp();
            return 0;
    }
}

Console.WriteLine($"NarratorSimRunner  domain={domain}  seed={seed}  duration={duration}s");
Console.WriteLine($"  model={model}  piper={piperExe}  voice={voicePath}");
Console.WriteLine($"  buffer watermarks low={lowWater}s high={highWater}s  slowdown={slowdownFactor}x");

// ── Setup ──────────────────────────────────────────────────────────────────────
IDomainPack domainPack = domain switch
{
    "island" => new IslandDomainPack(),
    "office" => new OfficeDomainPack(),
    _ => throw new ArgumentException($"Unknown domain: {domain}")
};

var traceSink = new NarrationTraceSink();
var engine = new Engine(domainPack, seed, traceSink);

// Actors
if (domain == "island")
{
    engine.AddActor(new ActorId("Johnny"), new Dictionary<string, object>
    {
        ["STR"] = 12, ["DEX"] = 14, ["CON"] = 13, ["INT"] = 10, ["WIS"] = 11, ["CHA"] = 15,
        ["satiety"] = 70.0, ["energy"] = 80.0, ["morale"] = 60.0
    });
}
else
{
    engine.AddActor(new ActorId("Jim"), new Dictionary<string, object> { ["satiety"] = 80.0, ["energy"] = 80.0 });
    engine.AddActor(new ActorId("Pam"), new Dictionary<string, object> { ["satiety"] = 60.0, ["energy"] = 90.0 });
}

var facts = new CanonicalFacts { Domain = domain };
var promptBuilder = new NarrationPromptBuilder(NarrationTone.Documentary);
var extractor = new TraceBeatExtractor(facts, promptBuilder);
var dilationCtrl = new TimeDilationController(lowWater, highWater, slowdownFactor);
var llmClient = new OllamaLlmClient(model);
var ttsClient = new PiperTtsClient(piperExe, voicePath);

// Audio queue: (wavPath, playAtSimTime, estimatedDurationSeconds)
var audioQueue = new ConcurrentQueue<AudioClip>();
var bufferSeconds = 0.0;
var bufferLock = new object();

// Narration job queue
var jobQueue = new ConcurrentQueue<NarrationJob>();

// Temp directory for WAV files
var wavDir = Path.Combine(Path.GetTempPath(), $"JohnnyLike_wavs_{seed}");
Directory.CreateDirectory(wavDir);

using var cts = new CancellationTokenSource();

// ── Background: narration pipeline ────────────────────────────────────────────
var narrationTask = Task.Run(async () =>
{
    while (!cts.Token.IsCancellationRequested)
    {
        // Drain new trace events → jobs
        while (traceSink.TryDequeue(out var evt))
        {
            var job = extractor.Consume(evt);
            if (job != null)
                jobQueue.Enqueue(job);
        }

        // Process one job
        if (jobQueue.TryDequeue(out var narJob))
        {
            try
            {
                var response = await llmClient.GenerateAsync(narJob.Prompt, cts.Token);
                var parsed = NarrationResponseParser.Parse(response, narJob);

                if (parsed.UpdatedSummary != null)
                    promptBuilder.UpdateSummary(parsed.UpdatedSummary);

                var wavPath = Path.Combine(wavDir, $"{narJob.JobId:N}.wav");
                await ttsClient.SynthesizeToWavAsync(parsed.Narration, wavPath, cts.Token);

                var estimatedSecs = EstimateWavDuration(parsed.Narration);
                lock (bufferLock)
                    bufferSeconds += estimatedSecs;

                audioQueue.Enqueue(new AudioClip(wavPath, narJob.PlayAtSimTime, estimatedSecs));
                Console.WriteLine($"[narration] t={narJob.PlayAtSimTime:F1} [{narJob.Kind}] {parsed.Narration}");
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Console.WriteLine($"[narration-err] {ex.Message}");
            }
        }
        else
        {
            await Task.Delay(50, cts.Token).ConfigureAwait(false);
        }
    }
}, cts.Token);

// ── Background: audio playback ────────────────────────────────────────────────
var playbackTask = Task.Run(async () =>
{
    while (!cts.Token.IsCancellationRequested)
    {
        if (audioQueue.TryPeek(out var clip) && engine.CurrentTime >= clip.PlayAtSimTime)
        {
            audioQueue.TryDequeue(out _);
            try
            {
                PlayWav(clip.WavPath);
                lock (bufferLock)
                    bufferSeconds = Math.Max(0, bufferSeconds - clip.EstimatedDurationSeconds);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[audio-err] {ex.Message}");
            }
        }
        else
        {
            await Task.Delay(100, cts.Token).ConfigureAwait(false);
        }
    }
}, cts.Token);

// ── Main sim loop ──────────────────────────────────────────────────────────────
var executor = new FakeExecutor(engine);
var sw = Stopwatch.StartNew();
var lastElapsed = 0.0;

while (engine.CurrentTime < duration)
{
    var wallNow = sw.Elapsed.TotalSeconds;
    var dtWall = (wallNow - lastElapsed) * realtimeFactor;
    lastElapsed = wallNow;

    double buf;
    lock (bufferLock) buf = bufferSeconds;
    var speedFactor = dilationCtrl.Decide(buf);

    var dtSim = dtWall * speedFactor;
    if (dtSim > 0)
        executor.Update(dtSim);

    // Throttle loop to ~60 fps wall-clock equivalent
    await Task.Delay(16).ConfigureAwait(false);
}

Console.WriteLine($"\nSim completed at t={engine.CurrentTime:F2}s");
cts.Cancel();
try { await Task.WhenAll(narrationTask, playbackTask); } catch { }

// Clean up temp wavs
foreach (var f in Directory.GetFiles(wavDir, "*.wav"))
    try { File.Delete(f); } catch { }

return 0;

// ── Helpers ────────────────────────────────────────────────────────────────────
static double EstimateWavDuration(string text) =>
    Math.Max(1.0, text.Split(' ').Length / 3.0); // rough ~3 words/sec

static void PlayWav(string path)
{
    if (!File.Exists(path)) return;
    // Use aplay on Linux (CI), SoundPlayer on Windows
    if (OperatingSystem.IsWindows())
    {
        PlayWavWindows(path);
    }
    else
    {
        var psi = new ProcessStartInfo("aplay", $"\"{path}\"")
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        using var proc = Process.Start(psi);
        proc?.WaitForExit();
    }
}

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
static void PlayWavWindows(string path)
{
#pragma warning disable CA1416
    using var player = new System.Media.SoundPlayer(path);
    player.PlaySync();
#pragma warning restore CA1416
}

static void PrintHelp()
{
    Console.WriteLine("NarratorSimRunner - narrated life simulation");
    Console.WriteLine("Options:");
    Console.WriteLine("  --domain <island|office>    Domain to simulate (default: island)");
    Console.WriteLine("  --seed <int>                Random seed (default: 42)");
    Console.WriteLine("  --duration <sec>            Sim duration (default: 120)");
    Console.WriteLine("  --realtime-factor <float>   Wall-clock speed multiplier (default: 1.0)");
    Console.WriteLine("  --model <name>              Ollama model name (default: llama3)");
    Console.WriteLine("  --piper-exe <path>          Path to piper.exe (default: piper.exe)");
    Console.WriteLine("  --voice <path>              Path to Piper voice ONNX model");
    Console.WriteLine("  --low-water <sec>           Buffer low watermark seconds (default: 5)");
    Console.WriteLine("  --high-water <sec>          Buffer high watermark seconds (default: 15)");
    Console.WriteLine("  --slowdown <factor>         Speed factor when buffer is low (default: 0.7)");
}

// ── Supporting types ───────────────────────────────────────────────────────────
sealed record AudioClip(string WavPath, double PlayAtSimTime, double EstimatedDurationSeconds);

/// <summary>
/// ITraceSink that also exposes a concurrent dequeue for the narration pipeline.
/// </summary>
sealed class NarrationTraceSink : ITraceSink
{
    private readonly List<TraceEvent> _all = new();
    private readonly ConcurrentQueue<TraceEvent> _pending = new();

    public void Record(TraceEvent evt)
    {
        lock (_all) _all.Add(evt);
        _pending.Enqueue(evt);
    }

    public List<TraceEvent> GetEvents() { lock (_all) return new List<TraceEvent>(_all); }
    public void Clear() { lock (_all) _all.Clear(); }

    public bool TryDequeue(out TraceEvent evt)
    {
        if (_pending.TryDequeue(out var result))
        {
            evt = result;
            return true;
        }
        evt = default!;
        return false;
    }
}
