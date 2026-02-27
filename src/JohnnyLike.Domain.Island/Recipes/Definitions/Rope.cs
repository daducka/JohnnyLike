using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Supply;

namespace JohnnyLike.Domain.Island.Recipes.Definitions;

/// <summary>
/// Recipe: braid rope from palm fronds.
/// Costs 1 palm frond and produces 3 rope.
/// </summary>
public static class Rope
{
    public static RecipeDefinition Define()
    {
        var supplyCosts = new List<RecipeSupplyCost>
        {
            RecipeSupplyCost.Of<PalmFrondSupply>(1)
        };

        return new RecipeDefinition(
            Id: "rope",
            DisplayName: "Braid rope",

            CraftActionId: new ActionId("craft_rope"),

            Location: "camp",

            Duration: EngineConstants.TimeToTicks(20.0),

            IntrinsicScore: 0.4,

            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Preparation] = 0.7,
                [QualityType.Mastery]     = 0.4
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

                pile.AddSupply(3, () => new RopeSupply());
            },

            Discovery:  new RecipeDiscoverySpec
            {
                Trigger = DiscoveryTrigger.ThinkAboutSupplies,

                CanDiscover = (actor, world) =>
                {
                    var pile = world.SharedSupplyPile;
                    if (pile == null) return false;

                    if (pile.GetQuantity<PalmFrondSupply>() < 1)
                        return false;

                    return true;
                },

                BaseChance = 0.4
            },

            SupplyCosts: supplyCosts
        );
    }
}
