using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Candidates;

[IslandCandidateProvider(800)]
public class VignetteCandidateProvider : IIslandCandidateProvider
{
    public void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        // Plane sighting vignette
        if (ctx.NowSeconds - ctx.Actor.LastPlaneSightingTime > 600.0)
        {
            if (ctx.Random.NextDouble() < 0.05)
            {
                var baseDC = 15;
                var modifier = ctx.Actor.GetSkillModifier("Perception");
                var advantage = ctx.Actor.GetAdvantage("Perception");
                var estimatedChance = DndMath.EstimateSuccessChanceD20(baseDC, modifier, advantage);

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
                    0.2 * estimatedChance,
                    "Plane sighting vignette"
                ));
            }
        }

        // Mermaid encounter vignette (only during night/dawn/dusk)
        if (ctx.World.TimeOfDay > 0.75 || ctx.World.TimeOfDay < 0.25)
        {
            if (ctx.NowSeconds - ctx.Actor.LastMermaidEncounterTime > 1200.0)
            {
                if (ctx.Random.NextDouble() < 0.02)
                {
                    var baseDC = 18;
                    var modifier = ctx.Actor.GetSkillModifier("Perception");
                    var advantage = ctx.Actor.GetAdvantage("Perception");
                    var estimatedChance = DndMath.EstimateSuccessChanceD20(baseDC, modifier, advantage);

                    output.Add(new ActionCandidate(
                        new ActionSpec(
                            new ActionId("mermaid_encounter"),
                            ActionKind.Interact,
                            new Dictionary<string, object>
                            {
                                ["dc"] = baseDC,
                                ["modifier"] = modifier,
                                ["advantage"] = advantage.ToString(),
                                ["vignette"] = true
                            },
                            15.0
                        ),
                        0.15 * estimatedChance,
                        "Mermaid encounter vignette"
                    ));
                }
            }
        }
    }
}
