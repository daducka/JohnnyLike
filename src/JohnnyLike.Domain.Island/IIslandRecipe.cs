using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;

namespace JohnnyLike.Domain.Island;

/// <summary>
/// A recipe that transforms inputs (consumed via PreAction) into outputs (produced in EffectHandler).
/// Recipes never directly modify actor stats — that is the responsibility of supply consumption actions.
/// </summary>
public interface IIslandRecipe
{
    string Id { get; }

    /// <summary>
    /// Adds crafting action candidates for this recipe when prerequisites are met.
    /// </summary>
    void AddCandidates(IslandContext ctx, List<ActionCandidate> output);
}
