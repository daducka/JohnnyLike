namespace JohnnyLike.Domain.Abstractions;

/// <summary>
/// Stores timestamped events for actor memory, allowing actors to remember when things last happened.
/// </summary>
public class EventMemory
{
    private readonly Dictionary<string, double> _lastEventTimes = new();

    /// <summary>
    /// Records that an event occurred at the specified time.
    /// </summary>
    public void RecordEvent(string eventKey, double time)
    {
        _lastEventTimes[eventKey] = time;
    }

    /// <summary>
    /// Gets the last time an event occurred, or double.NegativeInfinity if it never occurred.
    /// </summary>
    public double GetLastEventTime(string eventKey)
    {
        return _lastEventTimes.TryGetValue(eventKey, out var time) 
            ? time 
            : double.NegativeInfinity;
    }

    /// <summary>
    /// Gets the time elapsed since an event last occurred.
    /// Returns double.PositiveInfinity if the event never occurred.
    /// </summary>
    public double GetTimeSince(string eventKey, double currentTime)
    {
        var lastTime = GetLastEventTime(eventKey);
        return double.IsNegativeInfinity(lastTime) 
            ? double.PositiveInfinity 
            : currentTime - lastTime;
    }

    /// <summary>
    /// Checks if an event has ever occurred.
    /// </summary>
    public bool HasEventOccurred(string eventKey)
    {
        return _lastEventTimes.ContainsKey(eventKey);
    }

    /// <summary>
    /// Gets all recorded events as a dictionary for serialization.
    /// </summary>
    public Dictionary<string, double> GetAllEvents() => new(_lastEventTimes);

    /// <summary>
    /// Restores events from a dictionary (for deserialization).
    /// </summary>
    public void RestoreEvents(Dictionary<string, double> events)
    {
        _lastEventTimes.Clear();
        foreach (var kvp in events)
        {
            _lastEventTimes[kvp.Key] = kvp.Value;
        }
    }
}
