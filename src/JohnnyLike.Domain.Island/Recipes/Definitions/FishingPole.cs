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

            Duration: 40.0,

            IntrinsicScore: 0.7,

            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Preparation] = 0.8,
                [QualityType.Mastery]     = 0.6,
                [QualityType.Efficiency]  = 0.3
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
                effectCtx.World.WorldItems.Add(new FishingPoleItem(poleId, effectCtx.ActorId));
            },

            Discovery: new RecipeDiscoverySpec
            {
                Trigger = DiscoveryTrigger.ThinkAboutSupplies,

                CanDiscover = (actor, world) => actor.Satiety < 50.0,

                BaseChance = 1.0
            },

            SupplyCosts: supplyCosts
        );
    }
}
