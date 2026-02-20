using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;

namespace JohnnyLike.Domain.Island.Recipes;

/// <summary>
/// A fully data-driven definition of a crafting recipe.
/// Each recipe lives in its own file and returns one of these objects.
/// </summary>
public sealed record RecipeDefinition(
    string Id,
    string DisplayName,

    ActionId CraftActionId,

    string Location,

    Func<double, double> DurationSeconds,

    double IntrinsicScore,

    IReadOnlyDictionary<QualityType, double> Qualities,

    Func<IslandContext, bool> CanCraft,

    Func<EffectContext, bool> PreAction,

    Action<EffectContext> Effect,

    RecipeDiscoverySpec? Discovery
);
