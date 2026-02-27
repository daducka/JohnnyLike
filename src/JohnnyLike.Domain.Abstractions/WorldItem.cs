using System.Text.Json;

namespace JohnnyLike.Domain.Abstractions;

/// <summary>
/// Base class for all objects that exist within a <see cref="WorldState"/>.
/// Each item has a stable string <see cref="Id"/>, a domain-defined <see cref="Type"/> tag,
/// and an optional <see cref="RoomId"/> used for room-scoped candidate filtering.
/// </summary>
public abstract class WorldItem
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string RoomId { get; set; } = "";

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
            ["Type"] = Type,
            ["RoomId"] = RoomId
        };
    }

    public virtual void DeserializeFromDict(Dictionary<string, JsonElement> data)
    {
        Id = data["Id"].GetString()!;
        Type = data["Type"].GetString()!;
        if (data.TryGetValue("RoomId", out var roomId))
            RoomId = roomId.GetString() ?? "";
    }
}
