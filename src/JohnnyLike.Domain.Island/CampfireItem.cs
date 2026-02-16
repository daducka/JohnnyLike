namespace JohnnyLike.Domain.Island;

public class CampfireItem : MaintainableWorldItem
{
    public bool IsLit { get; set; } = true;
    public double FuelSeconds { get; set; } = 3600.0;

    public CampfireItem(string id = "main_campfire") 
        : base(id, "campfire", baseDecayPerSecond: 0.02)
    {
    }

    public override void Tick(double dtSeconds, IslandWorldState world)
    {
        base.Tick(dtSeconds, world);

        if (IsLit)
        {
            FuelSeconds = Math.Max(0.0, FuelSeconds - dtSeconds);
            
            if (FuelSeconds <= 0.0)
            {
                IsLit = false;
            }
        }

        if (!IsLit)
        {
            Quality = Math.Max(0.0, Quality - 0.05 * dtSeconds);
        }
    }
}
