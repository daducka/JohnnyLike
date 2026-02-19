using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Stats;
using JohnnyLike.Domain.Island.Supply;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Items;

/// <summary>
/// Represents a pile of driftwood on the beach that can be collected.
/// </summary>
public class DriftwoodPileItem : WorldItem, IIslandActionCandidate
{
    private static readonly ResourceId BeachResource = new("island:resource:beach");

    public DriftwoodPileItem(string id = "driftwood_pile")
        : base(id, "driftwood_pile")
    {
        // Driftwood pile doesn't decay
    }

    public void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        var driftwoodStat = ctx.World.GetStat<DriftwoodAvailabilityStat>("driftwood_availability");
        if (driftwoodStat == null || driftwoodStat.DriftwoodAvailable < 5.0)
            return;

        var sharedPile = ctx.World.SharedSupplyPile;
        if (sharedPile == null)
            return;

        var currentWood = sharedPile.GetQuantity<WoodSupply>("wood");

        // Calculate supply awareness based on Survival skill and WIS modifier
        var survivalSkill = ctx.Actor.SurvivalSkill;
        var wisdomMod = DndMath.AbilityModifier(ctx.Actor.WIS);
        var supplyAwareness = (survivalSkill + wisdomMod) / 2.0;

        // Determine base score based on current wood levels
        double baseScore;
        if (currentWood < 20.0)
        {
            var urgency = (20.0 - currentWood) / 20.0;
            baseScore = 0.8 + (urgency * 0.4);
        }
        else if (currentWood < 50.0)
        {
            var concern = (50.0 - currentWood) / 30.0;
            baseScore = 0.4 + (concern * 0.3);
        }
        else
        {
            baseScore = 0.3;
        }

        var foresightMultiplier = 1.0 + (supplyAwareness * 0.15);
        baseScore *= foresightMultiplier;

        var baseDC = driftwoodStat.DriftwoodAvailable < 20.0 ? 12 : 8;
        var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC);
        var duration = 25.0 + ctx.Random.NextDouble() * 10.0;

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("collect_driftwood"),
                ActionKind.Interact,
                parameters,
                duration,
                parameters.ToResultData(),
                new List<ResourceRequirement> { new ResourceRequirement(BeachResource) }
            ),
            baseScore,
            $"Collect driftwood (DC {baseDC}, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})",
            EffectHandler: new Action<EffectContext>(effectCtx =>
            {
                if (effectCtx.Tier == null)
                    return;

                var tier = effectCtx.Tier.Value;
                var driftStat = effectCtx.World.GetStat<DriftwoodAvailabilityStat>("driftwood_availability");
                if (driftStat == null)
                    return;

                var pile = effectCtx.World.SharedSupplyPile;
                if (pile == null)
                    return;

                double woodGained = tier switch
                {
                    RollOutcomeTier.CriticalSuccess => 15.0,
                    RollOutcomeTier.Success => 10.0,
                    RollOutcomeTier.PartialSuccess => 5.0,
                    _ => 0.0
                };

                if (woodGained > 0)
                {
                    pile.AddSupply("wood", woodGained, id => new WoodSupply(id));
                    driftStat.DriftwoodAvailable = Math.Max(0.0, driftStat.DriftwoodAvailable - woodGained);
                    
                    // Success effects
                    effectCtx.Actor.Morale += 5.0;
                    effectCtx.Actor.Energy -= 8.0;
                }
                else
                {
                    // Failure effects
                    effectCtx.Actor.Energy -= 5.0;
                }

                if (tier == RollOutcomeTier.CriticalFailure)
                {
                    effectCtx.Actor.Morale -= 5.0;
                }
            })
        ));
    }
}
