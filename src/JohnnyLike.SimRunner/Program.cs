using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.SimRunner;
using JohnnyLike.SimRunner.Optimizer;
using JohnnyLike.SimRunner.PressureFuzzer;
using System.Text.Json;

// ── Pressure Fuzzer subcommand ─────────────────────────────────────────────
if (args.Length > 0 && args[0] == "pressure-fuzzer")
{
    RunPressureFuzzer(args[1..]);
    return;
}

// ── Optimizer subcommand ───────────────────────────────────────────────────
if (args.Length > 0 && args[0] == "optimizer")
{
    RunOptimizer(args[1..]);
    return;
}

// ── evaluate-golden subcommand ─────────────────────────────────────────────
if (args.Length > 0 && args[0] == "evaluate-golden")
{
    RunEvaluateGolden(args[1..]);
    return;
}

// ── optimize-golden subcommand ─────────────────────────────────────────────
if (args.Length > 0 && args[0] == "optimize-golden")
{
    RunOptimizeGolden(args[1..]);
    return;
}

// ── run-fuzzer subcommand ──────────────────────────────────────────────────
if (args.Length > 0 && args[0] == "run-fuzzer")
{
    RunFuzzerComparison(args[1..]);
    return;
}

if (args.Length == 0)
{
    Console.WriteLine("JohnnyLike SimRunner");
    Console.WriteLine("Usage: SimRunner [options]");
    Console.WriteLine("  --scenario <path>         Load and run scenario from JSON file");
    Console.WriteLine("  --domain <name>           Domain to use: island (default: island)");
    Console.WriteLine("  --seed <number>           Random seed (default: 42)");
    Console.WriteLine("  --duration <sec>          Simulation duration in seconds");
    Console.WriteLine("  --trace                   Output detailed trace");
    Console.WriteLine("  --decision-summary        Emit summary decision trace events (chosen action + reason)");
    Console.WriteLine("  --decision-candidates     Emit per-candidate decision trace events");
    Console.WriteLine("  --decision-verbose        Emit verbose decision trace (includes scoring explanation)");
    Console.WriteLine("  --snapshot-interval <sec> Emit periodic simulation snapshots every <sec> simulation seconds");
    Console.WriteLine("  --save-artifacts [folder] Save trace logs to artifacts/ directory (optional subfolder)");
    Console.WriteLine("  --actor <name>            Starting actor archetype (default: Johnny)");
    Console.WriteLine($"                            Available: {string.Join(", ", Archetypes.All.Keys)}");
    Console.WriteLine("\nFuzz Testing:");
    Console.WriteLine("  --fuzz              Run fuzz testing mode");
    Console.WriteLine("  --runs <number>     Number of fuzz runs (default: 1)");
    Console.WriteLine("  --config <path>     Load fuzz config from JSON file");
    Console.WriteLine("  --profile <name>    Use predefined profile: smoke, extended, nightly");
    Console.WriteLine("  --verbose           Verbose output for fuzz runs");
    Console.WriteLine("  --save-artifacts [folder] Save test artifacts to disk (optional subfolder)");
    Console.WriteLine("\nDecision Surface Explorer (Pressure Fuzzer):");
    Console.WriteLine("  pressure-fuzzer             Run the decision surface sampler");
    Console.WriteLine("  --actors <names|all>        Comma-separated actor names or 'all' (default: all)");
    Console.WriteLine($"                              Available: {string.Join(", ", Archetypes.All.Keys)}");
    Console.WriteLine("  --scenario <name|all>       Scenario name or 'all' (default: all)");
    Console.WriteLine($"                              Available: {string.Join(", ", Enum.GetNames<FuzzerScenarioKind>())}");
    Console.WriteLine("  --output <path>             Output JSON file path (default: ./fuzzer-output.json)");
    Console.WriteLine("  --grid coarse|fine          Stat sampling density (default: coarse)");
    Console.WriteLine("  --top <n>                   Top N candidates to include (default: 5)");
    Console.WriteLine("  --profile <path>            Path to a tuning profile JSON file (default: production profile)");
    Console.WriteLine("\nOptimizer:");
    Console.WriteLine("  optimizer                   Run parameter-space optimizer against golden states");
    Console.WriteLine("  --base-profile <path>       Path to base tuning profile JSON (default: production profile)");
    Console.WriteLine("  --golden-states <path>      Path to golden-states JSON file (default: embedded dataset)");
    Console.WriteLine("  --output <path>             Output JSON file path (default: ./optimizer-results.json)");
    Console.WriteLine("  --max-iterations <n>        Max coordinate-descent iterations (default: 20)");
    return;
}

var scenarioPath = "";
var domainName = "island";
var seed = 42;
var duration = 60.0;
var outputTrace = false;
var fuzzMode = false;
var fuzzRuns = 1;
var fuzzConfigPath = "";
var fuzzProfile = "";
var verbose = false;
var saveArtifacts = false;
var artifactsSubDir = "";
var decisionTraceLevel = JohnnyLike.Engine.DecisionTraceLevel.None;
var actorName = "Johnny";
var snapshotIntervalSeconds = 0.0; // 0 = disabled

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--scenario":
            scenarioPath = args[++i];
            break;
        case "--domain":
            domainName = args[++i].ToLowerInvariant();
            break;
        case "--seed":
            seed = int.Parse(args[++i]);
            break;
        case "--duration":
            duration = double.Parse(args[++i]);
            break;
        case "--trace":
            outputTrace = true;
            break;
        case "--decision-summary":
            if (decisionTraceLevel < JohnnyLike.Engine.DecisionTraceLevel.Summary)
                decisionTraceLevel = JohnnyLike.Engine.DecisionTraceLevel.Summary;
            break;
        case "--decision-candidates":
            if (decisionTraceLevel < JohnnyLike.Engine.DecisionTraceLevel.Candidates)
                decisionTraceLevel = JohnnyLike.Engine.DecisionTraceLevel.Candidates;
            break;
        case "--decision-verbose":
            decisionTraceLevel = JohnnyLike.Engine.DecisionTraceLevel.Verbose;
            break;
        case "--fuzz":
            fuzzMode = true;
            break;
        case "--runs":
            fuzzRuns = int.Parse(args[++i]);
            break;
        case "--config":
            fuzzConfigPath = args[++i];
            break;
        case "--profile":
            fuzzProfile = args[++i];
            break;
        case "--verbose":
            verbose = true;
            break;
        case "--save-artifacts":
            saveArtifacts = true;
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                artifactsSubDir = args[++i];
            break;
        case "--actor":
            actorName = args[++i];
            break;
        case "--snapshot-interval":
            snapshotIntervalSeconds = double.Parse(args[++i]);
            if (snapshotIntervalSeconds <= 0.0)
                throw new ArgumentException("--snapshot-interval must be a positive number of seconds.");
            break;
    }
}

if (fuzzMode)
{
    RunFuzz(seed, fuzzRuns, fuzzConfigPath, fuzzProfile, verbose, domainName, saveArtifacts, artifactsSubDir);
}
else if (!string.IsNullOrEmpty(scenarioPath))
{
    RunScenario(scenarioPath, outputTrace, domainName, decisionTraceLevel, saveArtifacts, artifactsSubDir, snapshotIntervalSeconds);
}
else
{
    RunDefault(seed, duration, outputTrace, domainName, decisionTraceLevel, actorName, saveArtifacts, artifactsSubDir, snapshotIntervalSeconds);
}

