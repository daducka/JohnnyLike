using JohnnyLike.Domain.Abstractions;
using System.Text.Json;

namespace JohnnyLike.Domain.Island.Stats;

/// <summary>
/// Manages weather changes over time.
/// Depends on TimeOfDayStat for time-based weather patterns.
/// </summary>
public class WeatherStat : WorldStat
{
    /// <summary>
    /// Current weather condition
    /// </summary>
    public Weather Weather { get; set; } = Weather.Clear;

    public WeatherStat() : base("weather", "stat_weather")
    {
    }

    public override IEnumerable<string> GetDependencies()
    {
        yield return "time_of_day";
    }

    public override void Tick(double dtSeconds, WorldState worldState)
    {
        // Weather logic can be expanded here
        // For now, maintaining existing behavior (weather doesn't auto-change)
        // Future: could add time-based weather patterns, random changes, etc.
    }

    public override Dictionary<string, object> SerializeToDict()
    {
        var dict = base.SerializeToDict();
        dict["Weather"] = Weather.ToString();
        return dict;
    }

    public override void DeserializeFromDict(Dictionary<string, JsonElement> data)
    {
        base.DeserializeFromDict(data);
        Weather = Enum.Parse<Weather>(data["Weather"].GetString()!);
    }
}
