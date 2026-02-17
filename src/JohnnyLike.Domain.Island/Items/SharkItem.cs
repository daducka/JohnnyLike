using JohnnyLike.Domain.Abstractions;
using System.Text.Json;

namespace JohnnyLike.Domain.Island.Items;

public class SharkItem : WorldItem
{
    public double ExpiresAt { get; set; } = 0.0;

    public SharkItem(string id = "shark") 
        : base(id, "shark")
    {
    }

    public void Tick(double currentTime, IslandWorldState world)
    {
        // Auto-despawn when expired
        if (currentTime >= ExpiresAt)
        {
            world.WorldItems.Remove(this);
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