IDomainPack CreateDomainPack(string domainName)
{
    return domainName switch
    {
        "island" => new IslandDomainPack(),
        _ => throw new ArgumentException($"Unknown domain: {domainName}. Valid domains: island")
    };
}

void RunScenario(string path, bool trace, string domainName, JohnnyLike.Engine.DecisionTraceLevel decisionLevel, bool saveArtifacts = false, string artifactsSubDir = "", double snapshotIntervalSeconds = 0.0)
{
    var scenario = ScenarioLoader.LoadFromFile(path);
    using var writer = CreateArtifactWriter(saveArtifacts, domainName, scenario.Seed, artifactsSubDir: artifactsSubDir);

    writer.WriteLine($"Loading scenario from: {path}");
    writer.WriteLine($"Running scenario: {scenario.Name}");
    writer.WriteLine($"Seed: {scenario.Seed}, Duration: {scenario.DurationSeconds}s");
    writer.WriteLine($"Domain: {domainName}");
    
    var domainPack = CreateDomainPack(domainName);
    var traceSink = new InMemoryTraceSink();
    var traceOptions = new JohnnyLike.Engine.DecisionTraceOptions(decisionLevel);
    var engine = new JohnnyLike.Engine.Engine(domainPack, scenario.Seed, traceSink, traceOptions);
    
    // Add actors
    foreach (var actorDef in scenario.Actors)
    {
        engine.AddActor(new ActorId(actorDef.ActorId), actorDef.InitialState);
        writer.WriteLine($"Added actor: {actorDef.ActorId}");
    }
    
    // Enqueue signals
    foreach (var signal in scenario.Signals)
    {
        engine.EnqueueSignal(new Signal(
            signal.Type,
            (long)(signal.AtTime * 20),
            string.IsNullOrEmpty(signal.TargetActor) ? null : new ActorId(signal.TargetActor),
            signal.Data
        ));
    }
    
    var executor = new FakeExecutor(engine);
    var timeStep = 0.5;
    var elapsed = 0.0;

    var snapshotIntervalTicks = snapshotIntervalSeconds > 0.0
        ? (long)(snapshotIntervalSeconds * JohnnyLike.Engine.Engine.TickHz)
        : 0L;
    var nextSnapshotTick = snapshotIntervalTicks > 0L ? snapshotIntervalTicks : long.MaxValue;

    while (elapsed < scenario.DurationSeconds)
    {
        executor.Update(timeStep);
        elapsed += timeStep;

        if (snapshotIntervalTicks > 0L && engine.CurrentTick >= nextSnapshotTick)
        {
            foreach (var evt in domainPack.BuildPeriodicSnapshot(engine.WorldState, engine.Actors, engine.CurrentTick))
                traceSink.Record(evt);
            nextSnapshotTick += snapshotIntervalTicks;
        }
    }
    
    writer.WriteLine($"\nSimulation completed at t={engine.CurrentSeconds:F2}s");
    writer.WriteLine($"Total events: {traceSink.GetEvents().Count}");
    
    if (trace)
    {
        writer.WriteLine("\n=== TRACE ===");
        foreach (var evt in traceSink.GetEvents())
        {
            writer.WriteLine(evt.ToString());
        }
    }
    else if (decisionLevel > JohnnyLike.Engine.DecisionTraceLevel.None)
    {
        PrintDecisionSummary(traceSink.GetEvents(), decisionLevel, writer);
    }
    
    var hash = JohnnyLike.Engine.TraceHelper.ComputeTraceHash(traceSink.GetEvents());
    writer.WriteLine($"\nTrace hash: {hash}");
}

void RunDefault(int seed, double duration, bool trace, string domainName, JohnnyLike.Engine.DecisionTraceLevel decisionLevel, string actorName = "Johnny", bool saveArtifacts = false, string artifactsSubDir = "", double snapshotIntervalSeconds = 0.0)
{
    using var writer = CreateArtifactWriter(saveArtifacts, domainName, seed, actorName, artifactsSubDir);

    writer.WriteLine($"Running default {domainName} simulation");
    writer.WriteLine($"Seed: {seed}, Duration: {duration}s");
    
    var domainPack = CreateDomainPack(domainName);
    var traceSink = new InMemoryTraceSink();
    var traceOptions = new JohnnyLike.Engine.DecisionTraceOptions(decisionLevel);
    var engine = new JohnnyLike.Engine.Engine(domainPack, seed, traceSink, traceOptions);
    
    if (domainName == "island")
    {
        if (!Archetypes.All.TryGetValue(actorName, out var actorState))
            throw new ArgumentException($"Unknown actor: {actorName}. Available: {string.Join(", ", Archetypes.All.Keys)}");

        writer.WriteLine($"Actor: {actorName}");
        engine.AddActor(new ActorId(actorName), actorState);
    }
    
    var executor = new FakeExecutor(engine);
    var timeStep = 0.5;
    var elapsed = 0.0;

    var snapshotIntervalTicks = snapshotIntervalSeconds > 0.0
        ? (long)(snapshotIntervalSeconds * JohnnyLike.Engine.Engine.TickHz)
        : 0L;
    var nextSnapshotTick = snapshotIntervalTicks > 0L ? snapshotIntervalTicks : long.MaxValue;

    while (elapsed < duration)
    {
        executor.Update(timeStep);
        elapsed += timeStep;

        if (snapshotIntervalTicks > 0L && engine.CurrentTick >= nextSnapshotTick)
        {
            foreach (var evt in domainPack.BuildPeriodicSnapshot(engine.WorldState, engine.Actors, engine.CurrentTick))
                traceSink.Record(evt);
            nextSnapshotTick += snapshotIntervalTicks;
        }
    }
    
    writer.WriteLine($"\nSimulation completed at t={engine.CurrentSeconds:F2}s");
    writer.WriteLine($"Total events: {traceSink.GetEvents().Count}");
    
    if (trace)
    {
        writer.WriteLine("\n=== TRACE ===");
        foreach (var evt in traceSink.GetEvents())
        {
            writer.WriteLine(evt.ToString());
        }
    }
    else if (decisionLevel > JohnnyLike.Engine.DecisionTraceLevel.None)
    {
        PrintDecisionSummary(traceSink.GetEvents(), decisionLevel, writer);
    }
    
    var hash = JohnnyLike.Engine.TraceHelper.ComputeTraceHash(traceSink.GetEvents());
    writer.WriteLine($"\nTrace hash: {hash}");

    PrintPersonalityTiming(traceSink.GetEvents(), writer);
}

