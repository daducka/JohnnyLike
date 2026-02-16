using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Domain.Island.Candidates;

[IslandCandidateProvider(100, "sleep_under_tree")]
public class SleepCandidateProvider : IIslandCandidateProvider
{
    public void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        var baseScore = 0.4;
        if (ctx.Actor.Energy < 30.0)
            baseScore = 1.2;
        else if (ctx.Actor.Energy < 50.0)
            baseScore = 0.8;

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("sleep_under_tree"),
                ActionKind.Interact,
                new LocationActionParameters("tree"),
                30.0 + ctx.Rng.NextDouble() * 10.0
            ),
            baseScore,
            "Sleep under tree"
        ));
    }

    public void ApplyEffects(EffectContext ctx)
    {
        ctx.Actor.Energy = Math.Min(100.0, ctx.Actor.Energy + 40.0);
        ctx.Actor.Boredom = Math.Max(0.0, ctx.Actor.Boredom - 5.0);
    }
}
