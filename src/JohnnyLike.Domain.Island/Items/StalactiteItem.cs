using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Domain.Island.Items;

/// <summary>
/// A stalactite in the cave. Drips every 60 ticks and emits a StalactiteDrip trace event.
/// </summary>
public class StalactiteItem : WorldItem, ITickableWorldItem
{
    private long _lastDripTick = 0L;
    private const long DripIntervalTicks = 60L;

    public StalactiteItem(string id = "stalactite") : base(id, "stalactite")
    {
        RoomId = "cave";
    }

    public IEnumerable<string> GetDependencies() => Enumerable.Empty<string>();

    public List<TraceEvent> Tick(long currentTick, WorldState worldState)
    {
        var events = new List<TraceEvent>();

        // Emit a drip every 60 ticks
        if (currentTick > 0 && currentTick >= _lastDripTick + DripIntervalTicks)
        {
            var dripsToEmit = (currentTick - _lastDripTick) / DripIntervalTicks;
            for (long i = 0; i < dripsToEmit; i++)
            {
                var dripTick = _lastDripTick + DripIntervalTicks * (i + 1);
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
            _lastDripTick = _lastDripTick + DripIntervalTicks * dripsToEmit;
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
