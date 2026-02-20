using JohnnyLike.Domain.Island.Candidates;

namespace JohnnyLike.Domain.Island.Recipes;

/// <summary>
/// Specifies the conditions and probability for a recipe to be discovered.
/// </summary>
public sealed class RecipeDiscoverySpec
{
    public required DiscoveryTrigger Trigger { get; init; }

    /// <summary>
    /// Predicate evaluated against the actor and world at discovery time.
    /// Must not use any RNG — determinism is the caller's responsibility.
    /// </summary>
    public required Func<IslandActorState, IslandWorldState, bool> CanDiscover { get; init; }

    public required double BaseChance { get; init; }
}

/// <summary>
/// The action that can trigger recipe discovery.
/// </summary>
public enum DiscoveryTrigger
{
    ThinkAboutSupplies
}
