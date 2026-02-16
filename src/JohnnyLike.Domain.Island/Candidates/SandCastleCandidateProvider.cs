using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Candidates;

[IslandCandidateProvider(400)]
public class SandCastleCandidateProvider : IIslandCandidateProvider
{
    public void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        var baseDC = 8;

        if (ctx.World.TideLevel == TideLevel.High)
            baseDC += 4;

        var modifier = ctx.Actor.GetSkillModifier("Performance");
        var advantage = ctx.Actor.GetAdvantage("Performance");

        var estimatedChance = DndMath.EstimateSuccessChanceD20(baseDC, modifier, advantage);

        var baseScore = 0.3 + (ctx.Actor.Boredom / 100.0);
        baseScore *= estimatedChance;

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("build_sand_castle"),
                ActionKind.Interact,
                new Dictionary<string, object>
                {
                    ["dc"] = baseDC,
                    ["modifier"] = modifier,
                    ["advantage"] = advantage.ToString(),
                    ["location"] = "beach"
                },
                20.0 + ctx.Rng.NextDouble() * 10.0
            ),
            baseScore,
            $"Build sand castle (DC {baseDC}, {estimatedChance:P0} chance)"
        ));
    }
}
