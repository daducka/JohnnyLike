using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Office;
using JohnnyLike.Engine;
using System.Text.Json;

namespace JohnnyLike.SimRunner;

public class FuzzRunResult
{
    public bool Success { get; set; }
    public string? FailureReason { get; set; }
    public FuzzConfig Config { get; set; } = null!;
    public EventSchedule EventSchedule { get; set; } = null!;
    public FuzzMetrics Metrics { get; set; } = null!;
    public List<TraceEvent> RecentEvents { get; set; } = new();
    public InvariantViolation? Violation { get; set; }
    public string TraceHash { get; set; } = "";
}

public class FuzzRunner
{
    public static FuzzRunResult Run(FuzzConfig config)
    {
        var result = new FuzzRunResult
        {
            Config = config,
            Success = true
        };

        try
        {
            // Create deterministic RNG from seed
            var rng = new Random(config.Seed);
            
            // Create actor IDs
            var actorIds = new List<ActorId>();
            for (int i = 0; i < config.NumActors; i++)
            {
                actorIds.Add(new ActorId($"Actor{i}"));
            }

            // Generate deterministic event schedule
            var eventSchedule = EventSchedule.Generate(config, new Random(config.Seed), actorIds);
            result.EventSchedule = eventSchedule;

            // Create engine with domain pack
            var domainPack = new OfficeDomainPack();
            var traceSink = new InMemoryTraceSink();
            var engine = new JohnnyLike.Engine.Engine(domainPack, config.Seed, traceSink);

            // Add actors
            foreach (var actorId in actorIds)
            {
                var initialHunger = rng.Next(0, 50);
                var initialEnergy = rng.Next(60, 100);
                var initialSocial = rng.Next(30, 70);
                
                engine.AddActor(actorId, new Dictionary<string, object>
                {
                    ["hunger"] = (double)initialHunger,
                    ["energy"] = (double)initialEnergy,
                    ["social"] = (double)initialSocial
                });
            }

            // Enqueue all signals from schedule
            foreach (var scheduledSignal in eventSchedule.Events)
            {
                engine.EnqueueSignal(scheduledSignal.Signal);
            }

            // Create metrics collector
            var metricsCollector = new MetricsCollector(config);

            // Create fuzzable executor
            var executor = new FuzzableFakeExecutor(engine, config, new Random(config.Seed + 1));

            // Get director for invariant checking (we need to expose this via Engine)
            // For now, we'll track violations through metrics

            // Run simulation
            var currentTime = 0.0;
            var checkInterval = 1.0; // Check invariants every second
            var nextCheckTime = checkInterval;

            while (currentTime < config.SimulatedDurationSeconds)
            {
                // Update executor
                executor.Update(config.DtSeconds);
                currentTime += config.DtSeconds;

                // Collect trace events
                var newEvents = traceSink.GetEvents();
                if (newEvents.Count > 0)
                {
                    var lastProcessed = metricsCollector.Metrics.TotalActions + 
                                       metricsCollector.Metrics.SignalsProcessed;
                    
                    foreach (var evt in newEvents.Skip(lastProcessed))
                    {
                        metricsCollector.RecordEvent(evt);
                    }
                }

                // Periodic invariant check
                if (currentTime >= nextCheckTime)
                {
                    // Note: We can't access director directly, so we'll do limited checks
                    // In a real implementation, we'd expose GetDirector() or GetReservations() on Engine
                    
                    // Check for starvation
                    foreach (var actorId in actorIds)
                    {
                        var actorKey = actorId.Value;
                        if (!metricsCollector.Metrics.ActorLastCompletionTime.ContainsKey(actorKey) && 
                            currentTime > config.StarvationThresholdSeconds)
                        {
                            result.Success = false;
                            result.FailureReason = $"Actor {actorKey} starvation: no completions after {currentTime:F1}s";
                            result.Violation = new InvariantViolation
                            {
                                Reason = "Actor starvation detected",
                                TimeSeconds = currentTime,
                                Details = new Dictionary<string, object>
                                {
                                    ["actorId"] = actorKey,
                                    ["currentTime"] = currentTime
                                }
                            };
                            break;
                        }
                        else if (metricsCollector.Metrics.ActorLastCompletionTime.TryGetValue(actorKey, out var lastTime))
                        {
                            var timeSinceCompletion = currentTime - lastTime;
                            if (timeSinceCompletion > config.StarvationThresholdSeconds)
                            {
                                result.Success = false;
                                result.FailureReason = $"Actor {actorKey} starvation: {timeSinceCompletion:F1}s since last completion";
                                result.Violation = new InvariantViolation
                                {
                                    Reason = "Actor starvation detected",
                                    TimeSeconds = currentTime,
                                    Details = new Dictionary<string, object>
                                    {
                                        ["actorId"] = actorKey,
                                        ["timeSinceLastCompletion"] = timeSinceCompletion
                                    }
                                };
                                break;
                            }
                        }
                    }

                    if (!result.Success)
                        break;

                    nextCheckTime += checkInterval;
                }
            }

            // Collect final metrics and trace
            result.Metrics = metricsCollector.Metrics;
            result.RecentEvents = metricsCollector.RecentEvents.ToList();
            result.TraceHash = TraceHelper.ComputeTraceHash(traceSink.GetEvents());

        }
        catch (Exception ex)
        {
            result.Success = false;
            result.FailureReason = $"Exception: {ex.Message}";
        }

        return result;
    }

