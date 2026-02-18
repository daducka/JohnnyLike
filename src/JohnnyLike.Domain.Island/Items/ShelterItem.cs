using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Stats;
using System.Text.Json;

namespace JohnnyLike.Domain.Island.Items;

public class ShelterItem : MaintainableWorldItem
{
    public ShelterItem(string id = "main_shelter") 
        : base(id, "shelter", baseDecayPerSecond: 0.015)
    {
    }

    public override void Tick(double dtSeconds, IslandWorldState world)
    {
        base.Tick(dtSeconds, world);

        var weatherStat = world.GetStat<WeatherStat>("weather");
        if (weatherStat?.Weather == Weather.Rainy)
        {
            Quality = Math.Max(0.0, Quality - 0.03 * dtSeconds);
        }
        else if (weatherStat?.Weather == Weather.Windy)
        {
            Quality = Math.Max(0.0, Quality - 0.02 * dtSeconds);
        }
    }
}
