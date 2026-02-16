using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Candidates;

[IslandCandidateProvider(410)]
public class SwimCandidateProvider : IIslandCandidateProvider
{
    public void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        if (ctx.Actor.Energy < 20.0)
            return;

        var baseDC = 10;

        if (ctx.World.Weather == Weather.Windy)
            baseDC += 3;
        else if (ctx.World.Weather == Weather.Rainy)
            baseDC += 1;

        var modifier = ctx.Actor.GetSkillModifier("Survival");
        var advantage = ctx.Actor.GetAdvantage("Survival");

        var estimatedChance = DndMath.EstimateSuccessChanceD20(baseDC, modifier, advantage);

        var baseScore = 0.35 + (ctx.Actor.Morale < 30 ? 0.2 : 0.0);
        baseScore *= estimatedChance;

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("swim"),
                ActionKind.Interact,
                new()
                {
                    ["dc"] = baseDC,
                    ["modifier"] = modifier,
                    ["advantage"] = advantage.ToString(),
                    ["location"] = "water"
                },
                15.0 + ctx.Rng.NextDouble() * 5.0
            ),
            baseScore,
            $"Swim (DC {baseDC}, {estimatedChance:P0} chance)"
        ));
    }
}
