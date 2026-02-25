using System.Diagnostics;
using System.Threading.Channels;
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
var toneName = "documentary";

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
        case "--tone": toneName = args[++i].ToLowerInvariant(); break;
        case "--help":
            PrintHelp();
            return 0;
    }
}

// ── Startup validation ─────────────────────────────────────────────────────────
if (!File.Exists(piperExe))
    Console.Error.WriteLine($"[warn] Piper executable not found: {piperExe}");
if (!File.Exists(voicePath))
    Console.Error.WriteLine($"[warn] Voice model not found: {voicePath}");

NarrationTone narrationTone = toneName switch
{
    "gritty" => NarrationTone.Gritty,
    "sardonic" => NarrationTone.Sardonic,
    _ => NarrationTone.Documentary
};

Console.WriteLine($"NarratorSimRunner  domain={domain}  seed={seed}  duration={duration}s");
Console.WriteLine($"  tone={narrationTone.Name}  model={model}  piper={piperExe}  voice={voicePath}");
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
var promptBuilder = new NarrationPromptBuilder(narrationTone);
var extractor = new TraceBeatExtractor(facts, promptBuilder);
var dilationCtrl = new TimeDilationController(lowWater, highWater, slowdownFactor);
var llmClient = new OllamaLlmClient(model);
var ttsClient = new PiperTtsClient(piperExe, voicePath);

