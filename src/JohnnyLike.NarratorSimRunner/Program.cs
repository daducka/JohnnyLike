using System.Diagnostics;
using System.Threading.Channels;
using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.Engine;
using JohnnyLike.Llm;
using JohnnyLike.Narration;
using JohnnyLike.SimRunner;
using JohnnyLike.Tts;

// CLI parsing
var domain = "island";
var seed = 42;
var duration = 120.0;
var realtimeFactor = 1.0;
var model = "llama3";
string? piperExe = null;
string? voicePath = null;
var piperProvided = false;
var voiceProvided = false;
var lowWater = 5.0;
var highWater = 15.0;
var slowdownFactor = 0.7;
var toneName = "documentary";
var verbose = false;
var verboseEvery = 5.0;
var showPrompts = false;
var maxSpeed = false;
var maxSpeedStepTicks = 1L;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--domain": domain = args[++i]; break;
        case "--seed": seed = int.Parse(args[++i]); break;
        case "--duration": duration = double.Parse(args[++i]); break;
        case "--realtime-factor": realtimeFactor = double.Parse(args[++i]); break;
        case "--model": model = args[++i]; break;
        case "--piper-exe": piperExe = args[++i]; piperProvided = true; break;
        case "--voice": voicePath = args[++i]; voiceProvided = true; break;
        case "--low-water": lowWater = double.Parse(args[++i]); break;
        case "--high-water": highWater = double.Parse(args[++i]); break;
        case "--slowdown": slowdownFactor = double.Parse(args[++i]); break;
        case "--tone": toneName = args[++i].ToLowerInvariant(); break;
        case "--verbose": verbose = true; break;
        case "--verbose-every": verboseEvery = Math.Max(0.5, double.Parse(args[++i])); break;
        case "--show-prompts": showPrompts = true; break;
        case "--max-speed": maxSpeed = true; break;
        case "--max-speed-step-ticks": maxSpeedStepTicks = Math.Max(1L, long.Parse(args[++i])); break;
        case "--help":
            PrintHelp();
            return 0;
    }
}

var enableAudio = piperProvided && voiceProvided;

// Startup validation
if (piperProvided != voiceProvided)
{
    Console.Error.WriteLine("[warn] Audio disabled: provide both --piper-exe and --voice to enable voice output.");
}
else if (enableAudio)
{
    if (!File.Exists(piperExe))
        Console.Error.WriteLine($"[warn] Piper executable not found: {piperExe}");
    if (!File.Exists(voicePath))
        Console.Error.WriteLine($"[warn] Voice model not found: {voicePath}");
}

NarrationTone narrationTone = toneName switch
{
    "gritty" => NarrationTone.Gritty,
    "sardonic" => NarrationTone.Sardonic,
    _ => NarrationTone.Documentary
};

Console.WriteLine($"NarratorSimRunner  domain={domain}  seed={seed}  duration={duration}s");
Console.WriteLine($"  tone={narrationTone.Name}  model={model}");
if (enableAudio)
{
    Console.WriteLine($"  audio=enabled  piper={piperExe}  voice={voicePath}");
    Console.WriteLine($"  buffer watermarks low={lowWater}s high={highWater}s  slowdown={slowdownFactor}x");
}
else
{
    Console.WriteLine("  audio=disabled (text-only mode)");
}
if (verbose)
    Console.WriteLine($"  debug=verbose (every {verboseEvery:F1}s)");
if (showPrompts)
    Console.WriteLine("  debug=prompt-dump enabled");
if (maxSpeed)
    Console.WriteLine($"  pacing=max-speed (step={maxSpeedStepTicks} ticks)");

