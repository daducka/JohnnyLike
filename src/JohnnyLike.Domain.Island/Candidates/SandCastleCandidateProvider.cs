using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Candidates;

[IslandCandidateProvider(400, "build_sand_castle")]
public class SandCastleCandidateProvider : IIslandCandidateProvider
{
    private static readonly ResourceId BeachSandcastleSpot = new("island:resource:beach:sandcastle_spot");

    public void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        var baseDC = 8;

        if (ctx.World.TideLevel == TideLevel.High)
            baseDC += 4;

        // Roll skill check at candidate generation time
        var parameters = ctx.RollSkillCheck(SkillType.Performance, baseDC);

        var baseScore = 0.3 + (ctx.Actor.Boredom / 100.0);

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("build_sand_castle"),
                ActionKind.Interact,
                parameters,
                20.0 + ctx.Random.NextDouble() * 10.0,
                parameters.ToResultData(),
                new List<ResourceRequirement> { new ResourceRequirement(BeachSandcastleSpot) }
            ),
            baseScore,
            $"Build sand castle (DC {baseDC}, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})"
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
