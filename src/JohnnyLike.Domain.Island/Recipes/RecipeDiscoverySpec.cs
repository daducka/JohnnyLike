using JohnnyLike.Domain.Island.Candidates;

namespace JohnnyLike.Domain.Island.Recipes;

/// <summary>
/// Specifies the conditions and probability for a recipe to be discovered.
/// </summary>
public sealed class RecipeDiscoverySpec
{
    public required DiscoveryTrigger Trigger { get; init; }
    public required Func<IslandContext, bool> CanDiscover { get; init; }
    public required double BaseChance { get; init; }
}

/// <summary>
/// The action that can trigger recipe discovery.
/// </summary>
public enum DiscoveryTrigger
{
    ThinkAboutSupplies
}
