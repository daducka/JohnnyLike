using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Island.Supply;

namespace JohnnyLike.Domain.Island.Recipes.Definitions;

/// <summary>
/// Recipe: craft a campfire. Discoverable only when temperature is cold.
/// Requires 5 sticks, 3 wood, 6 rocks.
/// </summary>
public static class Campfire
{
    public static RecipeDefinition Define()
    {
        return new RecipeDefinition(
            Id: "campfire",
            DisplayName: "Build campfire",

            CraftActionId: new ActionId("craft_campfire"),

            Location: "camp",

            Duration: 60.0,

            IntrinsicScore: 0.8,

            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Preparation] = 1.0,
                [QualityType.Safety]      = 0.8,
                [QualityType.Comfort]     = 0.6
            },

            CanCraft: ctx =>
            {
                var weather = ctx.World.GetItem<WeatherItem>("weather");
                if (weather?.Temperature != TemperatureBand.Cold) return false;

                var pile = ctx.World.SharedSupplyPile;
                if (pile == null) return false;

                if (pile.GetQuantity<StickSupply>("sticks") < 5) return false;
                if (pile.GetQuantity<WoodSupply>("wood") < 3) return false;
                if (pile.GetQuantity<RocksSupply>("rocks") < 6) return false;

                return true;
            },

            PreAction: effectCtx =>
            {
                var weather = effectCtx.World.GetItem<WeatherItem>("weather");
                if (weather?.Temperature != TemperatureBand.Cold) return false;

                var pile = effectCtx.World.SharedSupplyPile;
                if (pile == null) return false;

                return
                    pile.TryConsumeSupply<StickSupply>("sticks", 5)
                 && pile.TryConsumeSupply<WoodSupply>("wood", 3)
                 && pile.TryConsumeSupply<RocksSupply>("rocks", 6);
            },

            Effect: effectCtx =>
            {
                effectCtx.World.WorldItems.Add(new CampfireItem("main_campfire"));
            },

            Discovery: new RecipeDiscoverySpec
            {
                Trigger = DiscoveryTrigger.ThinkAboutSupplies,

                CanDiscover = (actor, world) =>
                {
                    var weather = world.GetItem<WeatherItem>("weather");
                    return weather?.Temperature == TemperatureBand.Cold;
                },

                BaseChance = 0.3
            }
        );
    }
}
