using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Stats;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Items;

/// <summary>
/// Represents a coconut palm tree that can be shaken for coconuts.
/// </summary>
public class CoconutTreeItem : MaintainableWorldItem
{
    private static readonly ResourceId PalmTreeResource = new("island:resource:palm_tree");

    public CoconutTreeItem(string id = "palm_tree")
        : base(id, "palm_tree", baseDecayPerSecond: 0.0)
    {
        // Trees don't decay
    }

    public override void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        var coconutStat = ctx.World.GetStat<CoconutAvailabilityStat>("coconut_availability");
        if (coconutStat == null || coconutStat.CoconutsAvailable < 1)
            return;

        var baseDC = 12;

        if (coconutStat.CoconutsAvailable >= 5)
            baseDC -= 2;
        else if (coconutStat.CoconutsAvailable <= 2)
            baseDC += 2;

        var weatherStat = ctx.World.GetStat<WeatherStat>("weather");
        if (weatherStat?.Weather == Weather.Windy)
            baseDC -= 1;

        // Roll skill check at candidate generation time
        var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC);

        var baseScore = 0.4 + (ctx.Actor.Hunger / 150.0);
        if (ctx.Actor.Hunger > 70.0)
        {
            baseScore = 0.9;
        }

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("shake_tree_coconut"),
                ActionKind.Interact,
                parameters,
                10.0 + ctx.Random.NextDouble() * 5.0,
                parameters.ToResultData(),
                new List<ResourceRequirement> { new ResourceRequirement(PalmTreeResource) }
            ),
            baseScore,
            $"Get coconut (DC {baseDC}, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})",
            EffectHandler: new Action<EffectContext>(ApplyEffects)
        ));
    }

    public override void ApplyEffects(EffectContext ctx)
    {
        if (ctx.Tier == null)
            return;

        var tier = ctx.Tier.Value;
        var coconutStat = ctx.World.GetStat<CoconutAvailabilityStat>("coconut_availability");
        if (coconutStat == null)
            return;

        switch (tier)
        {
            case RollOutcomeTier.CriticalSuccess:
                ctx.Actor.Hunger = Math.Max(0.0, ctx.Actor.Hunger - 25.0);
                coconutStat.CoconutsAvailable = Math.Max(0, coconutStat.CoconutsAvailable - 1);
                ctx.Actor.Energy = Math.Min(100.0, ctx.Actor.Energy + 15.0);
                break;

            case RollOutcomeTier.Success:
                ctx.Actor.Hunger = Math.Max(0.0, ctx.Actor.Hunger - 15.0);
                coconutStat.CoconutsAvailable = Math.Max(0, coconutStat.CoconutsAvailable - 1);
                ctx.Actor.Energy = Math.Min(100.0, ctx.Actor.Energy + 10.0);
                break;

            case RollOutcomeTier.PartialSuccess:
                ctx.Actor.Morale = Math.Min(100.0, ctx.Actor.Morale + 2.0);
                break;

            case RollOutcomeTier.Failure:
                break;

            case RollOutcomeTier.CriticalFailure:
                ctx.Actor.Morale = Math.Max(0.0, ctx.Actor.Morale - 5.0);
                break;
        }
    }
}
