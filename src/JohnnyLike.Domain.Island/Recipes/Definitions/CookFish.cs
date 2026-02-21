using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Island.Supply;

namespace JohnnyLike.Domain.Island.Recipes.Definitions;

/// <summary>
/// Recipe: cook raw fish over a lit campfire to produce cooked fish.
/// Discoverable when fish is in the supply pile and the campfire is lit.
/// </summary>
public static class CookFish
{
    public static RecipeDefinition Define()
    {
        return new RecipeDefinition(
            Id: "cook_fish",
            DisplayName: "Cook fish over campfire",

            CraftActionId: new ActionId("cook_fish"),

            Location: "campfire",

            Duration: 25.0,

            IntrinsicScore: 0.4,

            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Preparation] = 0.8,
                [QualityType.Efficiency]  = 0.6
            },

            CanCraft: ctx =>
            {
                var pile = ctx.World.SharedSupplyPile;
                if (pile == null) return false;

                if (pile.GetQuantity<FishSupply>() < 1)
                    return false;

                var campfire = ctx.World.MainCampfire;
                if (campfire == null || !campfire.IsLit)
                    return false;

                return true;
            },

            PreAction: effectCtx =>
            {
                var pile = effectCtx.World.SharedSupplyPile;
                return pile != null &&
                      pile.TryConsumeSupply<FishSupply>(1);
            },

            Effect: effectCtx =>
            {
                var pile = effectCtx.World.SharedSupplyPile;
                if (pile == null) return;

                pile.AddSupply(1, () => new CookedFishSupply());
            },

            Discovery: new RecipeDiscoverySpec
            {
                Trigger = DiscoveryTrigger.ThinkAboutSupplies,

                CanDiscover = (actor, world) =>
                {
                    var pile = world.SharedSupplyPile;
                    if (pile == null) return false;

                    if (pile.GetQuantity<FishSupply>() < 1)
                        return false;

                    var campfire = world.MainCampfire;
                    return campfire != null && campfire.IsLit;
                },

                BaseChance = 0.3
            }
        );
    }
}