void RunFuzz(int baseSeed, int runs, string configPath, string profileName, bool verbose, string domainName, bool saveArtifacts, string artifactsSubDir = "")
{
    Console.WriteLine("=== FUZZ TESTING MODE ===");
    Console.WriteLine($"Runs: {runs}");
    Console.WriteLine($"Base Seed: {baseSeed}");
    Console.WriteLine($"Domain: {domainName}\n");

    FuzzConfig config;
    if (!string.IsNullOrEmpty(configPath))
    {
        var json = File.ReadAllText(configPath);
        config = JsonSerializer.Deserialize<FuzzConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? FuzzConfig.Default;
        Console.WriteLine($"Loaded config from: {configPath}");
    }
    else if (!string.IsNullOrEmpty(profileName))
    {
        var profile = profileName.ToLowerInvariant() switch
        {
            "smoke" => FuzzProfile.Smoke,
            "extended" => FuzzProfile.Extended,
            "nightly" => FuzzProfile.Nightly,
            _ => throw new ArgumentException($"Unknown profile: {profileName}. Valid profiles: smoke, extended, nightly")
        };
        config = FuzzConfig.FromProfile(profile, baseSeed);
        Console.WriteLine($"Using profile: {profileName}");
    }
    else
    {
        config = FuzzConfig.Default with { Seed = baseSeed };
        Console.WriteLine("Using default config");
    }

    var failures = new List<FuzzRunResult>();
    var successCount = 0;
    var domainPack = CreateDomainPack(domainName);

    for (int i = 0; i < runs; i++)
    {
        var runConfig = config with { Seed = baseSeed + i };
        Console.WriteLine($"\n--- Run {i + 1}/{runs} (seed: {runConfig.Seed}) ---");

        var result = FuzzRunner.Run(runConfig, domainPack);
        
        if (result.Success)
        {
            successCount++;
            if (verbose)
            {
                FuzzRunner.PrintResult(result, verbose: true);
            }
            else
            {
                Console.WriteLine($"✓ PASSED - {result.Metrics.CompletedActions} actions, hash: {result.TraceHash.Substring(0, 16)}...");
            }
        }
        else
        {
            failures.Add(result);
            FuzzRunner.PrintResult(result, verbose: true);
        }
    }

    Console.WriteLine($"\n{'=',60}");
    Console.WriteLine($"FUZZ TESTING COMPLETE");
    Console.WriteLine($"{'=',60}");
    Console.WriteLine($"Total Runs: {runs}");
    Console.WriteLine($"Passed: {successCount}");
    Console.WriteLine($"Failed: {failures.Count}");

    // Save artifacts
    if (saveArtifacts)
    {
        SaveFuzzArtifacts(profileName, runs, successCount, failures, artifactsSubDir);
    }

    if (failures.Count > 0)
    {
        Console.WriteLine($"\n--- Failed Run Seeds ---");
        foreach (var failure in failures)
        {
            Console.WriteLine($"Seed {failure.Config.Seed}: {failure.FailureReason}");
        }
        Environment.Exit(1);
    }
    else
    {
        Console.WriteLine("\n✓ All fuzz runs passed!");
    }
}

void PrintPersonalityTiming(List<TraceEvent> events, TextWriter writer)
{
    // Build a lookup: actorId -> (actionId -> (first completion time, count))
    var firstTimes = new Dictionary<string, Dictionary<string, (double FirstTime, int Count)>>();

    foreach (var evt in events)
    {
        if (evt.EventType != "ActionCompleted") continue;
        if (!evt.ActorId.HasValue) continue;
        if (!evt.Details.TryGetValue("actionId", out var actionIdObj)) continue;

        var actionId = actionIdObj?.ToString() ?? "";
        if (string.IsNullOrEmpty(actionId)) continue;

        var actor = evt.ActorId.Value.Value;
        if (!firstTimes.TryGetValue(actor, out var actorMap))
        {
            actorMap = new Dictionary<string, (double, int)>();
            firstTimes[actor] = actorMap;
        }

        if (actorMap.TryGetValue(actionId, out var existing))
            actorMap[actionId] = (existing.FirstTime, existing.Count + 1);
        else
            actorMap[actionId] = (evt.TimeSeconds, 1);
    }

    if (firstTimes.Count == 0)
        return;

    // Collect all unique action ids seen across all actors, sorted for stable output
    var allActions = firstTimes.Values
        .SelectMany(m => m.Keys)
        .Distinct()
        .OrderBy(a => a)
        .ToList();

    writer.WriteLine("\n=== PersonalityTiming ===");
    var actors = firstTimes.Keys.OrderBy(a => a).ToList();
    var actionColWidth = Math.Max(26, allActions.DefaultIfEmpty("").Max(a => a.Length) + 2);

    var actorColWidths = actors.ToDictionary(
        actor => actor,
        actor =>
        {
            var maxCellWidth = allActions
                .Select(action =>
                {
                    if (!firstTimes[actor].TryGetValue(action, out var entry))
                        return "-";
                    return $"{entry.FirstTime:F0}s (x{entry.Count})";
                })
                .DefaultIfEmpty("-")
                .Max(cell => cell.Length);
            return Math.Max(actor.Length + 2, maxCellWidth + 2);
        });

    // Header: action rows with actor columns
    var headerParts = new List<string> { "Action".PadRight(actionColWidth) };
    headerParts.AddRange(actors.Select(actor => actor.PadLeft(actorColWidths[actor])));
    var header = string.Join(" ", headerParts);
    writer.WriteLine(header);
    writer.WriteLine(new string('-', header.Length));

    foreach (var action in allActions)
    {
        var rowParts = new List<string> { action.PadRight(actionColWidth) };
        foreach (var actor in actors)
        {
            var times = firstTimes[actor];
            var cell = times.TryGetValue(action, out var entry)
                ? $"{entry.FirstTime:F0}s (x{entry.Count})"
                : "-";
            rowParts.Add(cell.PadLeft(actorColWidths[actor]));
        }
        writer.WriteLine(string.Join(" ", rowParts));
    }
}

void PrintDecisionSummary(List<TraceEvent> events, JohnnyLike.Engine.DecisionTraceLevel level, TextWriter writer)
{
    var decisionEvents = events.Where(e =>
        e.EventType is "DecisionSelected" or "DecisionNoActionAvailable" or
                       "DecisionOrderingApplied" or "DecisionCandidatesRanked" or
                       "DecisionCandidateRejected" or "DecisionCandidatesGenerated")
        .ToList();

    if (decisionEvents.Count == 0)
        return;

    writer.WriteLine($"\n=== DECISION SUMMARY ({level}) ===");
    foreach (var evt in decisionEvents)
    {
        var actor = evt.ActorId.HasValue ? evt.ActorId.Value.ToString() : "SYSTEM";
        switch (evt.EventType)
        {
            case "DecisionSelected":
                evt.Details.TryGetValue("actionId",        out var ai);
                evt.Details.TryGetValue("finalScore",      out var fs);
                evt.Details.TryGetValue("selectionReason", out var sr);
                evt.Details.TryGetValue("orderingBranch",  out var ob);
                evt.Details.TryGetValue("originalRank",    out var or);
                evt.Details.TryGetValue("attemptRank",     out var ar);
                writer.WriteLine($"[{evt.TimeSeconds:F2}s] {actor} → {ai} " +
                    $"(score={fs:F3}, reason={sr}, branch={ob ?? "n/a"}, rank={or ?? ar})");
                if (evt.Details.TryGetValue("topAlternatives", out var alts))
                    writer.WriteLine($"         alts: {alts}");
                break;
            case "DecisionNoActionAvailable":
                evt.Details.TryGetValue("reason", out var reason);
                writer.WriteLine($"[{evt.TimeSeconds:F2}s] {actor} → NO ACTION ({reason})");
                break;
            case "DecisionOrderingApplied":
                evt.Details.TryGetValue("orderingBranch",   out var branch);
                evt.Details.TryGetValue("decisionPragmatism", out var pragma);
                evt.Details.TryGetValue("temperature",      out var temp);
                writer.WriteLine($"[{evt.TimeSeconds:F2}s] {actor} ordering={branch}, P={pragma}, T={temp ?? "n/a"}");
                break;
            case "DecisionCandidatesRanked":
                evt.Details.TryGetValue("candidateCount", out var cc);
                writer.WriteLine($"[{evt.TimeSeconds:F2}s] {actor} ranked {cc} candidates");
                break;
            case "DecisionCandidateRejected":
                evt.Details.TryGetValue("failedActionId",  out var fa);
                evt.Details.TryGetValue("rejectionReason", out var rr);
                writer.WriteLine($"[{evt.TimeSeconds:F2}s] {actor} rejected {fa} ({rr})");
                break;
        }
    }
}

