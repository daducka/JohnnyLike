using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Island.Supply;

namespace JohnnyLike.Domain.Island.Recipes.Definitions;

/// <summary>
/// Recipe: craft a palm frond blanket from palm fronds.
/// Discoverable when there are palm fronds in the shared supply pile.
/// Only one blanket may exist in the world at a time.
/// </summary>
public static class PalmFrondBlanket
{
    public static RecipeDefinition Define()
    {
        var supplyCosts = new List<RecipeSupplyCost>
        {
            RecipeSupplyCost.Of<PalmFrondSupply>(5)
        };

        return new RecipeDefinition(
            Id: "palm_frond_blanket",
            DisplayName: "Weave palm frond blanket",

            CraftActionId: new ActionId("craft_palm_frond_blanket"),

            Location: "camp",

            Duration: EngineConstants.TimeToTicks(50.0),

            IntrinsicScore: 0.7,

            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Comfort]     = 1.0,
                [QualityType.Safety]      = 0.5,
                [QualityType.Preparation] = 0.6
            },

            CanCraft: ctx =>
            {
                // Only one blanket allowed.
                if (ctx.World.WorldItems.OfType<PalmFrondBlanketItem>().Any())
                    return false;

                var pile = ctx.World.SharedSupplyPile;
                return RecipeDefinition.HasRequiredSupplies(pile, supplyCosts);
            },

            PreAction: effectCtx =>
            {
                if (effectCtx.World.WorldItems.OfType<PalmFrondBlanketItem>().Any())
                    return false;

                var pile = effectCtx.World.SharedSupplyPile;
                return RecipeDefinition.TryConsumeRequiredSupplies(pile, supplyCosts);
            },

            Effect: effectCtx =>
            {
                effectCtx.World.AddWorldItem(new PalmFrondBlanketItem("palm_frond_blanket"), "beach");
                var actor = effectCtx.ActorId.Value;
                effectCtx.SetOutcomeNarration($"{actor} weaves palm fronds into a soft, warm blanket.");
            },

            Discovery: new RecipeDiscoverySpec
            {
                Trigger = DiscoveryTrigger.ThinkAboutSupplies,

                CanDiscover = (actor, world) =>
                {
                    // Discoverable when there are palm fronds available.
                    var pile = world.SharedSupplyPile;
                    return pile != null && pile.GetQuantity<PalmFrondSupply>() > 0;
                },

                BaseChance = 0.7
            },

            SupplyCosts: supplyCosts
        );
    }
}
