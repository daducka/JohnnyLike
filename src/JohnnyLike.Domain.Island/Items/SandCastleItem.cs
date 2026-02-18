using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Stats;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Items;

/// <summary>
/// Represents a sand castle building opportunity on the beach.
/// </summary>
public class SandCastleItem : MaintainableWorldItem
{
    private static readonly ResourceId BeachSandcastleSpot = new("island:resource:beach:sandcastle_spot");

    public SandCastleItem(string id = "sandcastle_opportunity")
        : base(id, "sandcastle_opportunity", baseDecayPerSecond: 0.0)
    {
        // This is more of an opportunity than a physical item, so it doesn't decay
    }

    public override void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        var baseDC = 8;

        var tideStat = ctx.World.GetStat<TideStat>("tide");
        if (tideStat?.TideLevel == TideLevel.High)
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
            $"Build sand castle (DC {baseDC}, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})",
            EffectHandler: new Action<EffectContext>(ApplyEffects)
        ));
    }

    public override void ApplyEffects(EffectContext ctx)
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