TeeWriter CreateArtifactWriter(bool saveArtifacts, string domainName, int seed, string? actorName = null, string artifactsSubDir = "")
{
    if (!saveArtifacts)
        return new TeeWriter(Console.Out);

    var artifactsDir = string.IsNullOrEmpty(artifactsSubDir)
        ? "artifacts"
        : Path.Combine("artifacts", artifactsSubDir);
    Directory.CreateDirectory(artifactsDir);
    var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
    var actorPart = string.IsNullOrEmpty(actorName) ? "" : $"-{actorName}";
    var tracePath = Path.Combine(artifactsDir, $"trace-{domainName}{actorPart}-seed{seed}-{timestamp}.txt");
    Console.WriteLine($"Saving trace to {tracePath}");
    StreamWriter fileWriter;
    try
    {
        fileWriter = new StreamWriter(tracePath);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Warning: could not open artifact file '{tracePath}': {ex.Message}. Falling back to console only.");
        return new TeeWriter(Console.Out);
    }
    return new TeeWriter(Console.Out, fileWriter);
}

void RunPressureFuzzer(string[] fuzzerArgs)
{
    var actorFilter        = new List<string>();
    var scenarioFilter     = new List<FuzzerScenarioKind>();
    var outputPath         = "./fuzzer-output.json";
    var topN               = 5;
    var coarseGrid         = true;
    var includeGoldenStates = true;
    DecisionTuningProfile? tuningProfile = null;

    for (int i = 0; i < fuzzerArgs.Length; i++)
    {
        switch (fuzzerArgs[i])
        {
            case "--actors":
            {
                var arg = fuzzerArgs[++i];
                if (arg.Equals("all", StringComparison.OrdinalIgnoreCase))
                    actorFilter.AddRange(Archetypes.All.Keys);
                else
                    actorFilter.AddRange(arg.Split(',', StringSplitOptions.TrimEntries));
                break;
            }
            case "--scenario":
            {
                var arg = fuzzerArgs[++i];
                if (arg.Equals("all", StringComparison.OrdinalIgnoreCase))
                    scenarioFilter.AddRange(Enum.GetValues<FuzzerScenarioKind>());
                else
                    scenarioFilter.Add(Enum.Parse<FuzzerScenarioKind>(arg, ignoreCase: true));
                break;
            }
            case "--output":
                outputPath = fuzzerArgs[++i];
                break;
            case "--top":
                topN = int.Parse(fuzzerArgs[++i]);
                break;
            case "--grid":
                coarseGrid = !fuzzerArgs[++i].Equals("fine", StringComparison.OrdinalIgnoreCase);
                break;
            case "--no-golden":
                includeGoldenStates = false;
                break;
            case "--profile":
                tuningProfile = DecisionTuningProfile.LoadFromFile(fuzzerArgs[++i]);
                break;
        }
    }

    var options = new PressureFuzzerOptions(
        ActorFilter:        actorFilter.Count   > 0 ? actorFilter   : null,
        ScenarioFilter:     scenarioFilter.Count > 0 ? scenarioFilter : null,
        OutputPath:         outputPath,
        TopCandidateCount:  topN,
        CoarseGrid:         coarseGrid,
        IncludeGoldenStates: includeGoldenStates,
        TuningProfile:      tuningProfile);

    var effectiveActors    = options.ActorFilter    ?? Archetypes.All.Keys.OrderBy(k => k).ToList();
    var effectiveScenarios = options.ScenarioFilter ?? Enum.GetValues<FuzzerScenarioKind>().ToList();
    var gridName           = options.CoarseGrid ? "coarse" : "fine";
    var effectiveProfile   = options.TuningProfile ?? DecisionTuningProfile.Default;

    Console.WriteLine("=== PRESSURE FUZZER / DECISION SURFACE EXPLORER ===");
    Console.WriteLine($"Actors:    {string.Join(", ", effectiveActors)}");
    Console.WriteLine($"Scenarios: {string.Join(", ", effectiveScenarios)}");
    Console.WriteLine($"Grid:      {gridName}");
    Console.WriteLine($"Top-N:     {topN}");
    Console.WriteLine($"Golden:    {(includeGoldenStates ? $"yes ({GoldenStates.All.Count} states)" : "no")}");
    Console.WriteLine($"Profile:   {effectiveProfile.ProfileName} [{effectiveProfile.ComputeHash()}]");
    Console.WriteLine($"Output:    {outputPath}");
    Console.WriteLine();

    Console.Write("Sampling...");
    var samples = PressureFuzzerRunner.Run(options);
    Console.WriteLine($" {samples.Count} samples generated.");
    Console.WriteLine($"  (incl. {samples.Count(s => s.GoldenStateLabel != null)} golden-state samples)");

    var dir = Path.GetDirectoryName(outputPath);
    if (!string.IsNullOrEmpty(dir))
        Directory.CreateDirectory(dir);

    PressureFuzzerRunner.WriteJson(samples, outputPath);
    Console.WriteLine($"✓ Written to {outputPath}");

    var summaryPath = PressureFuzzerRunner.DeriveSummaryPath(outputPath);
    var profileMeta = new ProfileMetadata(
        effectiveProfile.ProfileName,
        effectiveProfile.Description,
        effectiveProfile.ComputeHash());
    PressureFuzzerRunner.WriteSummaryJson(samples, summaryPath, profileMeta);
    Console.WriteLine($"✓ Summary written to {summaryPath}");

    // ── Console flag summary ────────────────────────────────────────────────
    Console.WriteLine($"\n--- Plausibility ---");
    Console.WriteLine($"  plausible:    {samples.Count(s => s.Plausibility.IsPlausibleGameplayState)}");
    Console.WriteLine($"  terminal:     {samples.Count(s => s.Plausibility.IsTerminalState)}");
    Console.WriteLine($"  extreme:      {samples.Count(s => s.Plausibility.IsExtremeState)}");

    Console.WriteLine($"\n--- Flag Summary ---");
    Console.WriteLine($"  criticalState:                  {samples.Count(s => s.Flags.CriticalState)}");
    Console.WriteLine($"  foodAvailableButNotChosen:      {samples.Count(s => s.Flags.FoodAvailableButNotChosen)}");
    Console.WriteLine($"  noFoodButNoAcquisition:         {samples.Count(s => s.Flags.NoFoodButNoAcquisition)}");
    Console.WriteLine($"  prepDominatesFood:              {samples.Count(s => s.Flags.PrepDominatesFood)}");
    Console.WriteLine($"  bedLoopRisk:                    {samples.Count(s => s.Flags.BedLoopRisk)}");
    Console.WriteLine($"  directFoodActionPresentButLost: {samples.Count(s => s.Flags.DirectFoodActionPresentButLost)}");
    Console.WriteLine($"  comfortBeatFood:                {samples.Count(s => s.Flags.ComfortBeatFood)}");
    Console.WriteLine($"  safetyBeatFood:                 {samples.Count(s => s.Flags.SafetyBeatFood)}");
    Console.WriteLine($"  personalityCollapseRisk:        {samples.Count(s => s.Flags.PersonalityCollapseRisk)}");

    var anyFlagged = samples.Any(s =>
        s.Flags.FoodAvailableButNotChosen ||
        s.Flags.NoFoodButNoAcquisition    ||
        s.Flags.PrepDominatesFood         ||
        s.Flags.BedLoopRisk               ||
        s.Flags.DirectFoodActionPresentButLost ||
        s.Flags.ComfortBeatFood           ||
        s.Flags.SafetyBeatFood);

    if (anyFlagged)
    {
        Console.WriteLine($"\n⚠ Pathology flags raised. Review {outputPath} for details.");
    }
    else
    {
        Console.WriteLine("\n✓ No pathology flags raised.");
    }
}

