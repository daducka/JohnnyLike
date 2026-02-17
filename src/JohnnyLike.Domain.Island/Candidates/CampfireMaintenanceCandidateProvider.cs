using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Kit.Dice;
using JohnnyLike.Domain.Island.Items;

namespace JohnnyLike.Domain.Island.Candidates;

[IslandCandidateProvider(150, "add_fuel_campfire", "relight_campfire", "repair_campfire", "rebuild_campfire")]
public class CampfireMaintenanceCandidateProvider : IIslandCandidateProvider
{
    public void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        var campfire = ctx.World.MainCampfire;
        if (campfire == null)
            return;

        var survivalMod = ctx.Actor.SurvivalSkill;
        var wisdomMod = DndMath.AbilityModifier(ctx.Actor.WIS);
        
        var foresightBonus = (survivalMod + wisdomMod) / 2.0;

        if (campfire.IsLit && campfire.FuelSeconds < 1800.0)
        {
            var urgency = 1.0 - (campfire.FuelSeconds / 1800.0);
            var foresightMultiplier = 1.0 + (foresightBonus * 0.1);

            var baseDC = 10;
            var (parameters, resultData, result) = ctx.RollSkillCheck("Survival", baseDC, "campfire");

            var baseScore = 0.3 + (urgency * 0.5 * foresightMultiplier);
            // Score based on actual outcome tier
            baseScore *= result.OutcomeTier >= RollOutcomeTier.Success ? 1.0 : 0.5;

            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("add_fuel_campfire"),
                    ActionKind.Interact,
                    parameters,
                    20.0 + ctx.Random.NextDouble() * 5.0,
                    resultData
                ),
                baseScore,
                $"Add fuel to campfire (fuel: {campfire.FuelSeconds:F0}s, rolled {result.Total}, {result.OutcomeTier})"
            ));
        }

        if (!campfire.IsLit && campfire.Quality > 20.0)
        {
            var urgency = 0.8;
            var foresightMultiplier = 1.0 + (foresightBonus * 0.15);

            var baseDC = 12;
            var (parameters, resultData, result) = ctx.RollSkillCheck("Survival", baseDC, "campfire");

            var baseScore = urgency * foresightMultiplier;
            // Score based on actual outcome tier
            baseScore *= result.OutcomeTier >= RollOutcomeTier.Success ? 1.0 : 0.5;

            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("relight_campfire"),
                    ActionKind.Interact,
                    parameters,
                    30.0 + ctx.Random.NextDouble() * 10.0,
                    resultData
                ),
                baseScore,
                $"Relight campfire (quality: {campfire.Quality:F0}%, rolled {result.Total}, {result.OutcomeTier})"
            ));
        }

        if (campfire.Quality < 70.0)
        {
            var urgency = (70.0 - campfire.Quality) / 70.0;
            var foresightMultiplier = 1.0 + (foresightBonus * 0.12);

            var baseDC = 11;
            var (parameters, resultData, result) = ctx.RollSkillCheck("Survival", baseDC, "campfire");

            var baseScore = 0.2 + (urgency * 0.4 * foresightMultiplier);
            // Score based on actual outcome tier
            baseScore *= result.OutcomeTier >= RollOutcomeTier.Success ? 1.0 : 0.5;

            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("repair_campfire"),
                    ActionKind.Interact,
                    parameters,
                    25.0 + ctx.Random.NextDouble() * 5.0,
                    resultData
                ),
                baseScore,
                $"Repair campfire (quality: {campfire.Quality:F0}%, rolled {result.Total}, {result.OutcomeTier})"
            ));
        }

        if (campfire.Quality < 10.0)
        {
            var baseDC = 15;
            var foresightMultiplier = 1.0 + (foresightBonus * 0.2);

            var (parameters, resultData, result) = ctx.RollSkillCheck("Survival", baseDC, "campfire");

            var baseScore = 1.0 * foresightMultiplier;
            // Score based on actual outcome tier
            baseScore *= result.OutcomeTier >= RollOutcomeTier.Success ? 1.0 : 0.5;

            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("rebuild_campfire"),
                    ActionKind.Interact,
                    parameters,
                    60.0 + ctx.Random.NextDouble() * 20.0,
                    resultData
                ),
                baseScore,
                $"Rebuild campfire from scratch (rolled {result.Total}, {result.OutcomeTier})"
            ));
        }
    }

    public void ApplyEffects(EffectContext ctx)
    {
        var campfire = ctx.World.MainCampfire;
        if (campfire == null)
            return;

        if (ctx.Tier == null)
            return;

        var tier = ctx.Tier.Value;
        var actionId = ctx.Outcome.ActionId.Value;

        switch (actionId)
        {
            case "add_fuel_campfire":
                if (tier >= RollOutcomeTier.PartialSuccess)
                {
                    var fuelAdded = tier == RollOutcomeTier.CriticalSuccess ? 2400.0 : 
                                    tier == RollOutcomeTier.Success ? 1800.0 : 900.0;
                    campfire.FuelSeconds = Math.Min(7200.0, campfire.FuelSeconds + fuelAdded);
                    ctx.Actor.Boredom = Math.Max(0.0, ctx.Actor.Boredom - 5.0);
                }
                break;

            case "relight_campfire":
                if (tier >= RollOutcomeTier.Success)
                {
                    campfire.IsLit = true;
                    campfire.FuelSeconds = tier == RollOutcomeTier.CriticalSuccess ? 1800.0 : 1200.0;
                    ctx.Actor.Morale = Math.Min(100.0, ctx.Actor.Morale + 10.0);
                    ctx.Actor.Boredom = Math.Max(0.0, ctx.Actor.Boredom - 8.0);
                }
                break;

            case "repair_campfire":
                if (tier >= RollOutcomeTier.PartialSuccess)
                {
                    var qualityRestored = tier == RollOutcomeTier.CriticalSuccess ? 40.0 : 
                                         tier == RollOutcomeTier.Success ? 25.0 : 15.0;
                    campfire.Quality = Math.Min(100.0, campfire.Quality + qualityRestored);
                    ctx.Actor.Boredom = Math.Max(0.0, ctx.Actor.Boredom - 7.0);
                }
                break;

            case "rebuild_campfire":
                if (tier >= RollOutcomeTier.Success)
                {
                    campfire.Quality = tier == RollOutcomeTier.CriticalSuccess ? 100.0 : 80.0;
                    campfire.IsLit = true;
                    campfire.FuelSeconds = 1800.0;
                    ctx.Actor.Morale = Math.Min(100.0, ctx.Actor.Morale + 15.0);
                    ctx.Actor.Boredom = Math.Max(0.0, ctx.Actor.Boredom - 20.0);
                }
                break;
        }
    }
}
