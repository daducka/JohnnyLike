using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Island.Supply;

namespace JohnnyLike.Domain.Island.Recipes.Definitions;

/// <summary>
/// Recipe: weave a fishing net from rope.
/// Requires significant rope investment and is unlocked by catching enough fish,
/// making it a meaningful mid-game upgrade.
/// </summary>
public static class FishingNetRecipe
{
    /// <summary>
    /// Number of fish that must have been caught (tracked in <see cref="IslandMetrics.FishCaught"/>)
    /// before this recipe can be discovered.
    /// </summary>
    public const int FishCaughtToDiscoverFishingNet = 3;

    public static RecipeDefinition Define()
    {
        var supplyCosts = new List<RecipeSupplyCost>
        {
            RecipeSupplyCost.Of<RopeSupply>(6)
        };

        return new RecipeDefinition(
            Id: "fishing_net",
            DisplayName: "Weave fishing net",

            CraftActionId: new ActionId("weave_fishing_net"),

            Location: "camp",

            Duration: Duration.Minutes(60),

            IntrinsicScore: 0.24,

            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Preparation]     = 1.10,
                [QualityType.FoodAcquisition] = 1.00,
                [QualityType.Mastery]         = 0.70,
                [QualityType.Efficiency]      = 0.50
            },

            CanCraft: ctx =>
            {
                // Only one fishing net in the world at a time.
                if (ctx.World.WorldItems.OfType<FishingNetItem>().Any())
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
                var net = new FishingNetItem("fishing_net");
                effectCtx.World.AddWorldItem(net, effectCtx.Actor.CurrentRoomId);
                var actor = effectCtx.ActorId.Value;
                effectCtx.SetOutcomeNarration($"{actor} carefully weaves a large fishing net from rope — it should catch far more fish than a pole.");
            },

            Discovery: new RecipeDiscoverySpec
            {
                Trigger = DiscoveryTrigger.ThinkAboutSupplies,

                CanDiscover = (actor, world) =>
                    world.Metrics.FishCaught >= FishCaughtToDiscoverFishingNet,

                BaseChance = 0.85,
                DiscoveryBeatText = actorName =>
                    $"{actorName} has caught enough fish to realize a net would be far more efficient than a pole."
            },

            SupplyCosts: supplyCosts
        );
    }
}