void SaveFuzzArtifacts(string profileName, int totalRuns, int successCount, List<FuzzRunResult> failures, string artifactsSubDir = "")
{
    const int MaxRecentEventsInArtifact = 50;
    const int MaxEventScheduleInArtifact = 20;

    var artifactsDir = string.IsNullOrEmpty(artifactsSubDir)
        ? "artifacts"
        : Path.Combine("artifacts", artifactsSubDir);
    Directory.CreateDirectory(artifactsDir);

    // Save summary
    var summary = new
    {
        Profile = profileName,
        Timestamp = DateTime.UtcNow.ToString("o"),
        TotalRuns = totalRuns,
        Passed = successCount,
        Failed = failures.Count,
        FailedSeeds = failures.Select(f => f.Config.Seed).ToList()
    };

    var summaryPath = Path.Combine(artifactsDir, "summary.json");
    File.WriteAllText(summaryPath, JsonSerializer.Serialize(summary, new JsonSerializerOptions 
    { 
        WriteIndented = true 
    }));
    Console.WriteLine($"\n✓ Saved summary to {summaryPath}");

    // Save detailed failure information
    if (failures.Count > 0)
    {
        var failuresDir = Path.Combine(artifactsDir, "failures");
        Directory.CreateDirectory(failuresDir);

        foreach (var failure in failures)
        {
            var failureData = new
            {
                Seed = failure.Config.Seed,
                FailureReason = failure.FailureReason,
                Config = failure.Config,
                Metrics = failure.Metrics,
                Violation = failure.Violation,
                TraceHash = failure.TraceHash,
                RecentEvents = failure.RecentEvents.TakeLast(MaxRecentEventsInArtifact).Select(e => e.ToString()).ToList(),
                EventSchedule = failure.EventSchedule.Events.Take(MaxEventScheduleInArtifact).Select(e => new
                {
                    TimeSeconds = e.TimeSeconds,
                    SignalType = e.Signal.Type,
                    TargetActor = e.Signal.TargetActor?.Value
                }).ToList()
            };

            var failurePath = Path.Combine(failuresDir, $"failure-seed-{failure.Config.Seed}.json");
            File.WriteAllText(failurePath, JsonSerializer.Serialize(failureData, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            }));
            Console.WriteLine($"✓ Saved failure details to {failurePath}");
        }
    }
}

void RunOptimizer(string[] optimizerArgs)
{
    DecisionTuningProfile? baseProfile  = null;
    IReadOnlyList<GoldenStateEntry>? goldenStates = null;
    var outputPath    = "./optimizer-results.json";
    var maxIterations = 20;

    for (int i = 0; i < optimizerArgs.Length; i++)
    {
        switch (optimizerArgs[i])
        {
            case "--base-profile":
                baseProfile = DecisionTuningProfile.LoadFromFile(optimizerArgs[++i]);
                break;
            case "--golden-states":
                goldenStates = GoldenStateLoader.LoadFromFile(optimizerArgs[++i]);
                break;
            case "--output":
                outputPath = optimizerArgs[++i];
                break;
            case "--max-iterations":
                maxIterations = int.Parse(optimizerArgs[++i]);
                break;
        }
    }

    var effectiveProfile    = baseProfile  ?? DecisionTuningProfile.Default;
    var effectiveGolden     = goldenStates ?? GoldenStateLoader.LoadEmbedded();

    Console.WriteLine("=== OPTIMIZER ===");
    Console.WriteLine($"Base profile:   {effectiveProfile.ProfileName} [{effectiveProfile.ComputeHash()}]");
    Console.WriteLine($"Golden states:  {effectiveGolden.Count} entries");
    Console.WriteLine($"Parameters:     {OptimizerRunner.DefaultParameters.Count} tunable");
    Console.WriteLine($"Max iterations: {maxIterations}");
    Console.WriteLine($"Output:         {outputPath}");
    Console.WriteLine();

    Console.Write("Running optimizer...");
    var result = OptimizerRunner.Run(new OptimizerOptions(
        BaseProfile:   effectiveProfile,
        GoldenStates:  effectiveGolden,
        MaxIterations: maxIterations));
    Console.WriteLine(" done.");

    // ── Console summary ─────────────────────────────────────────────────────
    Console.WriteLine();
    Console.WriteLine("--- Objective scores ---");
    Console.WriteLine($"  Base score:  {result.BaseScore:F2}" +
                      $"  ({result.BaseDesiredPassCount}/{effectiveGolden.Count} exact-desired," +
                      $" {result.BaseSatisfiedCount}/{effectiveGolden.Count} satisfied)");
    Console.WriteLine($"  Best score:  {result.BestScore:F2}" +
                      $"  ({result.BestDesiredPassCount}/{effectiveGolden.Count} exact-desired," +
                      $" {result.BestSatisfiedCount}/{effectiveGolden.Count} satisfied)");
    Console.WriteLine($"  Improvement: {result.ScoreImprovement:+0.##;-0.##;0} over {result.IterationsPerformed} iteration(s)");

    if (result.ProfileDiff.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("--- Parameter changes ---");
        foreach (var d in result.ProfileDiff)
            Console.WriteLine($"  {d.ParameterName}: {d.BaselineValue} → {d.CandidateValue} ({d.Delta:+0.######;-0.######})");
    }
    else
    {
        Console.WriteLine();
        Console.WriteLine("No parameter changes found (base profile is already locally optimal).");
    }

    // ── Per-state regression / improvement report ────────────────────────────
    var regressions  = result.BestResults
        .Zip(result.BaseResults, (best, @base) => (best, @base))
        .Where(pair => pair.best.StateSatisfied != pair.@base.StateSatisfied ||
                       pair.best.ForbiddenCategoryTriggered != pair.@base.ForbiddenCategoryTriggered)
        .ToList();

    if (regressions.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("--- Per-state changes ---");
        foreach (var (best, @base) in regressions)
        {
            var direction = best.Score > @base.Score ? "▲ improved" : "▼ regressed";
            var rankInfo  = best.BestDesiredCategoryRank.HasValue
                ? $"  rank={best.BestDesiredCategoryRank}"
                : "";
            Console.WriteLine($"  {direction}: [{best.Label ?? best.SampleKey}]  " +
                              $"base={@base.ActualTopCategory}  best={best.ActualTopCategory}  " +
                              $"desired={best.DesiredTopCategory}{rankInfo}");
        }
    }

    // ── Failed golden states on best profile ─────────────────────────────────
    var stillFailing = result.BestResults
        .Where(r => !r.StateSatisfied)
        .OrderBy(r => r.Score)
        .ToList();

    if (stillFailing.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("--- Remaining failures (not satisfied) ---");
        foreach (var r in stillFailing.Take(10))
        {
            var forbidden = r.ForbiddenCategoryTriggered ? "  ⚠ FORBIDDEN" : "";
            var delta     = r.DesiredCategoryVsWinnerDelta.HasValue
                ? $"  delta={r.DesiredCategoryVsWinnerDelta:F3}"
                : "";
            var rank      = r.BestDesiredCategoryRank.HasValue
                ? $"  rank={r.BestDesiredCategoryRank}"
                : "";
            Console.WriteLine($"  [{r.Label ?? r.SampleKey}]  " +
                              $"actual={r.ActualTopCategory}  desired={r.DesiredTopCategory}" +
                              $"{rank}{delta}{forbidden}");
        }
        if (stillFailing.Count > 10)
            Console.WriteLine($"  ... and {stillFailing.Count - 10} more.");
    }

    // ── Write JSON output ────────────────────────────────────────────────────
    var dir = Path.GetDirectoryName(outputPath);
    if (!string.IsNullOrEmpty(dir))
        Directory.CreateDirectory(dir);

    var jsonOpts = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    File.WriteAllText(outputPath, JsonSerializer.Serialize(result, jsonOpts));
    Console.WriteLine();
    Console.WriteLine($"✓ Results written to {outputPath}");
}