// Setup
IDomainPack domainPack = domain switch
{
    "island" => new IslandDomainPack(),
    "office" => new JohnnyLike.Domain.Island.IslandDomainPack(),
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
var llmClient = new OllamaLlmClient(
    model,
    maxRetries: 1,
    requestTimeout: TimeSpan.FromSeconds(8));
ITtsClient? ttsClient = enableAudio ? new PiperTtsClient(piperExe!, voicePath!) : null;

// Channel pipelines (bounded to avoid unbounded memory growth)
var jobChannel = Channel.CreateBounded<NarrationJob>(
    new BoundedChannelOptions(64) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
var audioChannel = Channel.CreateBounded<AudioClip>(
    new BoundedChannelOptions(32) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

var bufferSeconds = 0.0;
var bufferLock = new object();
long traceEventsSeen = 0;
long narrationJobsQueued = 0;
long narrationJobsStarted = 0;
long narrationJobsCompleted = 0;
long narrationErrors = 0;

// Temp directory for WAV files
var wavDir = enableAudio ? Path.Combine(Path.GetTempPath(), $"JohnnyLike_wavs_{seed}") : null;
if (wavDir != null)
    Directory.CreateDirectory(wavDir);

using var cts = new CancellationTokenSource();

// Pipeline task 1: trace events -> narration jobs
var traceToJobTask = Task.Run(async () =>
{
    try
    {
        await foreach (var evt in traceSink.Events.ReadAllAsync(cts.Token))
        {
            Interlocked.Increment(ref traceEventsSeen);

            // Debug log: print domain beats as they arrive
            if (evt.EventType == "NarrationBeat" && evt.Details.TryGetValue("text", out var beatText))
            {
                var phase = evt.Details.TryGetValue("phase", out var p) ? p : "?";
                Console.WriteLine($"[beat] t={evt.TimeSeconds:F1} [{phase}] {beatText}");
            }
            else if (verbose && (evt.EventType == "ActionAssigned" || evt.EventType == "ActionCompleted"))
            {
                Console.WriteLine($"[trace] {evt}");
            }

            var job = extractor.Consume(evt);
            if (job != null)
            {
                Interlocked.Increment(ref narrationJobsQueued);
                await jobChannel.Writer.WriteAsync(job, cts.Token).ConfigureAwait(false);
                if (verbose)
                    Console.WriteLine($"[job:queued] id={job.JobId:N} kind={job.Kind} playAt={job.PlayAtSimTime:F1}");
            }
        }
    }
    catch (OperationCanceledException) { }
    finally { jobChannel.Writer.TryComplete(); }
});

// Pipeline task 2: narration jobs -> LLM -> (optional) TTS -> (optional) audio clips
var jobToAudioTask = Task.Run(async () =>
{
    try
    {
        await foreach (var narJob in jobChannel.Reader.ReadAllAsync(cts.Token))
        {
            try
            {
                Interlocked.Increment(ref narrationJobsStarted);
                var llmSw = Stopwatch.StartNew();
                if (verbose)
                    Console.WriteLine($"[llm:start] id={narJob.JobId:N} kind={narJob.Kind} t={narJob.PlayAtSimTime:F1}");
                if (showPrompts)
                {
                    Console.WriteLine($"[prompt:start] id={narJob.JobId:N} kind={narJob.Kind} t={narJob.PlayAtSimTime:F1}");
                    Console.WriteLine(narJob.Prompt);
                    Console.WriteLine($"[prompt:end] id={narJob.JobId:N}");
                }

                var response = await llmClient.GenerateAsync(narJob.Prompt, cts.Token).ConfigureAwait(false);
                var parsed = NarrationResponseParser.Parse(response, narJob);
                llmSw.Stop();
                if (verbose)
                    Console.WriteLine($"[llm:done] id={narJob.JobId:N} ms={llmSw.ElapsedMilliseconds} chars={response.Length}");

                if (parsed.UpdatedSummary != null)
                    promptBuilder.UpdateSummary(parsed.UpdatedSummary);

                Console.WriteLine($"[narration] t={narJob.PlayAtSimTime:F1} [{narJob.Kind}] {parsed.Narration}");
                Interlocked.Increment(ref narrationJobsCompleted);

                if (enableAudio)
                {
                    var wavPath = Path.Combine(wavDir!, $"{narJob.JobId:N}.wav");
                    await ttsClient!.SynthesizeToWavAsync(parsed.Narration, wavPath, cts.Token).ConfigureAwait(false);

                    var clipDuration = ReadWavDuration(wavPath);
                    lock (bufferLock) bufferSeconds += clipDuration;

                    await audioChannel.Writer.WriteAsync(
                        new AudioClip(wavPath, narJob.PlayAtSimTime, clipDuration), cts.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Interlocked.Increment(ref narrationErrors);
                Console.WriteLine($"[narration-err] {ex.Message}");
            }
        }
    }
    catch (OperationCanceledException) { }
    finally { audioChannel.Writer.TryComplete(); }
});

// Pipeline task 3: audio playback
var playbackTask = Task.Run(async () =>
{
    try
    {
        await foreach (var clip in audioChannel.Reader.ReadAllAsync(cts.Token))
        {
            // Wait until sim time reaches the scheduled play time
            while (engine.CurrentSeconds < clip.PlayAtSimTime && !cts.Token.IsCancellationRequested)
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
if (!enableAudio)
{
    audioChannel.Writer.TryComplete();
    playbackTask = Task.CompletedTask;
}

// Main sim loop
var executor = new FakeExecutor(engine);
var sw = Stopwatch.StartNew();
var lastElapsed = 0.0;
var lastVerboseWall = 0.0;

while (engine.CurrentSeconds < duration)
{
    var wallNow = sw.Elapsed.TotalSeconds;

    if (maxSpeed)
    {
        var pendingNarration = Interlocked.Read(ref narrationJobsQueued) - Interlocked.Read(ref narrationJobsCompleted);
        if (pendingNarration <= 0)
        {
            // Run sim at max throughput while the narration pipeline is idle.
            var ticksRemaining = (long)Math.Ceiling((duration - engine.CurrentSeconds) * Engine.TickHz);
            var stepTicks = Math.Min(maxSpeedStepTicks, Math.Max(1L, ticksRemaining));
            executor.AdvanceTicks(stepTicks);
        }
        else
        {
            // Yield briefly while waiting for narration generation to catch up.
            await Task.Delay(1).ConfigureAwait(false);
        }
    }
    else
    {
        var dtWall = (wallNow - lastElapsed) * realtimeFactor;
        lastElapsed = wallNow;

        double buf;
        lock (bufferLock) buf = bufferSeconds;
        var speedFactor = enableAudio ? dilationCtrl.Decide(buf) : 1.0;

        var dtSim = dtWall * speedFactor;
        if (dtSim > 0)
            executor.Update(dtSim);

        // Throttle loop to ~60 fps wall-clock equivalent
        await Task.Delay(16).ConfigureAwait(false);
    }

    if (verbose && wallNow - lastVerboseWall >= verboseEvery)
    {
        lastVerboseWall = wallNow;
        var actorStates = string.Join(" | ", engine.Actors.Select(kvp =>
            $"{kvp.Key.Value}:{kvp.Value.Status}" +
            (kvp.Value.CurrentAction != null ? $"/{kvp.Value.CurrentAction.Id.Value}" : "")));
        Console.WriteLine(
            $"[loop] wall={wallNow:F1}s sim={engine.CurrentSeconds:F1}s tick={engine.CurrentTick} " +
            $"trace={Interlocked.Read(ref traceEventsSeen)} jobs={Interlocked.Read(ref narrationJobsQueued)}/" +
            $"{Interlocked.Read(ref narrationJobsStarted)}/{Interlocked.Read(ref narrationJobsCompleted)} " +
            $"errs={Interlocked.Read(ref narrationErrors)} actors={actorStates}");
    }
}

Console.WriteLine($"\nSim completed at t={engine.CurrentSeconds:F2}s");
traceSink.CompleteAdding(); // signals traceToJobTask to finish cleanly
var pipelineDrainTask = Task.WhenAll(traceToJobTask, jobToAudioTask, playbackTask);
var drainTimeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
var finishedTask = await Task.WhenAny(pipelineDrainTask, drainTimeoutTask).ConfigureAwait(false);
if (finishedTask != pipelineDrainTask)
{
    Console.WriteLine("[warn] Narration pipeline did not drain within 30s; cancelling pending work.");
    cts.Cancel();
}
try { await pipelineDrainTask.ConfigureAwait(false); } catch { }
if (verbose)
{
    Console.WriteLine(
        $"[final] trace={Interlocked.Read(ref traceEventsSeen)} jobs={Interlocked.Read(ref narrationJobsQueued)}/" +
        $"{Interlocked.Read(ref narrationJobsStarted)}/{Interlocked.Read(ref narrationJobsCompleted)} " +
        $"errs={Interlocked.Read(ref narrationErrors)}");
}

// Clean up temp wavs
if (wavDir != null)
{
    foreach (var f in Directory.GetFiles(wavDir, "*.wav"))
        try { File.Delete(f); } catch { }
}

return 0;

// Helpers

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
    Console.WriteLine("  --piper-exe <path>               Path to piper executable (required with --voice to enable audio)");
    Console.WriteLine("  --voice <path>                   Path to Piper voice ONNX model (required with --piper-exe)");
    Console.WriteLine("  --low-water <sec>                Buffer low watermark seconds (default: 5)");
    Console.WriteLine("  --high-water <sec>               Buffer high watermark seconds (default: 15)");
    Console.WriteLine("  --slowdown <factor>              Speed factor when buffer is low (default: 0.7)");
    Console.WriteLine("  --max-speed                      Run as fast as possible; pause only while narration is pending");
    Console.WriteLine("  --max-speed-step-ticks <int>     Tick batch size in max-speed mode (default: 1)");
    Console.WriteLine("  --verbose                        Enable detailed runner diagnostics");
    Console.WriteLine("  --verbose-every <sec>            Verbose heartbeat interval (default: 5.0, min 0.5)");
    Console.WriteLine("  --show-prompts                   Print full LLM prompt for each narration job");
    Console.WriteLine("Notes:");
    Console.WriteLine("  - Default mode is text-only narration.");
    Console.WriteLine("  - Audio playback is enabled only when BOTH --piper-exe and --voice are provided.");
}

// Supporting types
sealed record AudioClip(string WavPath, double PlayAtSimTime, double DurationSeconds);
