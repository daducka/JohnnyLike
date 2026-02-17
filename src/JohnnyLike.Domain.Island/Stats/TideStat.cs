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

    public override void Tick(double dtSeconds, WorldState worldState)
    {
        var island = (IslandWorldState)worldState;
        var timeOfDayStat = island.GetStat<TimeOfDayStat>("time_of_day");
        
        if (timeOfDayStat != null)
        {
            var tidePhase = (timeOfDayStat.TimeOfDay * 24.0) % 12.0;
            TideLevel = tidePhase >= 6.0 ? TideLevel.High : TideLevel.Low;
        }
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
