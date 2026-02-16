using System.Text.Json;

namespace JohnnyLike.Domain.Abstractions;

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
