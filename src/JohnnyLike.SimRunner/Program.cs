using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Office;
using JohnnyLike.Domain.Island;
using JohnnyLike.SimRunner;
using System.Text.Json;

if (args.Length == 0)
{
    Console.WriteLine("JohnnyLike SimRunner");
    Console.WriteLine("Usage: SimRunner [options]");
    Console.WriteLine("  --scenario <path>   Load and run scenario from JSON file");
    Console.WriteLine("  --domain <name>     Domain to use: office, island (default: office)");
    Console.WriteLine("  --seed <number>     Random seed (default: 42)");
    Console.WriteLine("  --duration <sec>    Simulation duration in seconds");
    Console.WriteLine("  --trace             Output detailed trace");
    Console.WriteLine("\nFuzz Testing:");
    Console.WriteLine("  --fuzz              Run fuzz testing mode");
    Console.WriteLine("  --runs <number>     Number of fuzz runs (default: 1)");
    Console.WriteLine("  --config <path>     Load fuzz config from JSON file");
    Console.WriteLine("  --profile <name>    Use predefined profile: smoke, extended, nightly");
    Console.WriteLine("  --verbose           Verbose output for fuzz runs");
    return;
}

var scenarioPath = "";
var domainName = "office";
var seed = 42;
var duration = 60.0;
var outputTrace = false;
var fuzzMode = false;
var fuzzRuns = 1;
var fuzzConfigPath = "";
var fuzzProfile = "";
var verbose = false;

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
    }
}

if (fuzzMode)
{
    RunFuzz(seed, fuzzRuns, fuzzConfigPath, fuzzProfile, verbose, domainName);
}
else if (!string.IsNullOrEmpty(scenarioPath))
{
    RunScenario(scenarioPath, outputTrace, domainName);
}
else
{
    RunDefault(seed, duration, outputTrace, domainName);
}

IDomainPack CreateDomainPack(string domainName)
{
    return domainName switch
    {
        "office" => new OfficeDomainPack(),
        "island" => new IslandDomainPack(),
        _ => throw new ArgumentException($"Unknown domain: {domainName}. Valid domains: office, island")
    };
}

void RunScenario(string path, bool trace, string domainName)
{
    Console.WriteLine($"Loading scenario from: {path}");
    var scenario = ScenarioLoader.LoadFromFile(path);
    
    Console.WriteLine($"Running scenario: {scenario.Name}");
    Console.WriteLine($"Seed: {scenario.Seed}, Duration: {scenario.DurationSeconds}s");
    Console.WriteLine($"Domain: {domainName}");
    
    var domainPack = CreateDomainPack(domainName);
    var traceSink = new InMemoryTraceSink();
    var engine = new JohnnyLike.Engine.Engine(domainPack, scenario.Seed, traceSink);
    
    // Add actors
    foreach (var actorDef in scenario.Actors)
    {
        engine.AddActor(new ActorId(actorDef.ActorId), actorDef.InitialState);
        Console.WriteLine($"Added actor: {actorDef.ActorId}");
    }
    
    // Enqueue signals
    foreach (var signal in scenario.Signals)
    {
        engine.EnqueueSignal(new Signal(
            signal.Type,
            signal.AtTime,
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
    
    Console.WriteLine($"\nSimulation completed at t={engine.CurrentTime:F2}s");
    Console.WriteLine($"Total events: {traceSink.GetEvents().Count}");
    
    if (trace)
    {
        Console.WriteLine("\n=== TRACE ===");
        foreach (var evt in traceSink.GetEvents())
        {
            Console.WriteLine(evt);
        }
    }
    
    var hash = JohnnyLike.Engine.TraceHelper.ComputeTraceHash(traceSink.GetEvents());
    Console.WriteLine($"\nTrace hash: {hash}");
}

void RunDefault(int seed, double duration, bool trace, string domainName)
{
    Console.WriteLine($"Running default {domainName} simulation");
    Console.WriteLine($"Seed: {seed}, Duration: {duration}s");
    
    var domainPack = CreateDomainPack(domainName);
    var traceSink = new InMemoryTraceSink();
    var engine = new JohnnyLike.Engine.Engine(domainPack, seed, traceSink);
    
    if (domainName == "office")
    {
        engine.AddActor(new ActorId("Jim"), new Dictionary<string, object>
        {
            ["hunger"] = 20.0,
            ["energy"] = 80.0
        });
        
        engine.AddActor(new ActorId("Pam"), new Dictionary<string, object>
        {
            ["hunger"] = 40.0,
            ["energy"] = 90.0
        });
    }
    else if (domainName == "island")
    {
        engine.AddActor(new ActorId("Johnny"), new Dictionary<string, object>
        {
            ["STR"] = 12,
            ["DEX"] = 14,
            ["CON"] = 13,
            ["INT"] = 10,
            ["WIS"] = 11,
            ["CHA"] = 15,
            ["hunger"] = 30.0,
            ["energy"] = 80.0,
            ["morale"] = 60.0,
            ["boredom"] = 20.0
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
    
    Console.WriteLine($"\nSimulation completed at t={engine.CurrentTime:F2}s");
    Console.WriteLine($"Total events: {traceSink.GetEvents().Count}");
    
    if (trace)
    {
        Console.WriteLine("\n=== TRACE ===");
        foreach (var evt in traceSink.GetEvents())
        {
            Console.WriteLine(evt);
        }
    }
    
    var hash = JohnnyLike.Engine.TraceHelper.ComputeTraceHash(traceSink.GetEvents());
    Console.WriteLine($"\nTrace hash: {hash}");
}

void RunFuzz(int baseSeed, int runs, string configPath, string profileName, bool verbose, string domainName)
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
