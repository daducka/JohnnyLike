using JohnnyLike.Domain.Abstractions;
using System.Text.Json;

namespace JohnnyLike.Domain.Island.Supply;

/// <summary>
/// Base class for all supply items that can be stored in supply piles
/// </summary>
public abstract class SupplyItem : WorldItem
{
    public double Quantity { get; set; }

    protected SupplyItem(string id, string type, double quantity = 0.0) 
        : base(id, type)
    {
        Quantity = quantity;
    }

    public override Dictionary<string, object> SerializeToDict()
    {
        var dict = base.SerializeToDict();
        dict["Quantity"] = Quantity;
        return dict;
    }

    public override void DeserializeFromDict(Dictionary<string, JsonElement> data)
    {
        base.DeserializeFromDict(data);
        Quantity = data["Quantity"].GetDouble();
    }
}