// ─── evaluate-golden subcommand ──────────────────────────────────────────────

void RunEvaluateGolden(string[] evalArgs)
{
    var profileArg = "default";
    var outputDir  = "artifacts/baseline";

    for (int i = 0; i < evalArgs.Length; i++)
    {
        switch (evalArgs[i])
        {
            case "--profile": profileArg = evalArgs[++i]; break;
            case "--output":  outputDir  = evalArgs[++i]; break;
        }
    }

    var profile = profileArg.Equals("default", StringComparison.OrdinalIgnoreCase)
        ? DecisionTuningProfile.Default
        : DecisionTuningProfile.LoadFromFile(profileArg);

    var goldenStates = GoldenStateLoader.LoadEmbedded();

    Console.WriteLine("=== EVALUATE GOLDEN ===");
    Console.WriteLine($"Profile:      {profile.ProfileName} [{profile.ComputeHash()}]");
    Console.WriteLine($"Golden states:{goldenStates.Count}");
    Console.WriteLine($"Output:       {outputDir}");
    Console.WriteLine();

    Console.Write("Evaluating...");
    var results = OptimizerRunner.EvaluateProfile(profile, goldenStates);
    Console.WriteLine(" done.");

    Directory.CreateDirectory(outputDir);
    var jsonOpts = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    // ── Partition results by set type ────────────────────────────────────────
    var setTypeByKey = goldenStates.ToDictionary(gs => gs.SampleKey, gs => gs.SetType ?? GoldenSetType.Training);

    static object BuildSplitMetrics(
        IEnumerable<(GoldenStateResult result, GoldenSetType setType)> group,
        GoldenSetType target)
    {
        var subset = group.Where(x => x.setType == target).Select(x => x.result).ToList();
        var regressions = target == GoldenSetType.Sacred
            ? subset.Where(r => !r.StateSatisfied || r.ForbiddenCategoryTriggered)
                    .Select(r => new { sampleKey = r.SampleKey, label = r.Label, actualTopCategory = r.ActualTopCategory, desiredTopCategory = r.DesiredTopCategory, violatedForbidden = r.ForbiddenCategoryTriggered })
                    .ToList<object>()
            : null;

        return target == GoldenSetType.Sacred
            ? (object)new
            {
                exact_pass_count   = subset.Count(r => r.DesiredTopCategoryMet),
                satisfied_pass_count = subset.Count(r => r.StateSatisfied),
                forbidden_count    = subset.Count(r => r.ForbiddenCategoryTriggered),
                regressions        = regressions!
            }
            : new
            {
                exact_pass_count   = subset.Count(r => r.DesiredTopCategoryMet),
                satisfied_pass_count = subset.Count(r => r.StateSatisfied),
                forbidden_count    = subset.Count(r => r.ForbiddenCategoryTriggered)
            };
    }

    var paired = results.Select(r => (result: r, setType: setTypeByKey.GetValueOrDefault(r.SampleKey, GoldenSetType.Training))).ToList();

    var evalSummary = new
    {
        profileName  = profile.ProfileName,
        profileHash  = profile.ComputeHash(),
        totalStates  = goldenStates.Count,
        completedAt  = DateTime.UtcNow.ToString("o"),
        training     = BuildSplitMetrics(paired, GoldenSetType.Training),
        holdout      = BuildSplitMetrics(paired, GoldenSetType.Holdout),
        sacred       = BuildSplitMetrics(paired, GoldenSetType.Sacred)
    };

    File.WriteAllText(
        Path.Combine(outputDir, "eval-summary.json"),
        JsonSerializer.Serialize(evalSummary, jsonOpts));
    Console.WriteLine($"✓ eval-summary.json");

    // ── eval-results.json ────────────────────────────────────────────────────
    var evalResults = paired.Select(p => new
    {
        sampleKey          = p.result.SampleKey,
        label              = p.result.Label,
        setType            = p.setType.ToString(),
        desiredTopCategory = p.result.DesiredTopCategory,
        actualTopCategory  = p.result.ActualTopCategory,
        actualTopCategories = p.result.ActualTopCategories,
        desiredRank        = p.result.BestDesiredCategoryRank,
        deltaFromTop       = p.result.DesiredCategoryVsWinnerDelta,
        isExactMatch       = p.result.DesiredTopCategoryMet,
        isSatisfied        = p.result.StateSatisfied,
        violatedForbidden  = p.result.ForbiddenCategoryTriggered,
        score              = p.result.Score
    }).ToList();

    File.WriteAllText(
        Path.Combine(outputDir, "eval-results.json"),
        JsonSerializer.Serialize(evalResults, jsonOpts));
    Console.WriteLine($"✓ eval-results.json");

    // ── failures.json ────────────────────────────────────────────────────────
    var failures = paired
        .Where(p => !p.result.StateSatisfied || p.result.ForbiddenCategoryTriggered)
        .Select(p => new
        {
            sampleKey          = p.result.SampleKey,
            label              = p.result.Label,
            setType            = p.setType.ToString(),
            desiredTopCategory = p.result.DesiredTopCategory,
            actualTopCategory  = p.result.ActualTopCategory,
            topCompetingCategories = p.result.ActualTopCategories,
            desiredRank        = p.result.BestDesiredCategoryRank,
            deltaFromTop       = p.result.DesiredCategoryVsWinnerDelta,
            isExactMatch       = p.result.DesiredTopCategoryMet,
            isSatisfied        = p.result.StateSatisfied,
            violatedForbidden  = p.result.ForbiddenCategoryTriggered,
            score              = p.result.Score
        }).ToList();

    File.WriteAllText(
        Path.Combine(outputDir, "failures.json"),
        JsonSerializer.Serialize(failures, jsonOpts));
    Console.WriteLine($"✓ failures.json  ({failures.Count} failed states)");

    Console.WriteLine();
    Console.WriteLine($"✓ All artifacts written to {outputDir}");
}

