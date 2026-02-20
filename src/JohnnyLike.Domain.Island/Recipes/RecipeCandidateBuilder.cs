using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;

namespace JohnnyLike.Domain.Island.Recipes;

/// <summary>
/// Builds ActionCandidates from RecipeDefinitions generically.
/// </summary>
public static class RecipeCandidateBuilder
{
    public static void AddCandidate(
        RecipeDefinition recipe,
        IslandContext ctx,
        List<ActionCandidate> output)
    {
        if (!recipe.CanCraft(ctx))
            return;

        var r = ctx.Random.NextDouble();
        var duration = recipe.DurationSeconds(r);

        output.Add(new ActionCandidate(
            new ActionSpec(
                recipe.CraftActionId,
                ActionKind.Interact,
                new LocationActionParameters(recipe.Location),
                duration
            ),
            recipe.IntrinsicScore,
            recipe.DisplayName,
            EffectHandler: recipe.Effect,
            PreAction: recipe.PreAction,
            Qualities: recipe.Qualities.ToDictionary(x => x.Key, x => x.Value)
        ));
    }
}
