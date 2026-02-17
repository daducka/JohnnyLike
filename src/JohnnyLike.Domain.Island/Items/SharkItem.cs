using JohnnyLike.Domain.Abstractions;
using System.Text.Json;

namespace JohnnyLike.Domain.Island.Items;

public class SharkItem : MaintainableWorldItem
{
    public double ExpiresAt { get; set; } = 0.0;

    public SharkItem(string id = "shark") 
        : base(id, "shark", baseDecayPerSecond: 0.0)
    {
    }

    public override void Tick(double dtSeconds, IslandWorldState world)
    {
        base.Tick(dtSeconds, world);
        
        // Mark as expired when time is up
        if (world.CurrentTime >= ExpiresAt)
        {
            IsExpired = true;
        }
    }

    public override Dictionary<string, object> SerializeToDict()
    {
        var dict = base.SerializeToDict();
        dict["ExpiresAt"] = ExpiresAt;
        return dict;
    }

    public override void DeserializeFromDict(Dictionary<string, JsonElement> data)
    {
        base.DeserializeFromDict(data);
        ExpiresAt = data["ExpiresAt"].GetDouble();
    }
}
