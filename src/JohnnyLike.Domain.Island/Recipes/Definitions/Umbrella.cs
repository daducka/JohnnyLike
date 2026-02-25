using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Island.Supply;

namespace JohnnyLike.Domain.Island.Recipes.Definitions;

/// <summary>
/// Recipe: craft an umbrella from sticks and palm fronds.
/// Discoverable when it is cold and both materials are available.
/// </summary>
public static class Umbrella
{
    public static RecipeDefinition Define()
    {
        var supplyCosts = new List<RecipeSupplyCost>
        {
            RecipeSupplyCost.Of<StickSupply>(2),
            RecipeSupplyCost.Of<PalmFrondSupply>(3)
        };

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
                return RecipeDefinition.HasRequiredSupplies(pile, supplyCosts);
            },

            PreAction: effectCtx =>
            {
                var pile = effectCtx.World.SharedSupplyPile;
                return RecipeDefinition.TryConsumeRequiredSupplies(pile, supplyCosts);
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
                    var weather = world.GetItem<WeatherItem>("weather");

                    if (weather?.Precipitation != PrecipitationBand.Rainy)
                        return false;

                    var pile = world.SharedSupplyPile;
                    if (pile == null) return false;

                    return
                        pile.GetQuantity<StickSupply>() > 0 &&
                        pile.GetQuantity<PalmFrondSupply>() > 0;
                },

                BaseChance = 0.25,

                DiscoveryBeatText = actorName =>
                    $"{actorName} realizes that an umbrella might be useful now and considers how they could build one."
            },

            SupplyCosts: supplyCosts
        );
    }
}
