namespace JohnnyLike.Engine;

public class VarietyMemory
{
    private readonly Dictionary<string, List<(double Time, string ActionType)>> _history = new();
    private readonly double _memoryWindowSeconds;

    public VarietyMemory(double memoryWindowSeconds = 300.0)
    {
        _memoryWindowSeconds = memoryWindowSeconds;
    }

    public void RecordAction(string actorId, string actionType, double time)
    {
        if (!_history.ContainsKey(actorId))
        {
            _history[actorId] = new();
        }

        _history[actorId].Add((time, actionType));
    }

    public double GetRepetitionPenalty(string actorId, string actionType, double currentTime)
    {
        if (!_history.TryGetValue(actorId, out var history))
        {
            return 0.0;
        }

        var cutoff = currentTime - _memoryWindowSeconds;
        var recentCount = history.Count(h => h.Time >= cutoff && h.ActionType == actionType);

        return recentCount * 0.2;
    }

    public void Cleanup(double currentTime)
    {
        var cutoff = currentTime - _memoryWindowSeconds;
        foreach (var key in _history.Keys.ToList())
        {
            _history[key] = _history[key].Where(h => h.Time >= cutoff).ToList();
            if (_history[key].Count == 0)
            {
                _history.Remove(key);
            }
        }
    }
}
