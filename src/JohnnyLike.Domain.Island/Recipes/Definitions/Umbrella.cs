using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Island.Stats;
using JohnnyLike.Domain.Island.Supply;

namespace JohnnyLike.Domain.Island.Recipes.Definitions;

/// <summary>
/// Recipe: craft an umbrella from sticks and palm fronds.
/// Discoverable when it is raining and both materials are available.
/// </summary>
public static class Umbrella
{
    public static RecipeDefinition Define()
    {
        return new RecipeDefinition(
            Id: "umbrella",
            DisplayName: "Craft umbrella",

            CraftActionId: new ActionId("craft_umbrella"),

            Location: "camp",

            Duration: 30.0,

            IntrinsicScore: 0.5,

            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Preparation] = 0.8,
                [QualityType.Comfort]     = 0.7,
                [QualityType.Safety]      = 0.5
            },

            CanCraft: ctx =>
            {
                // An actor can only own one umbrella at a time
                if (ctx.World.WorldItems.OfType<UmbrellaItem>()
                        .Any(u => u.OwnerActorId == ctx.ActorId))
                    return false;

                var pile = ctx.World.SharedSupplyPile;
                if (pile == null) return false;

                if (pile.GetQuantity<StickSupply>("stick") < 2)
                    return false;

                if (pile.GetQuantity<PalmFrondSupply>("palm_frond") < 3)
                    return false;

                return true;
            },

            PreAction: effectCtx =>
            {
                var pile = effectCtx.World.SharedSupplyPile;
                if (pile == null) return false;

                return
                    pile.TryConsumeSupply<StickSupply>("stick", 2)
                 && pile.TryConsumeSupply<PalmFrondSupply>("palm_frond", 3);
            },

            Effect: effectCtx =>
            {
                var toolId = $"umbrella_{effectCtx.ActorId.Value}";
                effectCtx.World.WorldItems.Add(new UmbrellaItem(toolId, effectCtx.ActorId));
            },

            Discovery: new RecipeDiscoverySpec
            {
                Trigger = DiscoveryTrigger.ThinkAboutSupplies,

                CanDiscover = (actor, world) =>
                {
                    var weather = world.GetStat<WeatherStat>("weather");

                    if (weather?.Weather != Weather.Rainy)
                        return false;

                    var pile = world.SharedSupplyPile;
                    if (pile == null) return false;

                    return
                        pile.GetQuantity<StickSupply>("stick") > 0 &&
                        pile.GetQuantity<PalmFrondSupply>("palm_frond") > 0;
                },

                BaseChance = 0.25
            }
        );
    }
}
