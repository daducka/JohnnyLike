using JohnnyLike.Domain.Abstractions;
using System.Text.Json;

namespace JohnnyLike.Domain.Island.Items;

public interface IExpirableItem
{
    long ExpiresAtTick { get; set; }
}

public abstract class ExpirableWorldItem : MaintainableWorldItem, IExpirableItem
{
    public long ExpiresAtTick { get; set; } = 0L;

    protected ExpirableWorldItem(string id, string type)
        : base(id, type, baseDecayPerSecond: 0.0)
    {
    }

    public override void Tick(double dtSeconds, IslandWorldState world)
    {
        base.Tick(dtSeconds, world);

        if (world.CurrentTick >= ExpiresAtTick)
        {
            IsExpired = true;
        }
    }

    public override Dictionary<string, object> SerializeToDict()
    {
        var dict = base.SerializeToDict();
        dict["ExpiresAtTick"] = ExpiresAtTick;
        return dict;
    }

    public override void DeserializeFromDict(Dictionary<string, JsonElement> data)
    {
        base.DeserializeFromDict(data);
        if (data.TryGetValue("ExpiresAtTick", out var eat))
            ExpiresAtTick = eat.GetInt64();
        else if (data.TryGetValue("ExpiresAt", out var ea)) // backward compat
            ExpiresAtTick = (long)(ea.GetDouble() * 20.0);
    }
}
