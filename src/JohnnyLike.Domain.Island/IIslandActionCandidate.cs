using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;

namespace JohnnyLike.Domain.Island;

/// <summary>
/// Island-specific action candidate interface.
/// Objects implementing this interface can provide action candidates and apply their effects
/// within the Island domain.
/// </summary>
public interface IIslandActionCandidate : IActionCandidate
{
    /// <summary>
    /// Add action candidates to the output list based on the current context.
    /// </summary>
    /// <param name="ctx">Island context containing actor state, world state, and other relevant information</param>
    /// <param name="output">List to add candidates to</param>
    void AddCandidates(IslandContext ctx, List<ActionCandidate> output);
    
    /// <summary>
    /// Apply the effects of a completed action.
    /// This is called after an action has been executed successfully.
    /// </summary>
    /// <param name="ctx">Effect context containing the outcome and state to modify</param>
    void ApplyEffects(EffectContext ctx);
}