// ─── optimize-golden subcommand ──────────────────────────────────────────────

void RunOptimizeGolden(string[] optArgs)
{
    var inputProfileArg = "default";
    var outputDir       = "artifacts/optimizer";
    var maxIterations   = 20;

    for (int i = 0; i < optArgs.Length; i++)
    {
        switch (optArgs[i])
        {
            case "--input-profile":  inputProfileArg = optArgs[++i]; break;
            case "--output":         outputDir        = optArgs[++i]; break;
            case "--max-iterations": maxIterations    = int.Parse(optArgs[++i]); break;
        }
    }

    var baseProfile = inputProfileArg.Equals("default", StringComparison.OrdinalIgnoreCase)
        ? DecisionTuningProfile.Default
        : DecisionTuningProfile.LoadFromFile(inputProfileArg);

    var goldenStates = GoldenStateLoader.LoadEmbedded();

    Console.WriteLine("=== OPTIMIZE GOLDEN ===");
    Console.WriteLine($"Base profile:   {baseProfile.ProfileName} [{baseProfile.ComputeHash()}]");
    Console.WriteLine($"Golden states:  {goldenStates.Count}");
    Console.WriteLine($"Max iterations: {maxIterations}");
    Console.WriteLine($"Output:         {outputDir}");
    Console.WriteLine();

    Console.Write("Running optimizer...");
    var result = OptimizerRunner.Run(new OptimizerOptions(
        BaseProfile:   baseProfile,
        GoldenStates:  goldenStates,
        MaxIterations: maxIterations));
    Console.WriteLine(" done.");

    Directory.CreateDirectory(outputDir);
    var jsonOpts = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    // ── optimizer-result.json ────────────────────────────────────────────────
    File.WriteAllText(
        Path.Combine(outputDir, "optimizer-result.json"),
        JsonSerializer.Serialize(result, jsonOpts));
    Console.WriteLine($"✓ optimizer-result.json");

    // ── optimized-profile.json ───────────────────────────────────────────────
    File.WriteAllText(
        Path.Combine(outputDir, "optimized-profile.json"),
        result.BestProfileJson);
    Console.WriteLine($"✓ optimized-profile.json");

    // ── optimizer-diff.json ──────────────────────────────────────────────────
    var diff = new
    {
        baseProfileName  = result.BaseProfileName,
        baseProfileHash  = result.BaseProfileHash,
        bestProfileName  = result.BestProfileName,
        bestProfileHash  = result.BestProfileHash,
        iterationsPerformed = result.IterationsPerformed,
        changes          = result.ProfileDiff.Select(d => new
        {
            parameter     = d.ParameterName,
            baselineValue = d.BaselineValue,
            optimizedValue = d.CandidateValue,
            delta         = d.Delta
        }).ToList()
    };
    File.WriteAllText(
        Path.Combine(outputDir, "optimizer-diff.json"),
        JsonSerializer.Serialize(diff, jsonOpts));
    Console.WriteLine($"✓ optimizer-diff.json");

    // ── optimizer-comparison.json ────────────────────────────────────────────
    var setTypeByKey = goldenStates.ToDictionary(gs => gs.SampleKey, gs => gs.SetType ?? GoldenSetType.Training);

    static (int exactPass, int satisfied, int forbidden) SplitMetrics(
        IEnumerable<GoldenStateResult> results,
        IReadOnlyDictionary<string, GoldenSetType> setTypeByKey,
        GoldenSetType target)
    {
        var subset = results.Where(r => setTypeByKey.GetValueOrDefault(r.SampleKey, GoldenSetType.Training) == target).ToList();
        return (
            subset.Count(r => r.DesiredTopCategoryMet),
            subset.Count(r => r.StateSatisfied),
            subset.Count(r => r.ForbiddenCategoryTriggered));
    }

    var (bTrainExact, bTrainSatisfied, bTrainForbidden) = SplitMetrics(result.BaseResults, setTypeByKey, GoldenSetType.Training);
    var (bHoldExact, bHoldSatisfied, bHoldForbidden)    = SplitMetrics(result.BaseResults, setTypeByKey, GoldenSetType.Holdout);
    var (bSacredExact, bSacredSatisfied, bSacredForbidden) = SplitMetrics(result.BaseResults, setTypeByKey, GoldenSetType.Sacred);

    var (oTrainExact, oTrainSatisfied, oTrainForbidden) = SplitMetrics(result.BestResults, setTypeByKey, GoldenSetType.Training);
    var (oHoldExact, oHoldSatisfied, oHoldForbidden)    = SplitMetrics(result.BestResults, setTypeByKey, GoldenSetType.Holdout);
    var (oSacredExact, oSacredSatisfied, oSacredForbidden) = SplitMetrics(result.BestResults, setTypeByKey, GoldenSetType.Sacred);

    var sacredRegressions = result.BestResults
        .Where(r => setTypeByKey.GetValueOrDefault(r.SampleKey, GoldenSetType.Training) == GoldenSetType.Sacred
                    && (!r.StateSatisfied || r.ForbiddenCategoryTriggered))
        .Select(r => new { sampleKey = r.SampleKey, label = r.Label, actualTopCategory = r.ActualTopCategory, desiredTopCategory = r.DesiredTopCategory })
        .ToList();

    var comparison = new
    {
        completedAt = result.CompletedAt,
        baseline = new
        {
            profileName  = result.BaseProfileName,
            profileHash  = result.BaseProfileHash,
            training     = new { exact_pass_count = bTrainExact, satisfied_pass_count = bTrainSatisfied, forbidden_count = bTrainForbidden },
            holdout      = new { exact_pass_count = bHoldExact, satisfied_pass_count = bHoldSatisfied, forbidden_count = bHoldForbidden },
            sacred       = new { exact_pass_count = bSacredExact, satisfied_pass_count = bSacredSatisfied, forbidden_count = bSacredForbidden }
        },
        optimized = new
        {
            profileName  = result.BestProfileName,
            profileHash  = result.BestProfileHash,
            training     = new { exact_pass_count = oTrainExact, satisfied_pass_count = oTrainSatisfied, forbidden_count = oTrainForbidden },
            holdout      = new { exact_pass_count = oHoldExact, satisfied_pass_count = oHoldSatisfied, forbidden_count = oHoldForbidden },
            sacred       = new { exact_pass_count = oSacredExact, satisfied_pass_count = oSacredSatisfied, forbidden_count = oSacredForbidden }
        },
        improvement = new
        {
            score_delta                 = Math.Round(result.ScoreImprovement, 4),
            training_exact_pass_delta   = oTrainExact - bTrainExact,
            training_satisfied_delta    = oTrainSatisfied - bTrainSatisfied,
            holdout_exact_pass_delta    = oHoldExact - bHoldExact,
            holdout_satisfied_delta     = oHoldSatisfied - bHoldSatisfied,
            sacred_exact_pass_delta     = oSacredExact - bSacredExact,
            sacred_satisfied_delta      = oSacredSatisfied - bSacredSatisfied,
            sacred_regression_flag      = sacredRegressions.Count > 0,
            sacred_regressions          = sacredRegressions
        }
    };

    File.WriteAllText(
        Path.Combine(outputDir, "optimizer-comparison.json"),
        JsonSerializer.Serialize(comparison, jsonOpts));
    Console.WriteLine($"✓ optimizer-comparison.json  (sacred_regression_flag={comparison.improvement.sacred_regression_flag})");

    Console.WriteLine();
    Console.WriteLine($"  Base  score: {result.BaseScore:F2}  ({result.BaseDesiredPassCount}/{goldenStates.Count} exact, {result.BaseSatisfiedCount}/{goldenStates.Count} satisfied)");
    Console.WriteLine($"  Best  score: {result.BestScore:F2}  ({result.BestDesiredPassCount}/{goldenStates.Count} exact, {result.BestSatisfiedCount}/{goldenStates.Count} satisfied)");
    Console.WriteLine($"  Improvement: {result.ScoreImprovement:+0.##;-0.##;0} over {result.IterationsPerformed} iteration(s)");
    Console.WriteLine();
    Console.WriteLine($"✓ All artifacts written to {outputDir}");
}

