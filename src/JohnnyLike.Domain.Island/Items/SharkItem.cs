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

    public override void Tick(double dtSeconds, IslandWorldState world)
    {
        base.Tick(dtSeconds, world);
        
        // Mark as expired when time is up
        if (world.CurrentTime >= ExpiresAt)
        {
            IsExpired = true;
        }
    }

    public override void PerformExpiration(IslandWorldState world)
    {
        base.PerformExpiration(world);
        
        // Release the water resource when the shark expires
        if (ReservedResourceId.HasValue && world.ReservationService != null)
        {
            world.ReservationService.Release(ReservedResourceId.Value);
        }
    }

    public override Dictionary<string, object> SerializeToDict()
    {
        var dict = base.SerializeToDict();
        dict["ExpiresAt"] = ExpiresAt;
        // Don't serialize ReservedResourceId as it's runtime-only state
        return dict;
    }

    public override void DeserializeFromDict(Dictionary<string, JsonElement> data)
    {
        base.DeserializeFromDict(data);
        ExpiresAt = data["ExpiresAt"].GetDouble();
        // ReservedResourceId is not deserialized; will be null after deserialization
    }
}
