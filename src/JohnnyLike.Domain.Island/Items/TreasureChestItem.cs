using JohnnyLike.Domain.Abstractions;
using System.Text.Json;

namespace JohnnyLike.Domain.Island.Items;

public class TreasureChestItem : WorldItem
{
    public bool IsOpened { get; set; } = false;
    public double Health { get; set; } = 100.0;
    public string Position { get; set; } = "shore";

    public TreasureChestItem(string id = "treasure_chest") 
        : base(id, "treasure_chest")
    {
    }

    public override Dictionary<string, object> SerializeToDict()
    {
        var dict = base.SerializeToDict();
        dict["IsOpened"] = IsOpened;
        dict["Health"] = Health;
        dict["Position"] = Position;
        return dict;
    }

    public override void DeserializeFromDict(Dictionary<string, JsonElement> data)
    {
        base.DeserializeFromDict(data);
        IsOpened = data["IsOpened"].GetBoolean();
        Health = data["Health"].GetDouble();
        Position = data["Position"].GetString()!;
    }
}
