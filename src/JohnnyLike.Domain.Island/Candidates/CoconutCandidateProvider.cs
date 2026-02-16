using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Candidates;

[IslandCandidateProvider(210)]
public class CoconutCandidateProvider : IIslandCandidateProvider
{
    public void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        if (ctx.World.CoconutsAvailable < 1)
            return;

        var baseDC = 12;

        if (ctx.World.CoconutsAvailable >= 5)
            baseDC -= 2;
        else if (ctx.World.CoconutsAvailable <= 2)
            baseDC += 2;

        if (ctx.World.Weather == Weather.Windy)
            baseDC -= 1;

        var modifier = ctx.Actor.GetSkillModifier("Survival");
        var advantage = ctx.Actor.GetAdvantage("Survival");

        var estimatedChance = DndMath.EstimateSuccessChanceD20(baseDC, modifier, advantage);

        var baseScore = 0.4 + (ctx.Actor.Hunger / 150.0);
        if (ctx.Actor.Hunger > 70.0)
        {
            baseScore = 0.9;
        }
        else
        {
            baseScore *= estimatedChance;
        }

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("shake_tree_coconut"),
                ActionKind.Interact,
                new Dictionary<string, object>
                {
                    ["dc"] = baseDC,
                    ["modifier"] = modifier,
                    ["advantage"] = advantage.ToString(),
                    ["location"] = "palm_tree"
                },
                10.0 + ctx.Rng.NextDouble() * 5.0
            ),
            baseScore,
            $"Get coconut (DC {baseDC}, {estimatedChance:P0} chance)"
        ));
    }
}
