using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Stats;
using JohnnyLike.Domain.Island.Supply;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Candidates;

[IslandCandidateProvider(160, "collect_driftwood")]
public class CollectDriftwoodCandidateProvider : IIslandCandidateProvider
{
    private static readonly ResourceId BeachResource = new("island:resource:beach");

    public void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        var driftwoodStat = ctx.World.GetStat<DriftwoodAvailabilityStat>("driftwood_availability");
        if (driftwoodStat == null || driftwoodStat.DriftwoodAvailable < 5.0)
            return;

        // Get shared supply pile
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
            // High urgency when wood is low
            var urgency = (20.0 - currentWood) / 20.0;
            baseScore = 0.8 + (urgency * 0.4);
        }
        else if (currentWood < 50.0)
        {
            // Moderate concern when wood is below medium level
            var concern = (50.0 - currentWood) / 30.0;
            baseScore = 0.4 + (concern * 0.3);
        }
        else
        {
            // Low priority when well-stocked
            baseScore = 0.3;
        }

        // Apply foresight multiplier (actors with higher supply awareness plan ahead better)
        var foresightMultiplier = 1.0 + (supplyAwareness * 0.15);
        baseScore *= foresightMultiplier;

        // Skill check difficulty varies by driftwood availability
        var baseDC = driftwoodStat.DriftwoodAvailable < 20.0 ? 12 : 8;
        var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC);

        // Duration: 25-35 seconds
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
            $"Collect driftwood (wood: {currentWood:F1}, driftwood: {driftwoodStat.DriftwoodAvailable:F1}, DC {baseDC}, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})"
        ));
    }

    public void ApplyEffects(EffectContext ctx)
    {
        if (ctx.Tier == null)
            return;

        var tier = ctx.Tier.Value;
        var driftwoodStat = ctx.World.GetStat<DriftwoodAvailabilityStat>("driftwood_availability");
        if (driftwoodStat == null)
            return;

        var sharedPile = ctx.World.SharedSupplyPile;
        if (sharedPile == null)
            return;

        // Determine amount collected based on outcome tier
        double amountCollected = tier switch
        {
            RollOutcomeTier.CriticalSuccess => 15.0,
            RollOutcomeTier.Success => 10.0,
            RollOutcomeTier.PartialSuccess => 5.0,
            _ => 0.0
        };

        if (amountCollected > 0.0)
        {
            // Deplete driftwood stat (capped at available amount)
            var actualAmount = Math.Min(amountCollected, driftwoodStat.DriftwoodAvailable);
            driftwoodStat.DriftwoodAvailable = Math.Max(0.0, driftwoodStat.DriftwoodAvailable - actualAmount);

            // Add wood to shared supply pile
            sharedPile.AddSupply("wood", actualAmount, id => new WoodSupply(id));

            // Success effects
            ctx.Actor.Morale = Math.Min(100.0, ctx.Actor.Morale + 5.0);
            ctx.Actor.Boredom = Math.Max(0.0, ctx.Actor.Boredom - 3.0);
            ctx.Actor.Energy = Math.Max(0.0, ctx.Actor.Energy - 8.0);
        }
        else
        {
            // Failure effects
            ctx.Actor.Energy = Math.Max(0.0, ctx.Actor.Energy - 5.0);
            ctx.Actor.Morale = Math.Max(0.0, ctx.Actor.Morale - 3.0);
        }
    }
}
