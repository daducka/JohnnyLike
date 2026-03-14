using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Domain.Island.Items;

/// <summary>
/// A stalactite in the cave. Drips every simulated hour and emits a StalactiteDrip trace event.
/// </summary>
public class StalactiteItem : WorldItem, ITickableWorldItem
{
    private long _lastDripTick = 0L;
    private static readonly Duration DripInterval = Duration.Hours(1);

    public StalactiteItem(string id = "stalactite") : base(id, "stalactite")
    {
    }

    public IEnumerable<string> GetDependencies() => Enumerable.Empty<string>();

    public List<TraceEvent> Tick(long currentTick, WorldState worldState)
    {
        var events = new List<TraceEvent>();
        var dripIntervalTicks = DripInterval.Ticks;

        // Emit a drip every simulated hour
        if (currentTick > 0 && currentTick >= _lastDripTick + dripIntervalTicks)
        {
            var dripsToEmit = (currentTick - _lastDripTick) / dripIntervalTicks;
            for (long i = 0; i < dripsToEmit; i++)
            {
                var dripTick = _lastDripTick + dripIntervalTicks * (i + 1);
                events.Add(new TraceEvent(
                    dripTick,
                    null,
                    "StalactiteDrip",
                    new Dictionary<string, object>
                    {
                        ["itemId"] = Id,
                        ["tick"] = dripTick
                    }
                ));
            }
            _lastDripTick = _lastDripTick + dripIntervalTicks * dripsToEmit;
        }

        return events;
    }

    public override Dictionary<string, object> SerializeToDict()
    {
        var dict = base.SerializeToDict();
        dict["LastDripTick"] = _lastDripTick;
        return dict;
    }

    public override void DeserializeFromDict(Dictionary<string, System.Text.Json.JsonElement> data)
    {
        base.DeserializeFromDict(data);
        if (data.TryGetValue("LastDripTick", out var ldt))
            _lastDripTick = ldt.GetInt64();
    }
}
