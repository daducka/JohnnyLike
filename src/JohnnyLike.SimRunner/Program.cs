using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Office;
using JohnnyLike.SimRunner;
using System.Text.Json;

if (args.Length == 0)
{
    Console.WriteLine("JohnnyLike SimRunner");
    Console.WriteLine("Usage: SimRunner [options]");
    Console.WriteLine("  --scenario <path>   Load and run scenario from JSON file");
    Console.WriteLine("  --seed <number>     Random seed (default: 42)");
    Console.WriteLine("  --duration <sec>    Simulation duration in seconds");
    Console.WriteLine("  --trace             Output detailed trace");
    return;
}

var scenarioPath = "";
var seed = 42;
var duration = 60.0;
var outputTrace = false;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--scenario":
            scenarioPath = args[++i];
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
    }
}

if (!string.IsNullOrEmpty(scenarioPath))
{
    RunScenario(scenarioPath, outputTrace);
}
else
{
    RunDefault(seed, duration, outputTrace);
}

void RunScenario(string path, bool trace)
{
    Console.WriteLine($"Loading scenario from: {path}");
    var scenario = ScenarioLoader.LoadFromFile(path);
    
    Console.WriteLine($"Running scenario: {scenario.Name}");
    Console.WriteLine($"Seed: {scenario.Seed}, Duration: {scenario.DurationSeconds}s");
    
    var domainPack = new OfficeDomainPack();
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

void RunDefault(int seed, double duration, bool trace)
{
    Console.WriteLine("Running default office simulation");
    Console.WriteLine($"Seed: {seed}, Duration: {duration}s");
    
    var domainPack = new OfficeDomainPack();
    var traceSink = new InMemoryTraceSink();
    var engine = new JohnnyLike.Engine.Engine(domainPack, seed, traceSink);
    
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
