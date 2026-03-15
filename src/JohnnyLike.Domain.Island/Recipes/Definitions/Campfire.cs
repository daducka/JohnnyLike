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
        var supplyCosts = new List<RecipeSupplyCost>
        {
            RecipeSupplyCost.Of<StickSupply>(5),
            RecipeSupplyCost.Of<WoodSupply>(3),
            RecipeSupplyCost.Of<RocksSupply>(6)
        };

        return new RecipeDefinition(
            Id: "campfire",
            DisplayName: "Build campfire",

            CraftActionId: new ActionId("craft_campfire"),

            Location: "camp",

            Duration: Duration.Hours(1),

            IntrinsicScore: 0.28,

            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Preparation] = 1.00,
                [QualityType.Mastery]     = 0.20,
                [QualityType.Efficiency]  = 0.15,
                [QualityType.Safety]      = 0.8,
                [QualityType.Comfort]     = 0.55
            },

            CanCraft: ctx =>
            {
                // Only one campfire per sim.
                if (ctx.World.WorldItems.OfType<CampfireItem>().Any())
                    return false;
                    
                var weather = ctx.World.GetItem<WeatherItem>("weather");
                if (weather?.Temperature != TemperatureBand.Cold) return false;

                var pile = ctx.World.SharedSupplyPile;
                return RecipeDefinition.HasRequiredSupplies(pile, supplyCosts);
            },

            PreAction: effectCtx =>
            {
                var weather = effectCtx.World.GetItem<WeatherItem>("weather");
                if (weather?.Temperature != TemperatureBand.Cold) return false;

                var pile = effectCtx.World.SharedSupplyPile;
                return RecipeDefinition.TryConsumeRequiredSupplies(pile, supplyCosts);
            },

            Effect: effectCtx =>
            {
                effectCtx.World.AddWorldItem(new CampfireItem("main_campfire"), "beach");
                var actor = effectCtx.ActorId.Value;
                effectCtx.SetOutcomeNarration($"{actor} assembles stones and logs into a functional campfire.");
            },

            Discovery: new RecipeDiscoverySpec
            {
                Trigger = DiscoveryTrigger.ThinkAboutSupplies,

                CanDiscover = (actor, world) =>
                {
                    var weather = world.GetItem<WeatherItem>("weather");
                    return weather?.Temperature == TemperatureBand.Cold;
                },

                BaseChance = 0.9,

                DiscoveryBeatText = actorName =>
                                    $"{actorName} realizes that building a campfire would help them stay warm and cook food during cold periods."

            },

            SupplyCosts: supplyCosts
        );
    }
}
