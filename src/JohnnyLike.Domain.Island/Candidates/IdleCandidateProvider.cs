using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Domain.Island.Candidates;

[IslandCandidateProvider(9999)]
public class IdleCandidateProvider : IIslandCandidateProvider
{
    public void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        // Idle must ALWAYS be a candidate with a low baseline score
        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("idle"),
                ActionKind.Wait,
                EmptyActionParameters.Instance,
                5.0
            ),
            0.3,
            "Idle"
        ));
    }
}
