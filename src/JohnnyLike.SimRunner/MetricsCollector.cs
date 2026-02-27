using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Engine;

namespace JohnnyLike.SimRunner;

public class InvariantViolation
{
    public string Reason { get; set; } = "";
    public double TimeSeconds { get; set; }
    public Dictionary<string, object> Details { get; set; } = new();
}

public class FuzzMetrics
{
    public int TotalActions { get; set; }
    public int CompletedActions { get; set; }
    public int FailedActions { get; set; }
    public int SignalsProcessed { get; set; }
    public int ReservationConflicts { get; set; }
    public Dictionary<string, int> ActorTaskCompletions { get; set; } = new();
    public Dictionary<string, double> ActorLastCompletionTime { get; set; } = new();
    public int SignalBacklogSize { get; set; }
    public int MaxSignalBacklog { get; set; }
}

public class MetricsCollector
{
    private readonly FuzzConfig _config;
    private readonly FuzzMetrics _metrics = new();
    private readonly List<TraceEvent> _recentEvents = new();
    private const int MaxRecentEvents = 100;

    public MetricsCollector(FuzzConfig config)
    {
        _config = config;
    }

    public FuzzMetrics Metrics => _metrics;
    public IReadOnlyList<TraceEvent> RecentEvents => _recentEvents.AsReadOnly();

    public void RecordEvent(TraceEvent evt)
    {
        _recentEvents.Add(evt);
        if (_recentEvents.Count > MaxRecentEvents)
        {
            _recentEvents.RemoveAt(0);
        }

        switch (evt.EventType)
        {
            case "ActionAssigned":
                _metrics.TotalActions++;
                break;
            
            case "ActionCompleted":
                _metrics.CompletedActions++;
                if (evt.ActorId.HasValue)
                {
                    var actorKey = evt.ActorId.Value.Value;
                    _metrics.ActorTaskCompletions.TryGetValue(actorKey, out var count);
                    _metrics.ActorTaskCompletions[actorKey] = count + 1;
                    _metrics.ActorLastCompletionTime[actorKey] = evt.TimeSeconds;
                }
                
                if (evt.Details.TryGetValue("outcomeType", out var outcome) && 
                    outcome.ToString() == "Failed")
                {
                    _metrics.FailedActions++;
                }
                break;
            
            case "SignalProcessed":
                _metrics.SignalsProcessed++;
                break;
        }
    }

    public InvariantViolation? CheckInvariants(
        double currentTime,
        Engine.Engine engine,
        ReservationTable reservations)
    {
        if (_metrics.ReservationConflicts > _config.MaxAllowedReservationConflicts)
        {
            return new InvariantViolation
            {
                Reason = "Reservation conflicts exceeded maximum allowed",
                TimeSeconds = currentTime,
                Details = new Dictionary<string, object>
                {
                    ["conflicts"] = _metrics.ReservationConflicts,
                    ["maxAllowed"] = _config.MaxAllowedReservationConflicts
                }
            };
        }

        foreach (var actor in engine.Actors)
        {
            var actorKey = actor.Key.Value;
            if (_metrics.ActorLastCompletionTime.TryGetValue(actorKey, out var lastTime))
            {
                var timeSinceCompletion = currentTime - lastTime;
                if (timeSinceCompletion > _config.StarvationThresholdSeconds)
                {
                    return new InvariantViolation
                    {
                        Reason = "Actor starvation detected",
                        TimeSeconds = currentTime,
                        Details = new Dictionary<string, object>
                        {
                            ["actorId"] = actorKey,
                            ["timeSinceLastCompletion"] = timeSinceCompletion,
                            ["threshold"] = _config.StarvationThresholdSeconds
                        }
                    };
                }
            }
            else if (currentTime > _config.StarvationThresholdSeconds)
            {
                return new InvariantViolation
                {
                    Reason = "Actor has never completed a task",
                    TimeSeconds = currentTime,
                    Details = new Dictionary<string, object>
                    {
                        ["actorId"] = actorKey,
                        ["currentTime"] = currentTime
                    }
                };
            }
        }

        _metrics.MaxSignalBacklog = Math.Max(_metrics.MaxSignalBacklog, _metrics.SignalBacklogSize);
        if (_metrics.SignalBacklogSize > _config.MaxActorQueueLength * 2)
        {
            return new InvariantViolation
            {
                Reason = "Signal backlog exceeded maximum",
                TimeSeconds = currentTime,
                Details = new Dictionary<string, object>
                {
                    ["backlogSize"] = _metrics.SignalBacklogSize,
                    ["maxAllowed"] = _config.MaxActorQueueLength * 2
                }
            };
        }

        return null;
    }

    public void UpdateSignalBacklog(int size)
    {
        _metrics.SignalBacklogSize = size;
    }
}
