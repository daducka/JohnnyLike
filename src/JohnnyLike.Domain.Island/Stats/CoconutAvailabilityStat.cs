using JohnnyLike.Domain.Abstractions;
using System.Text.Json;

namespace JohnnyLike.Domain.Island.Stats;

/// <summary>
/// Manages coconut availability with daily regeneration.
/// Depends on TimeOfDayStat to trigger daily regeneration.
/// </summary>
public class CoconutAvailabilityStat : WorldStat
{
    /// <summary>
    /// Current number of coconuts available for gathering
    /// </summary>
    public int CoconutsAvailable { get; set; } = 5;

    private int _lastDayCount = 0;

    public CoconutAvailabilityStat() : base("coconut_availability", "stat_coconut_availability")
    {
    }

    public override IEnumerable<string> GetDependencies()
    {
        yield return "time_of_day";
    }

    public override void Tick(double dtSeconds, WorldState worldState)
    {
        var island = (IslandWorldState)worldState;
        var timeOfDayStat = island.GetStat<TimeOfDayStat>("time_of_day");
        
        if (timeOfDayStat != null && timeOfDayStat.DayCount > _lastDayCount)
        {
            // New day - regenerate coconuts
            CoconutsAvailable = Math.Min(10, CoconutsAvailable + 3);
            _lastDayCount = timeOfDayStat.DayCount;
        }
    }

    public override Dictionary<string, object> SerializeToDict()
    {
        var dict = base.SerializeToDict();
        dict["CoconutsAvailable"] = CoconutsAvailable;
        dict["LastDayCount"] = _lastDayCount;
        return dict;
    }

    public override void DeserializeFromDict(Dictionary<string, JsonElement> data)
    {
        base.DeserializeFromDict(data);
        CoconutsAvailable = data["CoconutsAvailable"].GetInt32();
        // LastDayCount is optional for backward compatibility with saves that don't have it
        _lastDayCount = data.TryGetValue("LastDayCount", out var lastDayElement) 
            ? lastDayElement.GetInt32() 
            : 0;
    }
}
