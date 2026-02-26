namespace JohnnyLike.Domain.Abstractions;

public record TraceEvent(
    long Tick,
    ActorId? ActorId,
    string EventType,
    Dictionary<string, object> Details
)
{
    /// <summary>Display-only helper: converts tick to seconds at 20 Hz.</summary>
    public double TimeSeconds => (double)Tick / 20.0;

    public override string ToString()
    {
        var actorStr = ActorId.HasValue ? ActorId.Value.ToString() : "SYSTEM";
        var baseStr = $"[{TimeSeconds:F2}s] {actorStr} - {EventType}";
        
        if (Details.Count > 0)
        {
            var detailsStr = string.Join(", ", Details.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            return $"{baseStr} ({detailsStr})";
        }
        
        return baseStr;
    }
}

public interface ITraceSink
{
    void Record(TraceEvent evt);
    List<TraceEvent> GetEvents();
    void Clear();
}

public class InMemoryTraceSink : ITraceSink
{
    private readonly List<TraceEvent> _events = new();

    public void Record(TraceEvent evt)
    {
        _events.Add(evt);
    }

    public List<TraceEvent> GetEvents() => new(_events);

    public void Clear() => _events.Clear();
}
