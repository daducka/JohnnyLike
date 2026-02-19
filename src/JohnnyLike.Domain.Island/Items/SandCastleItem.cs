using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Stats;
using System.Text.Json;

namespace JohnnyLike.Domain.Island.Items;

/// <summary>
/// Represents an actual sand castle on the beach.
/// Decays over time, faster during rain and high tide.
/// </summary>
public class SandCastleItem : MaintainableWorldItem
{
    public SandCastleItem(string id = "sandcastle")
        : base(id, "sandcastle", baseDecayPerSecond: 0.02)
    {
        Quality = 100.0;
    }

    public override void Tick(double dtSeconds, IslandWorldState world)
    {
        // Calculate decay rate based on environmental conditions
        var decayRate = BaseDecayPerSecond;
        
        // Decay much faster in rain
        var weatherStat = world.GetStat<WeatherStat>("weather");
        if (weatherStat?.Weather == Weather.Rainy)
        {
            decayRate *= 5.0; // 5x faster decay in rain
        }
        
        // Decay faster at high tide
        var tideStat = world.GetStat<TideStat>("tide");
        if (tideStat?.TideLevel == TideLevel.High)
        {
            decayRate *= 3.0; // 3x faster decay at high tide
        }
        
        // Apply decay
        Quality = Math.Max(0.0, Quality - decayRate * dtSeconds);
        
        // Mark as expired when quality reaches 0
        if (Quality <= 0.0)
        {
            IsExpired = true;
        }
    }
}
