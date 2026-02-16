using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Domain.Island.Candidates;

public interface IIslandCandidateProvider
{
    void AddCandidates(IslandContext ctx, List<ActionCandidate> output);
}
