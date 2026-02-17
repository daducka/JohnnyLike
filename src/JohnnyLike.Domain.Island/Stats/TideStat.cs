using JohnnyLike.Domain.Abstractions;
using System.Text.Json;

namespace JohnnyLike.Domain.Island.Stats;

/// <summary>
/// Calculates tide levels based on time of day.
/// Depends on TimeOfDayStat for calculating tidal patterns.
/// </summary>
public class TideStat : WorldStat
{
    /// <summary>
    /// Current tide level
    /// </summary>
    public TideLevel TideLevel { get; set; } = TideLevel.Low;

    public TideStat() : base("tide", "stat_tide")
    {
    }

    public override IEnumerable<string> GetDependencies()
    {
        yield return "time_of_day";
    }

    public override List<TraceEvent> Tick(double dtSeconds, WorldState worldState, double currentTime)
    {
        var events = new List<TraceEvent>();
        var island = (IslandWorldState)worldState;
        var timeOfDayStat = island.GetStat<TimeOfDayStat>("time_of_day");
        
        if (timeOfDayStat != null)
        {
            var oldTideLevel = TideLevel;
            var tidePhase = (timeOfDayStat.TimeOfDay * 24.0) % 12.0;
            TideLevel = tidePhase >= 6.0 ? TideLevel.High : TideLevel.Low;
            
            // Log tide changes
            if (TideLevel != oldTideLevel)
            {
                events.Add(new TraceEvent(
                    currentTime,
                    null,
                    "TideChanged",
                    new Dictionary<string, object>
                    {
                        ["oldTide"] = oldTideLevel.ToString(),
                        ["newTide"] = TideLevel.ToString(),
                        ["timeOfDay"] = Math.Round(timeOfDayStat.TimeOfDay * 24.0, 2)
                    }
                ));
            }
        }
        
        return events;
    }

    public override Dictionary<string, object> SerializeToDict()
    {
        var dict = base.SerializeToDict();
        dict["TideLevel"] = TideLevel.ToString();
        return dict;
    }

    public override void DeserializeFromDict(Dictionary<string, JsonElement> data)
    {
        base.DeserializeFromDict(data);
        TideLevel = Enum.Parse<TideLevel>(data["TideLevel"].GetString()!);
    }
}
