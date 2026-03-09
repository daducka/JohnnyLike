using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.SimRunner;
using System.Text.Json;

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
    Console.WriteLine("  --save-artifacts          Save trace logs to artifacts/ directory");
    Console.WriteLine("\nFuzz Testing:");
    Console.WriteLine("  --fuzz              Run fuzz testing mode");
    Console.WriteLine("  --runs <number>     Number of fuzz runs (default: 1)");
    Console.WriteLine("  --config <path>     Load fuzz config from JSON file");
    Console.WriteLine("  --profile <name>    Use predefined profile: smoke, extended, nightly");
    Console.WriteLine("  --verbose           Verbose output for fuzz runs");
    Console.WriteLine("  --save-artifacts    Save test artifacts to disk");
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
var decisionTraceLevel = JohnnyLike.Engine.DecisionTraceLevel.None;

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
            break;
    }
}

if (fuzzMode)
{
    RunFuzz(seed, fuzzRuns, fuzzConfigPath, fuzzProfile, verbose, domainName, saveArtifacts);
}
else if (!string.IsNullOrEmpty(scenarioPath))
{
    RunScenario(scenarioPath, outputTrace, domainName, decisionTraceLevel, saveArtifacts);
}
else
{
    RunDefault(seed, duration, outputTrace, domainName, decisionTraceLevel, saveArtifacts);
}

IDomainPack CreateDomainPack(string domainName)
{
    return domainName switch
    {
        "island" => new IslandDomainPack(),
        _ => throw new ArgumentException($"Unknown domain: {domainName}. Valid domains: island")
    };
}

void RunScenario(string path, bool trace, string domainName, JohnnyLike.Engine.DecisionTraceLevel decisionLevel, bool saveArtifacts = false)
{
    var scenario = ScenarioLoader.LoadFromFile(path);
    using var writer = CreateArtifactWriter(saveArtifacts, domainName, scenario.Seed);

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
    
    while (elapsed < scenario.DurationSeconds)
    {
        executor.Update(timeStep);
        elapsed += timeStep;
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

void RunDefault(int seed, double duration, bool trace, string domainName, JohnnyLike.Engine.DecisionTraceLevel decisionLevel, bool saveArtifacts = false)
{
    using var writer = CreateArtifactWriter(saveArtifacts, domainName, seed);

    writer.WriteLine($"Running default {domainName} simulation");
    writer.WriteLine($"Seed: {seed}, Duration: {duration}s");
    
    var domainPack = CreateDomainPack(domainName);
    var traceSink = new InMemoryTraceSink();
    var traceOptions = new JohnnyLike.Engine.DecisionTraceOptions(decisionLevel);
    var engine = new JohnnyLike.Engine.Engine(domainPack, seed, traceSink, traceOptions);
    
    if (domainName == "island")
    {
        engine.AddActor(new ActorId("Johnny"), new Dictionary<string, object>
        {
            ["STR"] = 12,
            ["DEX"] = 14,
            ["CON"] = 13,
            ["INT"] = 10,
            ["WIS"] = 11,
            ["CHA"] = 15,
            ["satiety"] = 70.0,
            ["energy"] = 80.0,
            ["morale"] = 60.0
        });
    }
    
    var executor = new FakeExecutor(engine);
    var timeStep = 0.5;
    var elapsed = 0.0;
    
    while (elapsed < duration)
    {
        executor.Update(timeStep);
        elapsed += timeStep;
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

void RunFuzz(int baseSeed, int runs, string configPath, string profileName, bool verbose, string domainName, bool saveArtifacts)
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
        SaveFuzzArtifacts(profileName, runs, successCount, failures);
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

TeeWriter CreateArtifactWriter(bool saveArtifacts, string domainName, int seed)
{
    if (!saveArtifacts)
        return new TeeWriter(Console.Out);

    var artifactsDir = "artifacts";
    Directory.CreateDirectory(artifactsDir);
    var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
    var tracePath = Path.Combine(artifactsDir, $"trace-{domainName}-seed{seed}-{timestamp}.txt");
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

void SaveFuzzArtifacts(string profileName, int totalRuns, int successCount, List<FuzzRunResult> failures)
{
    const int MaxRecentEventsInArtifact = 50;
    const int MaxEventScheduleInArtifact = 20;

    var artifactsDir = "artifacts";
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