// ─── run-fuzzer subcommand ────────────────────────────────────────────────────

void RunFuzzerComparison(string[] fuzzerArgs)
{
    var baselineProfileArg  = "default";
    var optimizedProfileArg = (string?)null;
    var outputDir           = "artifacts/fuzzer";

    for (int i = 0; i < fuzzerArgs.Length; i++)
    {
        switch (fuzzerArgs[i])
        {
            case "--baseline-profile":  baselineProfileArg  = fuzzerArgs[++i]; break;
            case "--optimized-profile": optimizedProfileArg = fuzzerArgs[++i]; break;
            case "--output":            outputDir           = fuzzerArgs[++i]; break;
        }
    }

    var baselineProfile = baselineProfileArg.Equals("default", StringComparison.OrdinalIgnoreCase)
        ? DecisionTuningProfile.Default
        : DecisionTuningProfile.LoadFromFile(baselineProfileArg);

    var optimizedProfile = optimizedProfileArg != null
        ? DecisionTuningProfile.LoadFromFile(optimizedProfileArg)
        : (DecisionTuningProfile?)null;

    Console.WriteLine("=== RUN FUZZER (COMPARISON) ===");
    Console.WriteLine($"Baseline profile:  {baselineProfile.ProfileName} [{baselineProfile.ComputeHash()}]");
    Console.WriteLine($"Optimized profile: {(optimizedProfile != null ? $"{optimizedProfile.ProfileName} [{optimizedProfile.ComputeHash()}]" : "none")}");
    Console.WriteLine($"Output:            {outputDir}");
    Console.WriteLine();

    Directory.CreateDirectory(outputDir);
    var jsonOpts = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    // ── Baseline run ─────────────────────────────────────────────────────────
    Console.Write("Running baseline fuzzer...");
    var baselineOptions = new PressureFuzzerOptions(TuningProfile: baselineProfile, CoarseGrid: true);
    var baselineSamples = PressureFuzzerRunner.Run(baselineOptions);
    Console.WriteLine($" {baselineSamples.Count} samples.");

    var baselineProfileMeta = new ProfileMetadata(
        baselineProfile.ProfileName,
        baselineProfile.Description,
        baselineProfile.ComputeHash());

    var baselineSummaryPath = Path.Combine(outputDir, "baseline-summary.json");
    PressureFuzzerRunner.WriteSummaryJson(baselineSamples, baselineSummaryPath, baselineProfileMeta);
    Console.WriteLine($"✓ baseline-summary.json");

    PressureFuzzerRunner.WriteJson(baselineSamples, Path.Combine(outputDir, "baseline-results.json"));
    Console.WriteLine($"✓ baseline-results.json");

    // ── Optimized run (if profile provided) ──────────────────────────────────
    List<PressureSample>? optimizedSamples = null;
    if (optimizedProfile != null)
    {
        Console.Write("Running optimized fuzzer...");
        var optimizedOptions = new PressureFuzzerOptions(TuningProfile: optimizedProfile, CoarseGrid: true);
        optimizedSamples = PressureFuzzerRunner.Run(optimizedOptions);
        Console.WriteLine($" {optimizedSamples.Count} samples.");

        var optimizedProfileMeta = new ProfileMetadata(
            optimizedProfile.ProfileName,
            optimizedProfile.Description,
            optimizedProfile.ComputeHash());

        var optimizedSummaryPath = Path.Combine(outputDir, "optimized-summary.json");
        PressureFuzzerRunner.WriteSummaryJson(optimizedSamples, optimizedSummaryPath, optimizedProfileMeta);
        Console.WriteLine($"✓ optimized-summary.json");

        PressureFuzzerRunner.WriteJson(optimizedSamples, Path.Combine(outputDir, "optimized-results.json"));
        Console.WriteLine($"✓ optimized-results.json");
    }

    // ── comparison-summary.json ───────────────────────────────────────────────
    static object BuildFuzzerStarvationMetrics(List<PressureSample> samples)
    {
        // "Starvation" = samples where satiety is at or below 30 (FoodPressureThreshold)
        var starvationSamples = samples.Where(s => s.State.Satiety <= 30.0).ToList();
        var total = starvationSamples.Count;

        double toPercentage(int count) => total > 0 ? Math.Round(count * 100.0 / total, 1) : 0.0;

        var foodConsumptionLost = starvationSamples.Count(s => s.Flags.DirectFoodActionPresentButLost || s.Flags.FoodAvailableButNotChosen);
        var comfortDominated    = starvationSamples.Count(s => s.Flags.ComfortBeatFood);
        var prepDominated       = starvationSamples.Count(s => s.Flags.PrepDominatesFood);

        return new
        {
            starvation_sample_count             = total,
            food_consumption_lost_count         = foodConsumptionLost,
            food_consumption_lost_pct           = toPercentage(foodConsumptionLost),
            comfort_dominated_count             = comfortDominated,
            comfort_dominated_pct               = toPercentage(comfortDominated),
            prep_dominated_count                = prepDominated,
            prep_dominated_pct                  = toPercentage(prepDominated)
        };
    }

    var baselineMetrics   = BuildFuzzerStarvationMetrics(baselineSamples);
    var optimizedMetrics  = optimizedSamples != null
        ? BuildFuzzerStarvationMetrics(optimizedSamples)
        : (object?)null;

    var comparison = new
    {
        completedAt              = DateTime.UtcNow.ToString("o"),
        total_baseline_samples   = baselineSamples.Count,
        total_optimized_samples  = optimizedSamples?.Count,
        baseline                 = baselineMetrics,
        optimized                = optimizedMetrics
    };

    File.WriteAllText(
        Path.Combine(outputDir, "comparison-summary.json"),
        JsonSerializer.Serialize(comparison, jsonOpts));
    Console.WriteLine($"✓ comparison-summary.json");

    Console.WriteLine();
    Console.WriteLine($"✓ All artifacts written to {outputDir}");
}
