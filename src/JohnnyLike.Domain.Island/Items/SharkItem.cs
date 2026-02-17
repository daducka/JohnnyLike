using JohnnyLike.Domain.Abstractions;
using System.Text.Json;

namespace JohnnyLike.Domain.Island.Items;

public class SharkItem : MaintainableWorldItem
{
    public double ExpiresAt { get; set; } = 0.0;
    
    /// <summary>
    /// The resource ID that this shark has reserved (typically the water resource).
    /// Null if no resource was reserved or reservation failed.
    /// </summary>
    public ResourceId? ReservedResourceId { get; set; }

    public SharkItem(string id = "shark") 
        : base(id, "shark", baseDecayPerSecond: 0.0)
    {
    }

    public override void Tick(double dtSeconds, IslandWorldState world, IResourceAvailability? resourceAvailability)
    {
        base.Tick(dtSeconds, world, resourceAvailability);
        
        // Mark as expired when time is up
        if (world.CurrentTime >= ExpiresAt)
        {
            IsExpired = true;
        }
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
        dict["ExpiresAt"] = ExpiresAt;
        // ReservedResourceId is not serialized - it will be re-reserved if needed on deserialization
        return dict;
    }

    public override void DeserializeFromDict(Dictionary<string, JsonElement> data)
    {
        base.DeserializeFromDict(data);
        ExpiresAt = data["ExpiresAt"].GetDouble();
        // ReservedResourceId is not deserialized; will be null after deserialization
    }
}
