using JohnnyLike.Domain.Abstractions;
using System.Text.Json;

namespace JohnnyLike.Domain.Island;

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

    public override Dictionary<string, object> SerializeToDict()
    {
        var dict = base.SerializeToDict();
        dict["Quality"] = Quality;
        dict["BaseDecayPerSecond"] = BaseDecayPerSecond;
        return dict;
    }

    public override void DeserializeFromDict(Dictionary<string, JsonElement> data)
    {
        base.DeserializeFromDict(data);
        Quality = data["Quality"].GetDouble();
        BaseDecayPerSecond = data["BaseDecayPerSecond"].GetDouble();
    }
}
