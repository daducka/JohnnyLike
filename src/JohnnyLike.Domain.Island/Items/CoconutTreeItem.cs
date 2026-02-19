using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Stats;
using JohnnyLike.Domain.Island.Supply;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Items;

/// <summary>
/// Represents a coconut palm tree that can be shaken for coconuts.
/// </summary>
public class CoconutTreeItem : WorldItem, IIslandActionCandidate
{
    private static readonly ResourceId PalmTreeResource = new("island:resource:palm_tree");

    public CoconutTreeItem(string id = "palm_tree")
        : base(id, "palm_tree")
    {
        // Trees don't decay
    }

    public void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
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

        var baseScore = 0.4 + ((100.0 - ctx.Actor.Satiety) / 150.0);
        if (ctx.Actor.Satiety < 30.0)
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
            EffectHandler: new Action<EffectContext>(effectCtx =>
            {
                if (effectCtx.Tier == null)
                    return;

                var tier = effectCtx.Tier.Value;
                var coconutStat = effectCtx.World.GetStat<CoconutAvailabilityStat>("coconut_availability");
                if (coconutStat == null)
                    return;

                var sharedPile = effectCtx.World.SharedSupplyPile;

                switch (tier)
                {
                    case RollOutcomeTier.CriticalSuccess:
                        coconutStat.CoconutsAvailable = Math.Max(0, coconutStat.CoconutsAvailable - 1);
                        sharedPile?.AddSupply("coconut", 2.0, id => new CoconutSupply(id));
                        break;

                    case RollOutcomeTier.Success:
                        coconutStat.CoconutsAvailable = Math.Max(0, coconutStat.CoconutsAvailable - 1);
                        sharedPile?.AddSupply("coconut", 1.0, id => new CoconutSupply(id));
                        break;

                    case RollOutcomeTier.PartialSuccess:
                        effectCtx.Actor.Morale += 2.0;
                        break;

                    case RollOutcomeTier.Failure:
                        break;

                    case RollOutcomeTier.CriticalFailure:
                        effectCtx.Actor.Morale -= 5.0;
                        break;
                }
            })
        ));
    }
}
