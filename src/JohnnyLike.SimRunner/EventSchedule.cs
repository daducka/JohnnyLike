using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.SimRunner;

public record ScheduledSignal(
    double TimeSeconds,
    Signal Signal
);

public class EventSchedule
{
    private readonly List<ScheduledSignal> _events = new();

    public IReadOnlyList<ScheduledSignal> Events => _events.AsReadOnly();

    public void Add(double timeSeconds, Signal signal)
    {
        _events.Add(new ScheduledSignal(timeSeconds, signal));
    }

    public void SortByTime()
    {
        _events.Sort((a, b) => a.TimeSeconds.CompareTo(b.TimeSeconds));
    }

    public static EventSchedule Generate(FuzzConfig config, Random rng, List<ActorId> actorIds)
    {
        var schedule = new EventSchedule();
        var currentTime = 0.0;
        var endTime = (double)config.SimulatedDurationSeconds;

        // Calculate mean time between events (minutes to seconds)
        var meanTimeBetweenEvents = 60.0 / config.EventRatePerMinute;

        while (currentTime < endTime)
        {
            // Poisson process: exponential inter-arrival times
            var interval = -Math.Log(1.0 - rng.NextDouble()) * meanTimeBetweenEvents;
            
            // Apply burst multiplier occasionally
            if (rng.NextDouble() < config.BurstProbability)
            {
                interval /= config.BurstMultiplier;
            }

            currentTime += interval;
            
            if (currentTime >= endTime)
                break;

            // Generate random event type
            var eventType = rng.Next(0, 3);
            var targetActor = actorIds[rng.Next(actorIds.Count)];

            Signal signal;
            switch (eventType)
            {
                case 0: // Chat redeem
                    var emotes = new[] { "wave", "dance", "cheer", "laugh", "point" };
                    signal = new Signal(
                        "chat_redeem",
                        currentTime,
                        targetActor,
                        new Dictionary<string, object>
                        {
                            ["emote"] = emotes[rng.Next(emotes.Length)],
                            ["viewer"] = $"Viewer{rng.Next(1000)}"
                        }
                    );
                    break;
                
                case 1: // Cheer
                    signal = new Signal(
                        "cheer",
                        currentTime,
                        targetActor,
                        new Dictionary<string, object>
                        {
                            ["bits"] = rng.Next(1, 1000),
                            ["message"] = "Great job!"
                        }
                    );
                    break;
                
                case 2: // Printer jam (world signal, no specific actor)
                    signal = new Signal(
                        "printer_jam",
                        currentTime,
                        null,
                        new Dictionary<string, object>
                        {
                            ["duration"] = rng.Next(10, 60)
                        }
                    );
                    break;
                
                default:
                    continue;
            }

            schedule.Add(currentTime, signal);
        }

        schedule.SortByTime();
        return schedule;
    }
}
