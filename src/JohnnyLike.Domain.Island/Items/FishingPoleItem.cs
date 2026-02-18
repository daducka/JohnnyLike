using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Kit.Dice;
using System.Text.Json;

namespace JohnnyLike.Domain.Island.Items;

public class FishingPoleItem : ToolItem
{
    private static readonly ResourceId FishingPoleResource = new("island:resource:fishing_pole");
    public const double BreakageQualityThreshold = 20.0;
    
    public FishingPoleItem(string id, ActorId? ownerActorId = null) 
        : base(id, "fishing_pole", OwnershipType.Exclusive, baseDecayPerSecond: 0.005, maxOwners: 1)
    {
        OwnerActorId = ownerActorId;
    }

    public override void Tick(double dtSeconds, IslandWorldState world)
    {
        base.Tick(dtSeconds, world);
        
        // Fishing poles degrade slower than other tools but can break if quality is too low
        if (Quality < BreakageQualityThreshold && !IsBroken)
        {
            IsBroken = true;
        }
    }

    public override void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        // Only offer candidates if this actor owns the fishing pole
        if (!CanActorUseTool(ctx.ActorId))
            return;

        // GoFishing action - only if pole is not broken
        if (!IsBroken && Quality > 10.0)
        {
            var fishingMod = ctx.Actor.FishingSkill;
            var dexMod = DndMath.AbilityModifier(ctx.Actor.DEX);
            var baseDC = 12;
            
            // Quality affects the DC
            if (Quality < 50.0)
                baseDC += 2;
            else if (Quality > 80.0)
                baseDC -= 1;
            
            var parameters = ctx.RollSkillCheck(SkillType.Fishing, baseDC);
            var baseScore = 0.6 + (fishingMod * 0.05);
            
            // Reduce score if pole quality is low
            if (Quality < 50.0)
                baseScore *= 0.7;

            var resultData = parameters.ToResultData();
            resultData["__effect_handler__"] = new Action<EffectContext>(ApplyGoFishingEffect);
            
            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("go_fishing"),
                    ActionKind.Interact,
                    parameters,
                    45.0 + ctx.Random.NextDouble() * 15.0,
                    resultData,
                    new List<ResourceRequirement> { new ResourceRequirement(FishingPoleResource) }
                ),
                baseScore,
                $"Go fishing with pole (quality: {Quality:F0}%, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})"
            ));
        }

        // MaintainRod action - maintain the pole to keep it in good condition
        if (Quality < 80.0 && !IsBroken)
        {
            var urgency = (80.0 - Quality) / 80.0;
            var survivalMod = ctx.Actor.SurvivalSkill;
            var baseDC = 10;
            var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC);
            var baseScore = 0.3 + (urgency * 0.4);

            var resultData = parameters.ToResultData();
            resultData["__effect_handler__"] = new Action<EffectContext>(ApplyMaintainRodEffect);
            
            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("maintain_rod"),
                    ActionKind.Interact,
                    parameters,
                    20.0 + ctx.Random.NextDouble() * 5.0,
                    resultData,
                    new List<ResourceRequirement> { new ResourceRequirement(FishingPoleResource) }
                ),
                baseScore,
                $"Maintain fishing rod (quality: {Quality:F0}%, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})"
            ));
        }

        // RepairRod action - repair a broken or severely damaged pole
        if (IsBroken || Quality < 30.0)
        {
            var survivalMod = ctx.Actor.SurvivalSkill;
            var baseDC = IsBroken ? 15 : 13;
            var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC);
            var baseScore = IsBroken ? 1.0 : 0.7;

            var resultData = parameters.ToResultData();
            resultData["__effect_handler__"] = new Action<EffectContext>(ApplyRepairRodEffect);
            
            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("repair_rod"),
                    ActionKind.Interact,
                    parameters,
                    40.0 + ctx.Random.NextDouble() * 10.0,
                    resultData,
                    new List<ResourceRequirement> { new ResourceRequirement(FishingPoleResource) }
                ),
                baseScore,
                $"Repair fishing rod{(IsBroken ? " (broken)" : "")} (quality: {Quality:F0}%, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})"
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
            case "go_fishing":
                ApplyGoFishingEffect(ctx);
                break;

            case "maintain_rod":
                ApplyMaintainRodEffect(ctx);
                break;

            case "repair_rod":
                ApplyRepairRodEffect(ctx);
                break;
        }
    }

    public void ApplyGoFishingEffect(EffectContext ctx)
    {
        if (ctx.Tier == null)
            return;

        var tier = ctx.Tier.Value;

        if (tier >= RollOutcomeTier.PartialSuccess)
        {
            // Minor quality degradation from use
            Quality = Math.Max(0.0, Quality - 1.0);
            ctx.Actor.Boredom = Math.Max(0.0, ctx.Actor.Boredom - 5.0);
        }
    }

    public void ApplyMaintainRodEffect(EffectContext ctx)
    {
        if (ctx.Tier == null)
            return;

        var tier = ctx.Tier.Value;

        if (tier >= RollOutcomeTier.PartialSuccess)
        {
            var qualityRestored = tier == RollOutcomeTier.CriticalSuccess ? 25.0 : 
                                 tier == RollOutcomeTier.Success ? 15.0 : 8.0;
            Quality = Math.Min(100.0, Quality + qualityRestored);
            ctx.Actor.Boredom = Math.Max(0.0, ctx.Actor.Boredom - 3.0);
        }
    }

    public void ApplyRepairRodEffect(EffectContext ctx)
    {
        if (ctx.Tier == null)
            return;

        var tier = ctx.Tier.Value;

        if (tier >= RollOutcomeTier.Success)
        {
            var qualityRestored = tier == RollOutcomeTier.CriticalSuccess ? 50.0 : 35.0;
            Quality = Math.Min(100.0, Quality + qualityRestored);
            
            if (IsBroken)
            {
                IsBroken = false;
                ctx.Actor.Morale = Math.Min(100.0, ctx.Actor.Morale + 10.0);
            }
            
            ctx.Actor.Boredom = Math.Max(0.0, ctx.Actor.Boredom - 8.0);
        }
    }

    public override Dictionary<string, object> SerializeToDict()
    {
        var dict = base.SerializeToDict();
        // Base ToolItem already serializes OwnerActorId, IsBroken, etc.
        return dict;
    }

    public override void DeserializeFromDict(Dictionary<string, JsonElement> data)
    {
        base.DeserializeFromDict(data);
        // Base ToolItem already deserializes OwnerActorId, IsBroken, etc.
    }
}
