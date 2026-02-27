using System.Text.Json;

namespace JohnnyLike.Domain.Abstractions;

/// <summary>
/// Base class for all objects that exist within a <see cref="WorldState"/>.
/// Each item has a stable string <see cref="Id"/> and a domain-defined <see cref="Type"/> tag.
/// Room membership is managed by <see cref="WorldState.Rooms"/>, not by the item itself.
/// </summary>
public abstract class WorldItem
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";

    protected WorldItem(string id, string type)
    {
        Id = id;
        Type = type;
    }

    public virtual Dictionary<string, object> SerializeToDict()
    {
        return new Dictionary<string, object>
        {
            ["Id"] = Id,
            ["Type"] = Type
        };
    }

    public virtual void DeserializeFromDict(Dictionary<string, JsonElement> data)
    {
        Id = data["Id"].GetString()!;
        Type = data["Type"].GetString()!;
    }
}
