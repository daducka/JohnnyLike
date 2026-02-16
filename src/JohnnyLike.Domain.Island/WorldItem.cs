namespace JohnnyLike.Domain.Island;

public abstract class WorldItem
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";

    protected WorldItem(string id, string type)
    {
        Id = id;
        Type = type;
    }
}

public abstract class MaintainableWorldItem : WorldItem
{
    public double Quality { get; set; } = 100.0;
    public double BaseDecayPerSecond { get; set; } = 0.01;

    protected MaintainableWorldItem(string id, string type, double baseDecayPerSecond = 0.01)
        : base(id, type)
    {
        BaseDecayPerSecond = baseDecayPerSecond;
    }

    public virtual void Tick(double dtSeconds, IslandWorldState world)
    {
        Quality = Math.Max(0.0, Quality - BaseDecayPerSecond * dtSeconds);
    }
}
