using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Kit.Dice;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Island.Supply;

namespace JohnnyLike.Domain.Island.Candidates;

[IslandCandidateProvider(150, "add_fuel_campfire", "relight_campfire", "repair_campfire", "rebuild_campfire")]
public class CampfireMaintenanceCandidateProvider : IIslandCandidateProvider
{
    private static readonly ResourceId CampfireResource = new("island:resource:campfire");

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
            // Check wood availability
            var sharedPile = ctx.World.SharedSupplyPile;
            var currentWood = sharedPile?.GetQuantity<WoodSupply>("wood") ?? 0.0;

            // Reduce score or skip if insufficient wood
            if (currentWood < 10.0)
            {
                // Skip offering this action if very low on wood
                if (currentWood < 3.0)
                    return;
            }

            var urgency = 1.0 - (campfire.FuelSeconds / 1800.0);
            var foresightMultiplier = 1.0 + (foresightBonus * 0.1);

            var baseDC = 10;
            var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC);

            var baseScore = 0.3 + (urgency * 0.5 * foresightMultiplier);

            // Reduce score if wood is low
            if (currentWood < 10.0)
            {
                baseScore *= 0.5;
            }

            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("add_fuel_campfire"),
                    ActionKind.Interact,
                    parameters,
                    20.0 + ctx.Random.NextDouble() * 5.0,
                    parameters.ToResultData(),
                    new List<ResourceRequirement> { new ResourceRequirement(CampfireResource) }
                ),
                baseScore,
                $"Add fuel to campfire (fuel: {campfire.FuelSeconds:F0}s, wood: {currentWood:F1}, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})"
            ));
        }

        if (!campfire.IsLit && campfire.Quality > 20.0)
        {
            var urgency = 0.8;
            var foresightMultiplier = 1.0 + (foresightBonus * 0.15);

            var baseDC = 12;
            var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC);

            var baseScore = urgency * foresightMultiplier;

            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("relight_campfire"),
                    ActionKind.Interact,
                    parameters,
                    30.0 + ctx.Random.NextDouble() * 10.0,
                    parameters.ToResultData(),
                    new List<ResourceRequirement> { new ResourceRequirement(CampfireResource) }
                ),
                baseScore,
                $"Relight campfire (quality: {campfire.Quality:F0}%, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})"
            ));
        }

        if (campfire.Quality < 70.0)
        {
            var urgency = (70.0 - campfire.Quality) / 70.0;
            var foresightMultiplier = 1.0 + (foresightBonus * 0.12);

            var baseDC = 11;
            var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC);

            var baseScore = 0.2 + (urgency * 0.4 * foresightMultiplier);

            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("repair_campfire"),
                    ActionKind.Interact,
                    parameters,
                    25.0 + ctx.Random.NextDouble() * 5.0,
                    parameters.ToResultData(),
                    new List<ResourceRequirement> { new ResourceRequirement(CampfireResource) }
                ),
                baseScore,
                $"Repair campfire (quality: {campfire.Quality:F0}%, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})"
            ));
        }

        if (campfire.Quality < 10.0)
        {
            var baseDC = 15;
            var foresightMultiplier = 1.0 + (foresightBonus * 0.2);

            var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC);

            var baseScore = 1.0 * foresightMultiplier;

            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("rebuild_campfire"),
                    ActionKind.Interact,
                    parameters,
                    60.0 + ctx.Random.NextDouble() * 20.0,
                    parameters.ToResultData(),
                    new List<ResourceRequirement> { new ResourceRequirement(CampfireResource) }
                ),
                baseScore,
                $"Rebuild campfire from scratch (rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})"
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
                    // Calculate wood cost based on tier
                    var woodCost = tier == RollOutcomeTier.CriticalSuccess ? 3.0 :
                                   tier == RollOutcomeTier.Success ? 5.0 : 7.0;

                    var sharedPile = ctx.World.SharedSupplyPile;
                    if (sharedPile != null && sharedPile.TryConsumeSupply<WoodSupply>("wood", woodCost))
                    {
                        // Successfully consumed wood, add fuel as normal
                        var fuelAdded = tier == RollOutcomeTier.CriticalSuccess ? 2400.0 :
                                        tier == RollOutcomeTier.Success ? 1800.0 : 900.0;
                        campfire.FuelSeconds = Math.Min(7200.0, campfire.FuelSeconds + fuelAdded);
                        ctx.Actor.Boredom = Math.Max(0.0, ctx.Actor.Boredom - 5.0);
                    }
                    else
                    {
                        // Insufficient wood - action still succeeds but with reduced effectiveness
                        var reducedFuel = tier == RollOutcomeTier.CriticalSuccess ? 1200.0 :
                                          tier == RollOutcomeTier.Success ? 900.0 : 450.0;
                        campfire.FuelSeconds = Math.Min(7200.0, campfire.FuelSeconds + reducedFuel);
                        ctx.Actor.Boredom = Math.Max(0.0, ctx.Actor.Boredom - 2.0);
                        ctx.Actor.Morale = Math.Max(0.0, ctx.Actor.Morale - 3.0); // Frustration from lack of wood
                    }
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
