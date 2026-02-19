using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;

namespace JohnnyLike.Domain.Island.Supply;

/// <summary>
/// Implemented by supply items that can provide action candidates from within a SupplyPile.
/// </summary>
public interface ISupplyActionCandidate
{
    void AddCandidates(IslandContext ctx, SupplyPile pile, List<ActionCandidate> output);
}
