using JohnnyLike.Domain.Abstractions;
using System.Text.Json;

namespace JohnnyLike.Domain.Island.Items;

public class SharkItem : ExpirableWorldItem
{
    /// <summary>
    /// The resource ID that this shark has reserved (typically the water resource).
    /// Null if no resource was reserved or reservation failed.
    /// </summary>
    public ResourceId? ReservedResourceId { get; set; }

    public SharkItem(string id = "shark") 
        : base(id, "shark")
    {
        RoomId = "beach";
    }

    public override void PerformExpiration(IslandWorldState world, IResourceAvailability? resourceAvailability)
    {
        base.PerformExpiration(world, resourceAvailability);
        
        // Release the water resource when the shark expires
        if (ReservedResourceId.HasValue && resourceAvailability != null)
        {
            resourceAvailability.Release(ReservedResourceId.Value);
        }
    }

    public override Dictionary<string, object> SerializeToDict()
    {
        var dict = base.SerializeToDict();
        // ReservedResourceId is not serialized - it will be re-reserved if needed on deserialization
        return dict;
    }

    public override void DeserializeFromDict(Dictionary<string, JsonElement> data)
    {
        base.DeserializeFromDict(data);
        // ReservedResourceId is not deserialized; will be null after deserialization
    }
}
