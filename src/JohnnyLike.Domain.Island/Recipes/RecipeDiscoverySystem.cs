using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Telemetry;

namespace JohnnyLike.Domain.Island.Recipes;

/// <summary>
/// Handles deterministic recipe discovery for actors.
/// </summary>
public static class RecipeDiscoverySystem
{
    /// <summary>
    /// Collects all unknown recipes matching the trigger and discoverability conditions,
    /// applies each recipe's discovery base chance as a gate, then scores the remaining
    /// candidates using the same quality-weighting model as action candidates and discovers
    /// the top-scoring recipe.
    /// </summary>
    public static void TryDiscover(
        IslandActorState actor,
        IslandWorldState world,
        IRngStream rng,
        DiscoveryTrigger trigger,
        string? actorId = null)
    {
        var discoverableRecipes = new List<(string Id, RecipeDefinition Recipe)>();

        foreach (var (id, recipe) in IslandRecipeRegistry.All)
        {
            if (recipe.Discovery == null || recipe.Discovery.Trigger != trigger)
                continue;

            if (actor.KnownRecipeIds.Contains(id))
                continue;

            if (!recipe.Discovery.CanDiscover(actor, world))
                continue;

            if (rng.NextDouble() >= recipe.Discovery.BaseChance)
                continue;

            discoverableRecipes.Add((id, recipe));
        }

        if (discoverableRecipes.Count == 0)
            return;

        var topRecipe = discoverableRecipes
            .Select(x => new
            {
                x.Id,
                x.Recipe,
                Score = IslandDomainPack.ScoreByQualities(actor, x.Recipe.IntrinsicScore, x.Recipe.Qualities)
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Id, StringComparer.Ordinal)
            .First();

        actor.KnownRecipeIds.Add(topRecipe.Id);

        // Emit a narration beat for the discovery
        var tracer = world.Tracer;
        using (tracer.PushPhase(TracePhase.ActionCompleted))
        {
            var actorLabel = actorId ?? "Someone";
            // Use recipe-specific beat text if provided; otherwise fall back to a generic one.
            var text = topRecipe.Recipe.Discovery?.DiscoveryBeatText?.Invoke(actorLabel)
                ?? $"{actorLabel} realizes how to make a {topRecipe.Id.Replace('_', ' ')}.";
            tracer.Beat(
                text,
                subjectId: $"recipe:{topRecipe.Id}",
                priority: 70,
                actorId: actorId);
        }
    }
}
