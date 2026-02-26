namespace JohnnyLike.Engine;

public class VarietyMemory
{
    private readonly Dictionary<string, List<(long Tick, string ActionType)>> _history = new();
    private readonly long _memoryWindowTicks;

    public VarietyMemory(long memoryWindowTicks = 6000L) // 300s * 20 Hz
    {
        _memoryWindowTicks = memoryWindowTicks;
    }

    public void RecordAction(string actorId, string actionType, long tick)
    {
        if (!_history.ContainsKey(actorId))
            _history[actorId] = new();
        _history[actorId].Add((tick, actionType));
    }

    public double GetRepetitionPenalty(string actorId, string actionType, long currentTick)
    {
        if (!_history.TryGetValue(actorId, out var history))
            return 0.0;

        var cutoff = currentTick - _memoryWindowTicks;
        var recentCount = history.Count(h => h.Tick >= cutoff && h.ActionType == actionType);
        return recentCount * 0.2;
    }

    public void Cleanup(long currentTick)
    {
        var cutoff = currentTick - _memoryWindowTicks;
        foreach (var key in _history.Keys.ToList())
        {
            _history[key] = _history[key].Where(h => h.Tick >= cutoff).ToList();
            if (_history[key].Count == 0)
                _history.Remove(key);
        }
    }
}
