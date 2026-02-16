using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Candidates;

[IslandCandidateProvider(800)]
public class PlaneSightingCandidateProvider : IIslandCandidateProvider
{
    public void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        // Only add if random chance triggers
        if (ctx.Random.NextDouble() >= 0.05)
            return;

        var baseDC = 15;
        var modifier = ctx.Actor.GetSkillModifier("Perception");
        var advantage = ctx.Actor.GetAdvantage("Perception");
        var estimatedChance = DndMath.EstimateSuccessChanceD20(baseDC, modifier, advantage);

        // Calculate base score with cooldown factored in
        var timeSinceLastSighting = ctx.NowSeconds - ctx.Actor.LastPlaneSightingTime;
        var cooldownFactor = Math.Min(1.0, timeSinceLastSighting / 600.0); // 600 second cooldown
        var baseScore = 0.2 * estimatedChance * cooldownFactor;

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("plane_sighting"),
                ActionKind.Interact,
                new Dictionary<string, object>
                {
                    ["dc"] = baseDC,
                    ["modifier"] = modifier,
                    ["advantage"] = advantage.ToString(),
                    ["vignette"] = true
                },
                10.0
            ),
            baseScore,
            "Plane sighting vignette"
        ));
    }
}
