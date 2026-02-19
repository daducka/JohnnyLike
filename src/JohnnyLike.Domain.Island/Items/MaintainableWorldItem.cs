using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using System.Text.Json;

namespace JohnnyLike.Domain.Island;

public abstract class MaintainableWorldItem : WorldItem, IIslandActionCandidate
{
    public double Quality { get; set; } = 100.0;
    public double BaseDecayPerSecond { get; set; } = 0.01;
    public bool IsExpired { get; protected set; } = false;

    protected MaintainableWorldItem(string id, string type, double baseDecayPerSecond = 0.01)
        : base(id, type)
    {
        BaseDecayPerSecond = baseDecayPerSecond;
    }

    public virtual void Tick(double dtSeconds, IslandWorldState world)
    {
        Quality = Math.Max(0.0, Quality - BaseDecayPerSecond * dtSeconds);
    }

    /// <summary>
    /// Called when the item expires and is about to be removed from the world.
    /// Override this to perform custom expiration logic (e.g., spawn effects, update world state).
    /// </summary>
    public virtual void PerformExpiration(IslandWorldState world, IResourceAvailability? resourceAvailability)
    {
        // Default implementation does nothing
        // Subclasses can override to add custom expiration behavior
    }

    /// <summary>
    /// Add action candidates to the output list. Default implementation does nothing.
    /// Override to provide item-specific action candidates.
    /// </summary>
    public virtual void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        // Default implementation provides no candidates
        // Concrete items override this to provide their specific candidates
    }

    public override Dictionary<string, object> SerializeToDict()
    {
        var dict = base.SerializeToDict();
        dict["Quality"] = Quality;
        dict["BaseDecayPerSecond"] = BaseDecayPerSecond;
        return dict;
    }

    public override void DeserializeFromDict(Dictionary<string, JsonElement> data)
    {
        base.DeserializeFromDict(data);
        Quality = data["Quality"].GetDouble();
        BaseDecayPerSecond = data["BaseDecayPerSecond"].GetDouble();
    }
}
