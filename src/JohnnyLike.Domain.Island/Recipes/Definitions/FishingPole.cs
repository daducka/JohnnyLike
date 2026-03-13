using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Island.Supply;

namespace JohnnyLike.Domain.Island.Recipes.Definitions;

/// <summary>
/// Recipe: craft a fishing pole from sticks and rope.
/// Costs 3 sticks and 2 rope; creates a fishing pole owned by the crafting actor.
/// </summary>
public static class FishingPole
{
    public static RecipeDefinition Define()
    {
        var supplyCosts = new List<RecipeSupplyCost>
        {
            RecipeSupplyCost.Of<StickSupply>(3),
            RecipeSupplyCost.Of<RopeSupply>(2)
        };

        return new RecipeDefinition(
            Id: "fishing_pole",
            DisplayName: "Craft fishing pole",

            CraftActionId: new ActionId("craft_fishing_pole"),

            Location: "camp",

            Duration: Duration.Minutes(40),

            IntrinsicScore: 0.24,

            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Preparation] = 1.00,
                [QualityType.Mastery]     = 0.80,
                [QualityType.Efficiency]  = 0.40,
                [QualityType.Comfort]     = -0.05
            },

            CanCraft: ctx =>
            {
                // Only one owned fishing pole per actor.
                if (ctx.World.WorldItems.OfType<FishingPoleItem>()
                        .Any(p => p.OwnerActorId == ctx.ActorId))
                    return false;

                var pile = ctx.World.SharedSupplyPile;
                return RecipeDefinition.HasRequiredSupplies(pile, supplyCosts);
            },

            PreAction: effectCtx =>
            {
                var pile = effectCtx.World.SharedSupplyPile;
                return RecipeDefinition.TryConsumeRequiredSupplies(pile, supplyCosts);
            },

            Effect: effectCtx =>
            {
                var existingForActor = effectCtx.World.WorldItems
                    .OfType<FishingPoleItem>()
                    .Count(p => p.OwnerActorId == effectCtx.ActorId);

                var poleId = $"fishing_pole_{effectCtx.ActorId.Value}_{existingForActor + 1}";
                effectCtx.World.AddWorldItem(new FishingPoleItem(poleId, effectCtx.ActorId), "beach");
                var actor = effectCtx.ActorId.Value;
                effectCtx.SetOutcomeNarration($"{actor} carves and assembles a durable fishing pole.");
            },

            Discovery: new RecipeDiscoverySpec
            {
                Trigger = DiscoveryTrigger.ThinkAboutSupplies,

                CanDiscover = (actor, world) =>
                {
                    var pile = world.SharedSupplyPile;
                    if (pile == null) return false;

                    return
                        pile.GetQuantity<StickSupply>() > 0 &&
                        pile.GetQuantity<RopeSupply>() > 0;
                },

                BaseChance = 1.0
            },

            SupplyCosts: supplyCosts
        );
    }
}
