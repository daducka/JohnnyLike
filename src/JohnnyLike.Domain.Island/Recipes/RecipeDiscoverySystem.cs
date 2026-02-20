using JohnnyLike.Domain.Island.Candidates;

namespace JohnnyLike.Domain.Island.Recipes;

/// <summary>
/// Handles deterministic recipe discovery for actors.
/// </summary>
public static class RecipeDiscoverySystem
{
    /// <summary>
    /// Iterates all recipes and attempts to discover unknown ones that match the trigger.
    /// Uses the context's RNG for deterministic rolls.
    /// </summary>
    public static void TryDiscover(IslandContext ctx, DiscoveryTrigger trigger)
    {
        foreach (var (id, recipe) in IslandRecipeRegistry.All)
        {
            if (recipe.Discovery == null || recipe.Discovery.Trigger != trigger)
                continue;

            if (ctx.Actor.KnownRecipeIds.Contains(id))
                continue;

            if (!recipe.Discovery.CanDiscover(ctx))
                continue;

            var roll = ctx.Random.NextDouble();
            if (roll < recipe.Discovery.BaseChance)
            {
                ctx.Actor.KnownRecipeIds.Add(id);
            }
        }
    }
}
