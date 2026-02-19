using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;

namespace JohnnyLike.Domain.Island;

/// <summary>
/// Island-specific action candidate interface.
/// Objects implementing this interface can provide action candidates.
/// Effects are defined inline when creating ActionCandidate instances via the EffectHandler parameter.
/// </summary>
public interface IIslandActionCandidate : IActionCandidate
{
    /// <summary>
    /// Add action candidates to the output list based on the current context.
    /// </summary>
    /// <param name="ctx">Island context containing actor state, world state, and other relevant information</param>
    /// <param name="output">List to add candidates to</param>
    void AddCandidates(IslandContext ctx, List<ActionCandidate> output);
}
