using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Supply;
using JohnnyLike.Domain.Kit.Dice;
using System.Text.Json;

namespace JohnnyLike.Domain.Island.Items;

public class CampfireItem : ToolItem
{
    private static readonly ResourceId CampfireResource = new("island:resource:campfire");
    
    public bool IsLit { get; set; } = true;
    public double FuelSeconds { get; set; } = 3600.0;

    public CampfireItem(string id = "main_campfire") 
        : base(id, "campfire", OwnershipType.Shared, baseDecayPerSecond: 0.02)
    {
    }

    public override void Tick(double dtSeconds, IslandWorldState world)
    {
        base.Tick(dtSeconds, world);

        if (IsLit)
        {
            FuelSeconds = Math.Max(0.0, FuelSeconds - dtSeconds);
            
            if (FuelSeconds <= 0.0)
            {
                IsLit = false;
            }
        }

        if (!IsLit)
        {
            Quality = Math.Max(0.0, Quality - 0.05 * dtSeconds);
        }
    }

    public override void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        var survivalMod = ctx.Actor.SurvivalSkill;
        var wisdomMod = DndMath.AbilityModifier(ctx.Actor.WIS);
        var foresightBonus = (survivalMod + wisdomMod) / 2.0;

        // Add Fuel action
        if (IsLit && FuelSeconds < 1800.0)
        {
            var sharedPile = ctx.World.SharedSupplyPile;
            var currentWood = sharedPile?.GetQuantity<WoodSupply>("wood") ?? 0.0;

            if (currentWood < 3.0)
                return;

            var urgency = 1.0 - (FuelSeconds / 1800.0);
            var foresightMultiplier = 1.0 + (foresightBonus * 0.1);
            var baseDC = 10;
            var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC);
            var baseScore = 0.3 + (urgency * 0.5 * foresightMultiplier);

            if (currentWood < 10.0)
            {
                baseScore *= 0.5;
            }

