using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Domain.Island.Recipes;

/// <summary>
/// Handles deterministic recipe discovery for actors.
/// </summary>
public static class RecipeDiscoverySystem
{
    /// <summary>
    /// Iterates all recipes and attempts to discover unknown ones that match the trigger.
    /// Uses the provided <paramref name="rng"/> for deterministic rolls — callers must pass
    /// the RNG active at the moment of effect execution (e.g. <c>effectCtx.Rng</c>), not a
    /// context captured at candidate-generation time.  This ensures reproducible discovery
    /// outcomes when replaying from the same game state.
    /// </summary>
    public static void TryDiscover(
        IslandActorState actor,
        IslandWorldState world,
        IRngStream rng,
        DiscoveryTrigger trigger)
    {
        foreach (var (id, recipe) in IslandRecipeRegistry.All)
        {
            if (recipe.Discovery == null || recipe.Discovery.Trigger != trigger)
                continue;

            if (actor.KnownRecipeIds.Contains(id))
                continue;

            if (!recipe.Discovery.CanDiscover(actor, world))
                continue;

            if (rng.NextDouble() < recipe.Discovery.BaseChance)
            {
                actor.KnownRecipeIds.Add(id);
            }
        }
    }
}
