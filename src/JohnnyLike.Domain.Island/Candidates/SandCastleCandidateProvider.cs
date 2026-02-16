using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Candidates;

[IslandCandidateProvider(400, "build_sand_castle")]
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
                new SkillCheckActionParameters(baseDC, modifier, advantage, "beach"),
                20.0 + ctx.Rng.NextDouble() * 10.0
            ),
            baseScore,
            $"Build sand castle (DC {baseDC}, {estimatedChance:P0} chance)"
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
                ctx.Actor.Morale = Math.Min(100.0, ctx.Actor.Morale + 25.0);
                ctx.Actor.Boredom = Math.Max(0.0, ctx.Actor.Boredom - 30.0);
                break;

            case RollOutcomeTier.Success:
                ctx.Actor.Morale = Math.Min(100.0, ctx.Actor.Morale + 15.0);
                ctx.Actor.Boredom = Math.Max(0.0, ctx.Actor.Boredom - 20.0);
                break;

            case RollOutcomeTier.PartialSuccess:
                ctx.Actor.Morale = Math.Min(100.0, ctx.Actor.Morale + 5.0);
                ctx.Actor.Boredom = Math.Max(0.0, ctx.Actor.Boredom - 10.0);
                break;

            case RollOutcomeTier.Failure:
                ctx.Actor.Boredom = Math.Max(0.0, ctx.Actor.Boredom - 5.0);
                break;

            case RollOutcomeTier.CriticalFailure:
                ctx.Actor.Morale = Math.Max(0.0, ctx.Actor.Morale - 5.0);
                break;
        }
    }
}
