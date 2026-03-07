using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Supply;

namespace JohnnyLike.Domain.Island.Recipes.Definitions;

/// <summary>
/// Recipe: craft fishing bait from carcass scraps.
/// Discoverable when carcass scraps are available in the supply pile.
/// </summary>
public static class Bait
{
    public static RecipeDefinition Define()
    {
        var supplyCosts = new List<RecipeSupplyCost>
        {
            RecipeSupplyCost.Of<CarcassScrapsSupply>(2)
        };

        return new RecipeDefinition(
            Id: "bait",
            DisplayName: "Craft fishing bait",

            CraftActionId: new ActionId("craft_bait"),

            Location: "camp",

            Duration: EngineConstants.TimeToTicks(10.0),

            IntrinsicScore: 0.5,

            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Preparation] = 0.7,
                [QualityType.Efficiency]  = 0.5
            },

            CanCraft: ctx =>
            {
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
                var pile = effectCtx.World.SharedSupplyPile;
                if (pile == null) return;

                pile.AddSupply(1.0, () => new BaitSupply());
                var actor = effectCtx.ActorId.Value;
                effectCtx.SetOutcomeNarration($"{actor} fashions some fish scraps into usable fishing bait.");
            },

            Discovery: new RecipeDiscoverySpec
            {
                Trigger = DiscoveryTrigger.ThinkAboutSupplies,

                CanDiscover = (actor, world) =>
                {
                    var pile = world.SharedSupplyPile;
                    if (pile == null) return false;
                    return pile.GetQuantity<CarcassScrapsSupply>() >= 1.0;
                },

                BaseChance = 0.4
            },

            SupplyCosts: supplyCosts
        );
    }
}