// Channel pipelines (bounded to avoid unbounded memory growth)
var jobChannel = Channel.CreateBounded<NarrationJob>(
    new BoundedChannelOptions(64) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
var audioChannel = Channel.CreateBounded<AudioClip>(
    new BoundedChannelOptions(32) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

var bufferSeconds = 0.0;
var bufferLock = new object();

// Temp directory for WAV files
var wavDir = Path.Combine(Path.GetTempPath(), $"JohnnyLike_wavs_{seed}");
Directory.CreateDirectory(wavDir);

using var cts = new CancellationTokenSource();

// ── Pipeline task 1: trace events → narration jobs ────────────────────────────
var traceToJobTask = Task.Run(async () =>
{
    try
    {
        await foreach (var evt in traceSink.Events.ReadAllAsync(cts.Token))
        {
            // Debug log: print domain beats as they arrive
            if (evt.EventType == "NarrationBeat" && evt.Details.TryGetValue("text", out var beatText))
            {
                var phase = evt.Details.TryGetValue("phase", out var p) ? p : "?";
                Console.WriteLine($"[beat] t={evt.Time:F1} [{phase}] {beatText}");
            }

            var job = extractor.Consume(evt);
            if (job != null)
                await jobChannel.Writer.WriteAsync(job, cts.Token).ConfigureAwait(false);
        }
    }
    catch (OperationCanceledException) { }
    finally { jobChannel.Writer.TryComplete(); }
});

// ── Pipeline task 2: narration jobs → LLM → TTS → audio clips ─────────────────
var jobToAudioTask = Task.Run(async () =>
{
    try
    {
        await foreach (var narJob in jobChannel.Reader.ReadAllAsync(cts.Token))
        {
            try
            {
                var response = await llmClient.GenerateAsync(narJob.Prompt, cts.Token).ConfigureAwait(false);
                var parsed = NarrationResponseParser.Parse(response, narJob);

                if (parsed.UpdatedSummary != null)
                    promptBuilder.UpdateSummary(parsed.UpdatedSummary);

                var wavPath = Path.Combine(wavDir, $"{narJob.JobId:N}.wav");
                await ttsClient.SynthesizeToWavAsync(parsed.Narration, wavPath, cts.Token).ConfigureAwait(false);

                var clipDuration = ReadWavDuration(wavPath);
                lock (bufferLock) bufferSeconds += clipDuration;

                await audioChannel.Writer.WriteAsync(
                    new AudioClip(wavPath, narJob.PlayAtSimTime, clipDuration), cts.Token).ConfigureAwait(false);

                Console.WriteLine($"[narration] t={narJob.PlayAtSimTime:F1} [{narJob.Kind}] {parsed.Narration}");
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Console.WriteLine($"[narration-err] {ex.Message}");
            }
        }
    }
    catch (OperationCanceledException) { }
    finally { audioChannel.Writer.TryComplete(); }
});

// ── Pipeline task 3: audio playback ──────────────────────────────────────────
var playbackTask = Task.Run(async () =>
{
    try
    {
        await foreach (var clip in audioChannel.Reader.ReadAllAsync(cts.Token))
        {
            // Wait until sim time reaches the scheduled play time
            while (engine.CurrentTime < clip.PlayAtSimTime && !cts.Token.IsCancellationRequested)
                await Task.Delay(50, cts.Token).ConfigureAwait(false);

            if (cts.Token.IsCancellationRequested) break;

            try
            {
                PlayWav(clip.WavPath);
                lock (bufferLock)
                    bufferSeconds = Math.Max(0, bufferSeconds - clip.DurationSeconds);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[audio-err] {ex.Message}");
            }
        }
    }
    catch (OperationCanceledException) { }
});

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
traceSink.CompleteAdding(); // signals traceToJobTask to finish cleanly
cts.Cancel();
try { await Task.WhenAll(traceToJobTask, jobToAudioTask, playbackTask); } catch { }

// Clean up temp wavs
foreach (var f in Directory.GetFiles(wavDir, "*.wav"))
    try { File.Delete(f); } catch { }

return 0;

// ── Helpers ────────────────────────────────────────────────────────────────────

/// <summary>
/// Reads the WAV RIFF header to compute the exact clip duration.
/// Falls back to 1.0 s on any read failure or unsupported format.
/// </summary>
static double ReadWavDuration(string path)
{
    // WAV chunk identifiers (little-endian uint32)
    const uint RiffSignature = 0x46464952u; // "RIFF"
    const uint WaveSignature = 0x45564157u; // "WAVE"
    const uint FmtChunkId   = 0x20746d66u; // "fmt "
    const uint DataChunkId  = 0x61746164u; // "data"

    // Bytes read from the fmt chunk before seeking past the remainder:
    // AudioFormat(2) + NumChannels(2) + SampleRate(4) + ByteRate(4) = 12
    const int FmtBytesRead = 12;

    try
    {
        using var br = new BinaryReader(File.OpenRead(path));

        if (br.ReadUInt32() != RiffSignature) return 1.0;
        br.ReadUInt32(); // file size - 8
        if (br.ReadUInt32() != WaveSignature) return 1.0;

        int byteRate = 0;
        while (br.BaseStream.Position <= br.BaseStream.Length - 8)
        {
            var chunkId   = br.ReadUInt32();
            var chunkSize = (int)br.ReadUInt32();

            if (chunkId == FmtChunkId)
            {
                br.ReadUInt16(); // AudioFormat
                br.ReadUInt16(); // NumChannels
                br.ReadUInt32(); // SampleRate
                byteRate = (int)br.ReadUInt32();
                // Skip the rest of the fmt chunk beyond the bytes already read
                br.BaseStream.Seek(chunkSize - FmtBytesRead, SeekOrigin.Current);
            }
            else if (chunkId == DataChunkId)
            {
                return byteRate > 0 ? (double)chunkSize / byteRate : 1.0;
            }
            else
            {
                br.BaseStream.Seek(chunkSize, SeekOrigin.Current);
            }
        }
        return 1.0;
    }
    catch
    {
        return 1.0;
    }
}

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
    Console.WriteLine("  --domain <island|office>         Domain to simulate (default: island)");
    Console.WriteLine("  --seed <int>                     Random seed (default: 42)");
    Console.WriteLine("  --duration <sec>                 Sim duration (default: 120)");
    Console.WriteLine("  --realtime-factor <float>        Wall-clock speed multiplier (default: 1.0)");
    Console.WriteLine("  --tone <documentary|gritty|sardonic>  Narration style (default: documentary)");
    Console.WriteLine("  --model <name>                   Ollama model name (default: llama3)");
    Console.WriteLine("  --piper-exe <path>               Path to piper.exe (default: piper.exe)");
    Console.WriteLine("  --voice <path>                   Path to Piper voice ONNX model");
    Console.WriteLine("  --low-water <sec>                Buffer low watermark seconds (default: 5)");
    Console.WriteLine("  --high-water <sec>               Buffer high watermark seconds (default: 15)");
    Console.WriteLine("  --slowdown <factor>              Speed factor when buffer is low (default: 0.7)");
}

// ── Supporting types ───────────────────────────────────────────────────────────
sealed record AudioClip(string WavPath, double PlayAtSimTime, double DurationSeconds);
