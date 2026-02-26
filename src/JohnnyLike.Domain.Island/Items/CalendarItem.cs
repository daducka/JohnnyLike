using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Telemetry;

namespace JohnnyLike.Domain.Island.Items;

public class CalendarItem : WorldItem, ITickableWorldItem
{
    public double TimeOfDay { get; set; } = 0.5;
    public int DayCount { get; set; } = 0;
    private long _lastTick = 0;

    public CalendarItem(string id = "calendar") : base(id, "calendar") { RoomId = "beach"; }

    public IEnumerable<string> GetDependencies() => Enumerable.Empty<string>();

    public List<TraceEvent> Tick(long currentTick, WorldState worldState)
    {
        var world = (IslandWorldState)worldState;
        var dtTicks = currentTick - _lastTick;
        _lastTick = currentTick;
        var dtSeconds = (double)dtTicks / 20.0;

        TimeOfDay += dtSeconds / 86400.0;

        if (TimeOfDay >= 1.0)
        {
            TimeOfDay -= 1.0;
            DayCount++;

            using (world.Tracer.PushPhase(TracePhase.WorldTick))
                world.Tracer.BeatWorld(
                    $"Day {DayCount} has begun.",
                    subjectId: "calendar:day",
                    priority: 30);
        }

        return new List<TraceEvent>();
    }

    public double HourOfDay => TimeOfDay * 24.0;

    public override Dictionary<string, object> SerializeToDict()
    {
        var dict = base.SerializeToDict();
        dict["TimeOfDay"] = TimeOfDay;
        dict["DayCount"] = DayCount;
        dict["LastTick"] = _lastTick;
        return dict;
    }

    public override void DeserializeFromDict(Dictionary<string, System.Text.Json.JsonElement> data)
    {
        base.DeserializeFromDict(data);
        if (data.TryGetValue("TimeOfDay", out var tod)) TimeOfDay = tod.GetDouble();
        if (data.TryGetValue("DayCount", out var dc)) DayCount = dc.GetInt32();
        if (data.TryGetValue("LastTick", out var lt)) _lastTick = lt.GetInt64();
    }
}
