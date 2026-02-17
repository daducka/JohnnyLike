using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Candidates;

[IslandCandidateProvider(410, "swim")]
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

        // Roll skill check at candidate generation time
        var (parameters, resultData, result) = ctx.RollSkillCheck("Survival", baseDC, "water");

        var baseScore = 0.35 + (ctx.Actor.Morale < 30 ? 0.2 : 0.0);
        // Score based on actual outcome tier
        baseScore *= result.OutcomeTier >= RollOutcomeTier.Success ? 1.0 : 0.3;

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("swim"),
                ActionKind.Interact,
                parameters,
                15.0 + ctx.Random.NextDouble() * 5.0,
                resultData
            ),
            baseScore,
            $"Swim (DC {baseDC}, rolled {result.Total}, {result.OutcomeTier})"
        ));
    }

    public void ApplyEffects(EffectContext ctx)
    {
        if (ctx.Tier == null)
            return;

        var tier = ctx.Tier.Value;

        switch (tier)
        {
            case RollOutcomeTier.CriticalSuccess:
                ctx.Actor.Morale = Math.Min(100.0, ctx.Actor.Morale + 20.0);
                ctx.Actor.Energy = Math.Max(0.0, ctx.Actor.Energy - 5.0);
                ctx.Actor.Boredom = Math.Max(0.0, ctx.Actor.Boredom - 15.0);
                break;

            case RollOutcomeTier.Success:
                ctx.Actor.Morale = Math.Min(100.0, ctx.Actor.Morale + 10.0);
                ctx.Actor.Energy = Math.Max(0.0, ctx.Actor.Energy - 10.0);
                ctx.Actor.Boredom = Math.Max(0.0, ctx.Actor.Boredom - 10.0);
                break;

            case RollOutcomeTier.PartialSuccess:
                ctx.Actor.Morale = Math.Min(100.0, ctx.Actor.Morale + 3.0);
                ctx.Actor.Energy = Math.Max(0.0, ctx.Actor.Energy - 15.0);
                break;

            case RollOutcomeTier.Failure:
                ctx.Actor.Energy = Math.Max(0.0, ctx.Actor.Energy - 15.0);
                break;

            case RollOutcomeTier.CriticalFailure:
                ctx.Actor.Energy = Math.Max(0.0, ctx.Actor.Energy - 25.0);
                ctx.Actor.Morale = Math.Max(0.0, ctx.Actor.Morale - 10.0);
                break;
        }
    }
}
