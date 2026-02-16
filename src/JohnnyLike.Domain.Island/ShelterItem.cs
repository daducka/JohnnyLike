namespace JohnnyLike.Domain.Island;

public class ShelterItem : MaintainableWorldItem
{
    public ShelterItem(string id = "main_shelter") 
        : base(id, "shelter", baseDecayPerSecond: 0.015)
    {
    }

    public override void Tick(double dtSeconds, IslandWorldState world)
    {
        base.Tick(dtSeconds, world);

        if (world.Weather == Weather.Rainy)
        {
            Quality = Math.Max(0.0, Quality - 0.03 * dtSeconds);
        }
        else if (world.Weather == Weather.Windy)
        {
            Quality = Math.Max(0.0, Quality - 0.02 * dtSeconds);
        }
    }
}
