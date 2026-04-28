using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Loot;
using System.Text.Json;

namespace JohnnyLike.Domain.Island.Items;

/// <summary>
/// A one-shot interactive world item that offers an investigation action.
/// When the actor completes the action the configured <see cref="LootDefinition"/> drops
/// are applied, narration is emitted, and the item removes itself from the world.
/// Only one actor can consume a given loot item at a time (resource reservation).
/// </summary>
public class LootItem : WorldItem, IIslandActionCandidate
{
    public LootKind Kind { get; set; }
    public bool IsConsumed { get; set; } = false;

    public LootItem(string id, LootKind kind) : base(id, "loot_item")
    {
        Kind = kind;
    }

    /// <summary>Parameterless-id constructor for deserialization only.</summary>
    public LootItem(string id) : base(id, "loot_item") { }

    public void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        if (IsConsumed)
            return;

        var def = IslandLootRegistry.Get(Kind);
        var lootResource = new ResourceId($"loot:{Id}");

        if (ctx.ResourceAvailability.IsReserved(lootResource))
            return;

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId(def.ActionId),
                ActionKind.Interact,
                EmptyActionParameters.Instance,
                def.Duration,
                def.ActionNarrationDescription,
                null,
                new List<ResourceRequirement> { new ResourceRequirement(lootResource) }
            ),
            def.IntrinsicScore,
            Reason: $"Investigate {def.DisplayName}",
            EffectHandler: new Action<EffectContext>(effectCtx =>
            {
                if (IsConsumed)
                    return;

                IsConsumed = true;

                var actorName = effectCtx.ActorId.Value;
                var narration = def.SuccessNarration.Replace("{actor}", actorName);

                var sharedPile = effectCtx.World.SharedSupplyPile;
                foreach (var drop in def.Drops)
                {
                    switch (drop)
                    {
                        case SupplyLootDrop supplyDrop:
                            if (sharedPile != null)
                            {
                                var supply = IslandSupplyFactory.Create(supplyDrop.SupplyKind);
                                sharedPile.AddSupplyItem(supply, supplyDrop.Quantity);
                            }
                            break;

                        case WorldItemLootDrop worldDrop:
                            var newItem = worldDrop.Factory(worldDrop.ItemId);
                            var roomId = effectCtx.World.GetItemRoomId(Id) ?? "beach";
                            effectCtx.World.AddWorldItem(newItem, roomId);
                            break;
                    }
                }

                effectCtx.SetOutcomeNarration(narration);
                effectCtx.World.RemoveWorldItem(Id);
            }),
            Qualities: new Dictionary<QualityType, double>(def.Qualities),
            ActorRequirement: actor => CandidateRequirements.IsHuman(actor) && CandidateRequirements.AliveOnly(actor)
        ));
    }

    public override Dictionary<string, object> SerializeToDict()
    {
        var dict = base.SerializeToDict();
        dict["Kind"] = Kind.ToString();
        dict["IsConsumed"] = IsConsumed;
        return dict;
    }

    public override void DeserializeFromDict(Dictionary<string, JsonElement> data)
    {
        base.DeserializeFromDict(data);
        if (data.TryGetValue("Kind", out var kindEl) &&
            Enum.TryParse<LootKind>(kindEl.GetString(), out var kind))
            Kind = kind;
        if (data.TryGetValue("IsConsumed", out var consumed))
            IsConsumed = consumed.GetBoolean();
    }
}