            var resultData = parameters.ToResultData();
            // Add direct effect handler instead of tool_item_id
            resultData["__effect_handler__"] = new Action<EffectContext>(ApplyAddFuelEffect);
            
            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("add_fuel_campfire"),
                    ActionKind.Interact,
                    parameters,
                    20.0 + ctx.Random.NextDouble() * 5.0,
                    resultData,
                    new List<ResourceRequirement> { new ResourceRequirement(CampfireResource) }
                ),
                baseScore,
                $"Add fuel to campfire (fuel: {FuelSeconds:F0}s, wood: {currentWood:F1}, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})"
            ));
        }

        // Light action
        if (!IsLit && Quality > 20.0)
        {
            var urgency = 0.8;
            var foresightMultiplier = 1.0 + (foresightBonus * 0.15);
            var baseDC = 12;
            var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC);
            var baseScore = urgency * foresightMultiplier;

            var resultData = parameters.ToResultData();
            resultData["__effect_handler__"] = new Action<EffectContext>(ApplyRelightEffect);
            
            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("relight_campfire"),
                    ActionKind.Interact,
                    parameters,
                    30.0 + ctx.Random.NextDouble() * 10.0,
                    resultData,
                    new List<ResourceRequirement> { new ResourceRequirement(CampfireResource) }
                ),
                baseScore,
                $"Relight campfire (quality: {Quality:F0}%, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})"
            ));
        }

        // Maintain (Repair) action
        if (Quality < 70.0)
        {
            var urgency = (70.0 - Quality) / 70.0;
            var foresightMultiplier = 1.0 + (foresightBonus * 0.12);
            var baseDC = 11;
            var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC);
            var baseScore = 0.2 + (urgency * 0.4 * foresightMultiplier);

            var resultData = parameters.ToResultData();
            resultData["__effect_handler__"] = new Action<EffectContext>(ApplyRepairEffect);
            
            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("repair_campfire"),
                    ActionKind.Interact,
                    parameters,
                    25.0 + ctx.Random.NextDouble() * 5.0,
                    resultData,
                    new List<ResourceRequirement> { new ResourceRequirement(CampfireResource) }
                ),
                baseScore,
                $"Repair campfire (quality: {Quality:F0}%, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})"
            ));
        }

        // Rebuild action
        if (Quality < 10.0)
        {
            var baseDC = 15;
            var foresightMultiplier = 1.0 + (foresightBonus * 0.2);
            var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC);
            var baseScore = 1.0 * foresightMultiplier;

            var resultData = parameters.ToResultData();
            resultData["__effect_handler__"] = new Action<EffectContext>(ApplyRebuildEffect);
            
            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("rebuild_campfire"),
                    ActionKind.Interact,
                    parameters,
                    60.0 + ctx.Random.NextDouble() * 20.0,
                    resultData,
                    new List<ResourceRequirement> { new ResourceRequirement(CampfireResource) }
                ),
                baseScore,
                $"Rebuild campfire from scratch (rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})"
            ));
        }
    }

    public override void ApplyEffects(EffectContext ctx)
    {
        if (ctx.Tier == null)
            return;

        var tier = ctx.Tier.Value;
        var actionId = ctx.Outcome.ActionId.Value;

        switch (actionId)
        {
            case "add_fuel_campfire":
                ApplyAddFuelEffect(ctx);
                break;

            case "relight_campfire":
                ApplyRelightEffect(ctx);
                break;

            case "repair_campfire":
                ApplyRepairEffect(ctx);
                break;

            case "rebuild_campfire":
                ApplyRebuildEffect(ctx);
                break;
        }
    }

    private void ApplyAddFuelEffect(EffectContext ctx)
    {
        if (ctx.Tier == null)
            return;

        var tier = ctx.Tier.Value;

        if (tier >= RollOutcomeTier.PartialSuccess)
        {
            var woodCost = tier == RollOutcomeTier.CriticalSuccess ? 3.0 :
                           tier == RollOutcomeTier.Success ? 5.0 : 7.0;

            var sharedPile = ctx.World.SharedSupplyPile;
            if (sharedPile != null && sharedPile.TryConsumeSupply<WoodSupply>("wood", woodCost))
            {
                var fuelAdded = tier == RollOutcomeTier.CriticalSuccess ? 2400.0 :
                                tier == RollOutcomeTier.Success ? 1800.0 : 900.0;
                FuelSeconds = Math.Min(7200.0, FuelSeconds + fuelAdded);
                ctx.Actor.Boredom = Math.Max(0.0, ctx.Actor.Boredom - 5.0);
            }
            else
            {
                var reducedFuel = tier == RollOutcomeTier.CriticalSuccess ? 1200.0 :
                                  tier == RollOutcomeTier.Success ? 900.0 : 450.0;
                FuelSeconds = Math.Min(7200.0, FuelSeconds + reducedFuel);
                ctx.Actor.Boredom = Math.Max(0.0, ctx.Actor.Boredom - 2.0);
                ctx.Actor.Morale = Math.Max(0.0, ctx.Actor.Morale - 3.0);
            }
        }
    }

    private void ApplyRelightEffect(EffectContext ctx)
    {
        if (ctx.Tier == null)
            return;

        var tier = ctx.Tier.Value;

        if (tier >= RollOutcomeTier.Success)
        {
            IsLit = true;
            FuelSeconds = tier == RollOutcomeTier.CriticalSuccess ? 1800.0 : 1200.0;
            ctx.Actor.Morale = Math.Min(100.0, ctx.Actor.Morale + 10.0);
            ctx.Actor.Boredom = Math.Max(0.0, ctx.Actor.Boredom - 8.0);
        }
    }

    private void ApplyRepairEffect(EffectContext ctx)
    {
        if (ctx.Tier == null)
            return;

        var tier = ctx.Tier.Value;

        if (tier >= RollOutcomeTier.PartialSuccess)
        {
            var qualityRestored = tier == RollOutcomeTier.CriticalSuccess ? 40.0 : 
                                 tier == RollOutcomeTier.Success ? 25.0 : 15.0;
            Quality = Math.Min(100.0, Quality + qualityRestored);
            ctx.Actor.Boredom = Math.Max(0.0, ctx.Actor.Boredom - 7.0);
        }
    }

    private void ApplyRebuildEffect(EffectContext ctx)
    {
        if (ctx.Tier == null)
            return;

        var tier = ctx.Tier.Value;

        if (tier >= RollOutcomeTier.Success)
        {
            Quality = tier == RollOutcomeTier.CriticalSuccess ? 100.0 : 80.0;
            IsLit = true;
            FuelSeconds = 1800.0;
            ctx.Actor.Morale = Math.Min(100.0, ctx.Actor.Morale + 15.0);
            ctx.Actor.Boredom = Math.Max(0.0, ctx.Actor.Boredom - 20.0);
        }
    }

    public override Dictionary<string, object> SerializeToDict()
    {
        var dict = base.SerializeToDict();
        dict["IsLit"] = IsLit;
        dict["FuelSeconds"] = FuelSeconds;
        return dict;
    }

    public override void DeserializeFromDict(Dictionary<string, JsonElement> data)
    {
        base.DeserializeFromDict(data);
        IsLit = data["IsLit"].GetBoolean();
        FuelSeconds = data["FuelSeconds"].GetDouble();
    }
}
