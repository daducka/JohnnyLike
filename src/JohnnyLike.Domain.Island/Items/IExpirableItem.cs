using JohnnyLike.Domain.Abstractions;
using System.Text.Json;

namespace JohnnyLike.Domain.Island.Items;

/// <summary>
/// Interface for items that expire after a certain time.
/// Handles expiration logic and serialization of expiration time.
/// </summary>
public interface IExpirableItem
{
    /// <summary>
    /// The time when this item expires and should be removed from the world.
    /// </summary>
    double ExpiresAt { get; set; }
}

/// <summary>
/// Base class for items that expire after a certain time.
/// Provides common tick logic and serialization for time-bound items.
/// </summary>
public abstract class ExpirableWorldItem : MaintainableWorldItem, IExpirableItem
{
    public double ExpiresAt { get; set; } = 0.0;

    protected ExpirableWorldItem(string id, string type)
        : base(id, type, baseDecayPerSecond: 0.0)
    {
        // Expirable items don't decay via quality - they expire at a specific time
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
