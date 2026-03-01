using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Telemetry;

namespace JohnnyLike.Domain.Island.Items;

/// <summary>Broad time-of-day phases used for narration grounding.</summary>
public enum DayPhase { Dawn, Morning, Noon, Afternoon, Evening, Night }

public class CalendarItem : WorldItem, ITickableWorldItem
{
    public double TimeOfDay { get; set; } = 0.5;
    public int DayCount { get; set; } = 0;
    public DayPhase CurrentDayPhase { get; private set; } = DayPhase.Morning;
    private DayPhase _lastEmittedDayPhase = DayPhase.Morning;
    private long _lastTick = 0;

    public CalendarItem(string id = "calendar") : base(id, "calendar") { }

    public IEnumerable<string> GetDependencies() => Enumerable.Empty<string>();

    public List<TraceEvent> Tick(long currentTick, WorldState worldState)
    {
        var world = (IslandWorldState)worldState;
        var dtTicks = currentTick - _lastTick;
        _lastTick = currentTick;
        var dtSeconds = (double)dtTicks / 20.0;

        TimeOfDay += dtSeconds / 86400.0;

        var events = new List<TraceEvent>();

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

        // Emit DayPhaseChanged trace event when the phase changes.
        var newPhase = ComputeDayPhase(HourOfDay);
        if (newPhase != _lastEmittedDayPhase)
        {
            _lastEmittedDayPhase = newPhase;
            CurrentDayPhase = newPhase;

            events.Add(new TraceEvent(
                currentTick,
                null,
                "DayPhaseChanged",
                new Dictionary<string, object>
                {
                    ["dayPhase"] = newPhase.ToString(),
                    ["text"]     = DayPhaseText(newPhase)
                }
            ));
        }

        return events;
    }

    public double HourOfDay => TimeOfDay * 24.0;

    /// <summary>
    /// Maps the current hour (0–24) to a broad <see cref="DayPhase"/>:
    /// 5–7 Dawn, 7–12 Morning, 12–13 Noon, 13–17 Afternoon, 17–20 Evening,
    /// all other hours (20–24 / 0–5) Night.
    /// </summary>
    public static DayPhase ComputeDayPhase(double hourOfDay) => hourOfDay switch
    {
        >= 5  and < 7  => DayPhase.Dawn,
        >= 7  and < 12 => DayPhase.Morning,
        >= 12 and < 13 => DayPhase.Noon,
        >= 13 and < 17 => DayPhase.Afternoon,
        >= 17 and < 20 => DayPhase.Evening,
        _              => DayPhase.Night
    };

    private static string DayPhaseText(DayPhase phase) => phase switch
    {
        DayPhase.Dawn      => "The horizon brightens as dawn breaks over the island.",
        DayPhase.Morning   => "Morning light spreads across the beach.",
        DayPhase.Noon      => "The sun reaches its peak directly overhead.",
        DayPhase.Afternoon => "The afternoon heat settles over the island.",
        DayPhase.Evening   => "The sun dips toward the horizon, painting the sky orange.",
        DayPhase.Night     => "Night falls, and the stars emerge above the island.",
        _                  => $"It is {phase.ToString().ToLowerInvariant()}."
    };

    public override Dictionary<string, object> SerializeToDict()
    {
        var dict = base.SerializeToDict();
        dict["TimeOfDay"] = TimeOfDay;
        dict["DayCount"] = DayCount;
        dict["LastTick"] = _lastTick;
        dict["CurrentDayPhase"] = CurrentDayPhase.ToString();
        return dict;
    }

    public override void DeserializeFromDict(Dictionary<string, System.Text.Json.JsonElement> data)
    {
        base.DeserializeFromDict(data);
        if (data.TryGetValue("TimeOfDay", out var tod)) TimeOfDay = tod.GetDouble();
        if (data.TryGetValue("DayCount", out var dc)) DayCount = dc.GetInt32();
        if (data.TryGetValue("LastTick", out var lt)) _lastTick = lt.GetInt64();
        if (data.TryGetValue("CurrentDayPhase", out var dp) &&
            Enum.TryParse<DayPhase>(dp.GetString(), out var phase))
        {
            CurrentDayPhase = phase;
            _lastEmittedDayPhase = phase;
        }
    }
}
