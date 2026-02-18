using JohnnyLike.Domain.Abstractions;
using System.Text.Json;

namespace JohnnyLike.Domain.Island.Stats;

/// <summary>
/// Manages the day/night cycle and day count.
/// Has no dependencies - ticked first in the update order.
/// </summary>
public class TimeOfDayStat : WorldStat
{
    /// <summary>
    /// Time of day as fraction of a day (0.0 = midnight, 0.5 = noon, 1.0 = midnight)
    /// </summary>
    public double TimeOfDay { get; set; } = 0.5;

    /// <summary>
    /// Number of complete days that have passed
    /// </summary>
    public int DayCount { get; set; } = 0;

    public TimeOfDayStat() : base("time_of_day", "stat_time_of_day")
    {
    }

    public override List<TraceEvent> Tick(double dtSeconds, WorldState worldState, double currentTime)
    {
        var events = new List<TraceEvent>();
        
        TimeOfDay += dtSeconds / 86400.0; // 86400 seconds in a day
        
        if (TimeOfDay >= 1.0)
        {
            TimeOfDay -= 1.0;
            DayCount++;
        }
        
        return events;
    }

    public override Dictionary<string, object> SerializeToDict()
    {
        var dict = base.SerializeToDict();
        dict["TimeOfDay"] = TimeOfDay;
        dict["DayCount"] = DayCount;
        return dict;
    }

    public override void DeserializeFromDict(Dictionary<string, JsonElement> data)
    {
        base.DeserializeFromDict(data);
        TimeOfDay = data["TimeOfDay"].GetDouble();
        DayCount = data["DayCount"].GetInt32();
    }
}
