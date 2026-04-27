using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Island.Supply;

namespace JohnnyLike.Domain.Island.Recipes.Definitions;

/// <summary>
/// Recipe: build a crab trap from sticks and rope.
/// Discoverable only when CrabSupply exists in the shared pile — meaning the actor
/// has already caught a crab and knows crabs are catchable.
/// </summary>
public static class CrabTrap
{
    public static RecipeDefinition Define()
    {
        var supplyCosts = new List<RecipeSupplyCost>
        {
            RecipeSupplyCost.Of<StickSupply>(4),
            RecipeSupplyCost.Of<RopeSupply>(3)
        };

        return new RecipeDefinition(
            Id: "crab_trap",
            DisplayName: "Build crab trap",

            CraftActionId: new ActionId("build_crab_trap"),

            Location: "camp",

            Duration: Duration.Minutes(50),

            IntrinsicScore: 0.22,

            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Preparation]     = 1.00,
                [QualityType.FoodAcquisition] = 0.80,
                [QualityType.Mastery]         = 0.60,
                [QualityType.Efficiency]      = 0.40
            },

            CanCraft: ctx =>
            {
                // Only one crab trap in the world at a time.
                if (ctx.World.WorldItems.OfType<CrabTrapItem>().Any())
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
                var trap = new CrabTrapItem("crab_trap");
                effectCtx.World.AddWorldItem(trap, effectCtx.Actor.CurrentRoomId);
                var actor = effectCtx.ActorId.Value;
                effectCtx.SetOutcomeNarration($"{actor} weaves sticks and rope into a sturdy crab trap.");
            },

            Discovery: new RecipeDiscoverySpec
            {
                Trigger = DiscoveryTrigger.ThinkAboutSupplies,

                CanDiscover = (actor, world) =>
                {
                    // CrabSupply must exist in the shared pile (not just a crab actor being present).
                    var pile = world.SharedSupplyPile;
                    return pile != null && pile.GetQuantity<CrabSupply>() > 0;
                },

                BaseChance = 0.9,
                DiscoveryBeatText = actorName =>
                    $"{actorName} stares at the crab and wonders if they could build a trap to catch more without chasing them down."
            },

            SupplyCosts: supplyCosts
        );
    }
}