    public static void PrintResult(FuzzRunResult result, bool verbose = false)
    {
        Console.WriteLine($"\n{'=',60}");
        if (result.Success)
        {
            Console.WriteLine("✓ FUZZ RUN PASSED");
        }
        else
        {
            Console.WriteLine("✗ FUZZ RUN FAILED");
            Console.WriteLine($"Reason: {result.FailureReason}");
        }
        Console.WriteLine($"{'=',60}");

        Console.WriteLine($"Seed: {result.Config.Seed}");
        Console.WriteLine($"Duration: {result.Config.SimulatedDurationSeconds}s");
        Console.WriteLine($"Actors: {result.Config.NumActors}");
        Console.WriteLine($"Trace Hash: {result.TraceHash}");

        Console.WriteLine($"\n--- Metrics ---");
        Console.WriteLine($"Total Actions: {result.Metrics.TotalActions}");
        Console.WriteLine($"Completed: {result.Metrics.CompletedActions}");
        Console.WriteLine($"Failed: {result.Metrics.FailedActions}");
        Console.WriteLine($"Scenes Proposed: {result.Metrics.ScenesProposed}");
        Console.WriteLine($"Scenes Completed: {result.Metrics.ScenesCompleted}");
        Console.WriteLine($"Scenes Aborted: {result.Metrics.ScenesAborted}");
        Console.WriteLine($"Signals Processed: {result.Metrics.SignalsProcessed}");
        Console.WriteLine($"Max Signal Backlog: {result.Metrics.MaxSignalBacklog}");

        if (result.Metrics.ActorTaskCompletions.Count > 0)
        {
            Console.WriteLine($"\n--- Actor Completions ---");
            foreach (var kvp in result.Metrics.ActorTaskCompletions.OrderBy(x => x.Key))
            {
                Console.WriteLine($"{kvp.Key}: {kvp.Value} tasks");
            }
        }

        if (!result.Success && result.Violation != null)
        {
            Console.WriteLine($"\n--- Invariant Violation ---");
            Console.WriteLine($"Time: {result.Violation.TimeSeconds:F2}s");
            Console.WriteLine($"Reason: {result.Violation.Reason}");
            foreach (var detail in result.Violation.Details)
            {
                Console.WriteLine($"  {detail.Key}: {detail.Value}");
            }
        }

        if (!result.Success || verbose)
        {
            Console.WriteLine($"\n--- Recent Events (last {result.RecentEvents.Count}) ---");
            foreach (var evt in result.RecentEvents.TakeLast(20))
            {
                Console.WriteLine(evt);
            }

            Console.WriteLine($"\n--- Config JSON ---");
            Console.WriteLine(JsonSerializer.Serialize(result.Config, new JsonSerializerOptions { WriteIndented = true }));

            Console.WriteLine($"\n--- Event Schedule (first 10) ---");
            foreach (var evt in result.EventSchedule.Events.Take(10))
            {
                Console.WriteLine($"[{evt.TimeSeconds:F2}s] {evt.Signal.Type} -> {evt.Signal.TargetActor?.Value ?? "WORLD"}");
            }
            if (result.EventSchedule.Events.Count > 10)
            {
                Console.WriteLine($"... and {result.EventSchedule.Events.Count - 10} more events");
            }
        }
    }
}
