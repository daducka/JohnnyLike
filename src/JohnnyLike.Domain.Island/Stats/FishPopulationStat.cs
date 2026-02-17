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

    public override void Tick(double dtSeconds, WorldState worldState)
    {
        // Regenerate fish over time
        var regenAmount = FishRegenRatePerMinute * (dtSeconds / 60.0);
        FishAvailable = Math.Min(100.0, FishAvailable + regenAmount);
        
        // Future: could add weather-based modifiers
        // var island = (IslandWorldState)worldState;
        // var weatherStat = island.GetStat<WeatherStat>("weather");
        // if (weatherStat?.Weather == Weather.Rainy) { ... }
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
