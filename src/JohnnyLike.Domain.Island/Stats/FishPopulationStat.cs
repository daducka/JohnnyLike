using JohnnyLike.Domain.Abstractions;
using System.Text.Json;

namespace JohnnyLike.Domain.Island.Stats;

/// <summary>
/// Manages fish population regeneration over time.
/// Depends on WeatherStat and TimeOfDayStat for environmental modifiers.
/// </summary>
public class FishPopulationStat : WorldStat
{
    /// <summary>
    /// Current number of fish available for fishing
    /// </summary>
    public double FishAvailable { get; set; } = 100.0;

    /// <summary>
    /// Rate at which fish regenerate per minute
    /// </summary>
    public double FishRegenRatePerMinute { get; set; } = 5.0;

    public FishPopulationStat() : base("fish_population", "stat_fish_population")
    {
    }

    public override IEnumerable<string> GetDependencies()
    {
        yield return "weather";
        yield return "time_of_day";
    }

    public override List<TraceEvent> Tick(double dtSeconds, WorldState worldState, double currentTime)
    {
        var events = new List<TraceEvent>();
        
        // Track old value for trace event
        var oldFish = FishAvailable;
        
        // Regenerate fish over time
        var regenAmount = FishRegenRatePerMinute * (dtSeconds / 60.0);
        FishAvailable = Math.Min(100.0, FishAvailable + regenAmount);
        
        // Log fish regeneration if significant (at least 1 fish worth)
        if (FishAvailable - oldFish >= 1.0)
        {
            events.Add(new TraceEvent(
                currentTime,
                null,
                "FishRegenerated",
                new Dictionary<string, object>
                {
                    ["oldAvailable"] = Math.Round(oldFish, 2),
                    ["newAvailable"] = Math.Round(FishAvailable, 2),
                    ["regenerated"] = Math.Round(FishAvailable - oldFish, 2)
                }
            ));
        }
        
        // Future: could add weather-based modifiers
        // var island = (IslandWorldState)worldState;
        // var weatherStat = island.GetStat<WeatherStat>("weather");
        // if (weatherStat?.Weather == Weather.Rainy) { ... }
        
        return events;
    }

    public override Dictionary<string, object> SerializeToDict()
    {
        var dict = base.SerializeToDict();
        dict["FishAvailable"] = FishAvailable;
        dict["FishRegenRatePerMinute"] = FishRegenRatePerMinute;
        return dict;
    }

    public override void DeserializeFromDict(Dictionary<string, JsonElement> data)
    {
        base.DeserializeFromDict(data);
        FishAvailable = data["FishAvailable"].GetDouble();
        FishRegenRatePerMinute = data["FishRegenRatePerMinute"].GetDouble();
    }
}
