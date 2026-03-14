using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Island.Supply;

namespace JohnnyLike.Domain.Island.Recipes.Definitions;

/// <summary>
/// Recipe: craft a palm frond bed from a blanket, sticks, and rope.
/// Discoverable when the actor's world contains a blanket and there are sticks and rope in supply.
/// Only one bed may exist in the world at a time.
/// The crafting process consumes the existing palm frond blanket.
/// </summary>
public static class PalmFrondBed
{
    public static RecipeDefinition Define()
    {
        var supplyCosts = new List<RecipeSupplyCost>
        {
            RecipeSupplyCost.Of<StickSupply>(4),
            RecipeSupplyCost.Of<RopeSupply>(2)
        };

        return new RecipeDefinition(
            Id: "palm_frond_bed",
            DisplayName: "Build palm frond bed",

            CraftActionId: new ActionId("craft_palm_frond_bed"),

            Location: "camp",

            Duration: Duration.Minutes(70),

            IntrinsicScore: 0.26,

            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Preparation] = 0.90,
                [QualityType.Mastery]     = 0.20,
                [QualityType.Efficiency]  = 0.15,
                [QualityType.Comfort]     = 0.95,
                [QualityType.Safety]      = 0.6
            },

            CanCraft: ctx =>
            {
                // Only one bed allowed.
                if (ctx.World.WorldItems.OfType<PalmFrondBedItem>().Any())
                    return false;

                // Requires a blanket in the world.
                if (!ctx.World.WorldItems.OfType<PalmFrondBlanketItem>().Any())
                    return false;

                var pile = ctx.World.SharedSupplyPile;
                return RecipeDefinition.HasRequiredSupplies(pile, supplyCosts);
            },

            PreAction: effectCtx =>
            {
                if (effectCtx.World.WorldItems.OfType<PalmFrondBedItem>().Any())
                    return false;

                var blanket = effectCtx.World.WorldItems.OfType<PalmFrondBlanketItem>().FirstOrDefault();
                if (blanket == null)
                    return false;

                var pile = effectCtx.World.SharedSupplyPile;
                if (!RecipeDefinition.TryConsumeRequiredSupplies(pile, supplyCosts))
                    return false;

                // Consume the blanket.
                effectCtx.World.WorldItems.Remove(blanket);
                return true;
            },

            Effect: effectCtx =>
            {
                effectCtx.World.AddWorldItem(new PalmFrondBedItem("palm_frond_bed"), "beach");
                var actor = effectCtx.ActorId.Value;
                effectCtx.SetOutcomeNarration($"{actor} constructs a sturdy raised bed from sticks and rope, layering the frond blanket as a mattress.");
            },

            Discovery: new RecipeDiscoverySpec
            {
                Trigger = DiscoveryTrigger.ThinkAboutSupplies,

                CanDiscover = (actor, world) =>
                {
                    // Discoverable when there is a blanket in the world and sticks and rope are available.
                    if (!world.WorldItems.OfType<PalmFrondBlanketItem>().Any())
                        return false;

                    var pile = world.SharedSupplyPile;
                    if (pile == null) return false;

                    return pile.GetQuantity<StickSupply>() >= 1 && pile.GetQuantity<RopeSupply>() >= 1;
                },

                BaseChance = 0.8
            },

            SupplyCosts: supplyCosts
        );
    }
}
