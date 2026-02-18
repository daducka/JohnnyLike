using JohnnyLike.Domain.Abstractions;
using System.Text.Json;

namespace JohnnyLike.Domain.Island.Items;

/// <summary>
/// Base class for world items that can be owned and used as tools by actors.
/// Supports both shared (multiple users) and exclusive (single owner) ownership models.
/// </summary>
public abstract class ToolItem : MaintainableWorldItem
{
    public OwnershipType OwnershipType { get; set; }
    
    /// <summary>
    /// For exclusive tools, this is the owner's ActorId. Null for shared tools or unowned exclusive tools.
    /// </summary>
    public ActorId? OwnerActorId { get; set; }
    
    /// <summary>
    /// Maximum number of actors that can own this tool simultaneously.
    /// For Exclusive tools, this is typically 1. For Shared tools, it can be unlimited (int.MaxValue).
    /// </summary>
    public int MaxOwners { get; set; }
    
    /// <summary>
    /// Indicates whether the tool is broken and needs repair before use.
    /// </summary>
    public bool IsBroken { get; set; } = false;

    protected ToolItem(string id, string type, OwnershipType ownershipType, double baseDecayPerSecond = 0.01, int maxOwners = 1)
        : base(id, type, baseDecayPerSecond)
    {
        OwnershipType = ownershipType;
        MaxOwners = maxOwners;
        
        if (ownershipType == OwnershipType.Shared)
        {
            MaxOwners = int.MaxValue; // Shared tools can be used by unlimited actors
        }
    }

    /// <summary>
    /// Check if the given actor can use this tool based on ownership rules.
    /// </summary>
    public virtual bool CanActorUseTool(ActorId actorId)
    {
        if (OwnershipType == OwnershipType.Shared)
        {
            return true; // Anyone can use shared tools
        }
        
        // Exclusive tool - only the owner can use it
        return OwnerActorId.HasValue && OwnerActorId.Value == actorId;
    }

    /// <summary>
    /// Virtual method for tools to contribute their own action candidates.
    /// Override in concrete tool implementations to provide tool-specific actions.
    /// </summary>
    public virtual void AddCandidates(Candidates.IslandContext ctx, List<ActionCandidate> output)
    {
        // Default implementation does nothing
        // Concrete tool classes override this to provide their specific candidates
    }

    /// <summary>
    /// Virtual method for tools to apply effects when their actions are executed.
    /// Override in concrete tool implementations to handle tool-specific action effects.
    /// </summary>
    public virtual void ApplyEffects(EffectContext ctx)
    {
        // Default implementation does nothing
        // Concrete tool classes override this to apply their specific effects
    }

    public override Dictionary<string, object> SerializeToDict()
    {
        var dict = base.SerializeToDict();
        dict["OwnershipType"] = OwnershipType.ToString();
        dict["OwnerActorId"] = OwnerActorId?.Value ?? "";
        dict["MaxOwners"] = MaxOwners;
        dict["IsBroken"] = IsBroken;
        return dict;
    }

    public override void DeserializeFromDict(Dictionary<string, JsonElement> data)
    {
        base.DeserializeFromDict(data);
        
        if (data.TryGetValue("OwnershipType", out var ownershipTypeElement))
        {
            OwnershipType = Enum.Parse<OwnershipType>(ownershipTypeElement.GetString()!);
        }
        
        if (data.TryGetValue("OwnerActorId", out var ownerElement))
        {
            var ownerStr = ownerElement.GetString();
            OwnerActorId = string.IsNullOrEmpty(ownerStr) ? null : new ActorId(ownerStr);
        }
        
        if (data.TryGetValue("MaxOwners", out var maxOwnersElement))
        {
            MaxOwners = maxOwnersElement.GetInt32();
        }
        
        if (data.TryGetValue("IsBroken", out var isBrokenElement))
        {
            IsBroken = isBrokenElement.GetBoolean();
        }
    }
}
