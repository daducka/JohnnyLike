using JohnnyLike.Domain.Abstractions;
using System.Text.Json;

namespace JohnnyLike.Domain.Island.Stats;

/// <summary>
/// Manages driftwood availability on the beach.
/// Replenishes over time based on tide and weather conditions.
/// Depends on TideStat and WeatherStat.
/// </summary>
public class DriftwoodAvailabilityStat : WorldStat
{
    /// <summary>
    /// Current amount of driftwood available on the beach
    /// </summary>
    public double DriftwoodAvailable { get; set; } = 50.0;

    private const double BaseReplenishPerMinute = 0.5;
    private const double MaxDriftwood = 100.0;

    public DriftwoodAvailabilityStat() : base("driftwood_availability", "stat_driftwood")
    {
    }

    public override IEnumerable<string> GetDependencies()
    {
        yield return "tide";
        yield return "weather";
    }

    public override List<TraceEvent> Tick(double dtSeconds, WorldState worldState, double currentTime)
    {
        var events = new List<TraceEvent>();
        var island = (IslandWorldState)worldState;
        
        var tideStat = island.GetStat<TideStat>("tide");
        var weatherStat = island.GetStat<WeatherStat>("weather");

        if (tideStat != null && weatherStat != null)
        {
            var oldAmount = DriftwoodAvailable;

            // Calculate replenishment rate
            var replenishRate = BaseReplenishPerMinute;

            // 2x faster during high tide
            if (tideStat.TideLevel == TideLevel.High)
            {
                replenishRate *= 2.0;
            }

            // 1.5x faster during windy weather
            if (weatherStat.Weather == Weather.Windy)
            {
                replenishRate *= 1.5;
            }

            // Apply replenishment (convert per-minute rate to per-second)
            var replenishAmount = replenishRate * (dtSeconds / 60.0);
            DriftwoodAvailable = Math.Min(MaxDriftwood, DriftwoodAvailable + replenishAmount);

            // Log significant replenishment (only if amount increased by at least 0.1)
            if (DriftwoodAvailable - oldAmount >= 0.1)
            {
                events.Add(new TraceEvent(
                    currentTime,
                    null,
                    "DriftwoodReplenished",
                    new Dictionary<string, object>
                    {
                        ["oldAmount"] = Math.Round(oldAmount, 2),
                        ["newAmount"] = Math.Round(DriftwoodAvailable, 2),
                        ["added"] = Math.Round(DriftwoodAvailable - oldAmount, 2),
                        ["tide"] = tideStat.TideLevel.ToString(),
                        ["weather"] = weatherStat.Weather.ToString()
                    }
                ));
            }
        }

        return events;
    }

    public override Dictionary<string, object> SerializeToDict()
    {
        var dict = base.SerializeToDict();
        dict["DriftwoodAvailable"] = DriftwoodAvailable;
        return dict;
    }

    public override void DeserializeFromDict(Dictionary<string, JsonElement> data)
    {
        base.DeserializeFromDict(data);
        DriftwoodAvailable = data["DriftwoodAvailable"].GetDouble();
    }
}
