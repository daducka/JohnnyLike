using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Supply;
using JohnnyLike.Domain.Island.Telemetry;
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

    public override void Tick(long dtTicks, IslandWorldState world)
    {
        base.Tick(dtTicks, world);
        
        // Fishing poles degrade slower than other tools but can break if quality is too low
        if (Quality < BreakageQualityThreshold && !IsBroken)
        {
            IsBroken = true;
        }
    }

    protected override void EmitDegradationBeat(IEventTracer tracer, double threshold)
    {
        var description = threshold switch
        {
            >= 75.0 => "starting to show wear",
            >= 50.0 => "getting difficult to cast",
            >= 25.0 => "starting to splinter",
            _ => "barely holding together"
        };
        using (tracer.PushPhase(TracePhase.WorldTick))
            tracer.BeatWorld($"The fishing rod is {description}.", subjectId: "item:fishing_pole", priority: 30);
    }

    protected override void EmitBrokenBeat(IEventTracer tracer)
    {
        using (tracer.PushPhase(TracePhase.WorldTick))
            tracer.BeatWorld("The fishing rod snaps and becomes unusable.", subjectId: "item:fishing_pole", priority: 40);
    }

    public override void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        // Only offer candidates if this actor owns the fishing pole
        if (!CanActorUseTool(ctx.ActorId))
            return;

        // GoFishing action - only if pole is not broken and ocean has fish
        if (!IsBroken && Quality > 10.0)
        {
            var ocean = ctx.World.GetItem<OceanItem>("ocean") as ISupplyBounty;
            var fishAvailable = ocean?.GetQuantity<FishSupply>() ?? 0.0;

            if (fishAvailable >= 1.0)
            {
                var baseDC = 12;

                if (Quality < 50.0)
                    baseDC += 2;
                else if (Quality > 80.0)
                    baseDC -= 1;

                var parameters = ctx.RollSkillCheck(SkillType.Fishing, baseDC);

                // Shared reservation context captured by both lambdas.
                BountyCollectionContext? fishCtx = null;
                var actorKey = ctx.ActorId.Value;

                output.Add(new ActionCandidate(
                    new ActionSpec(
                        new ActionId("go_fishing"),
                        ActionKind.Interact,
                        parameters,
                        EngineConstants.TimeToTicks(45.0, 60.0, ctx.Random),
                        parameters.ToResultData(),
                        new List<ResourceRequirement> { new ResourceRequirement(FishingPoleResource) }
                    ),
                    0.5,
                    Reason: $"Go fishing with pole (quality: {Quality:F0}%, fish available: {fishAvailable:F0}, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})",
                    PreAction: new Func<EffectContext, bool>(_ =>
                    {
                        if (ocean == null) return false;
                        var available = ocean.GetQuantity<FishSupply>();
                        if (available < 1.0) return false;
                        // Reserve max payout (CriticalSuccess = 2 fish)
                        ocean.ReserveSupply<FishSupply>(actorKey, Math.Min(available, 2.0));
                        fishCtx = new BountyCollectionContext(ocean, actorKey);
                        return true;
                    }),
                    EffectHandler: new Action<EffectContext>(effectCtx =>
                    {
                        if (effectCtx.Tier == null || fishCtx == null)
                        {
                            fishCtx?.Source.ReleaseReservation(fishCtx.ReservationKey);
                            return;
                        }

                        var tier = effectCtx.Tier.Value;
                        var src = fishCtx.Source;
                        var key = fishCtx.ReservationKey;

                        if (tier >= RollOutcomeTier.PartialSuccess)
                        {
                            Quality = Math.Max(0.0, Quality - 1.0);
                            effectCtx.Actor.Morale += 5.0;

                            var sharedPile = effectCtx.World.SharedSupplyPile;
                            if (sharedPile != null)
                            {
                                // CriticalSuccess commits 2 fish; Success/Partial commits 1
                                // CommitReservation returns any remainder (e.g. reserved 2, committed 1)
                                var commitFish = tier == RollOutcomeTier.CriticalSuccess ? 2.0 : 1.0;
                                src.CommitReservation<FishSupply>(key, commitFish, sharedPile, () => new FishSupply());
                            }
                            else
                            {
                                src.ReleaseReservation(key); // no shared pile — return fish to ocean
                            }

                            using (effectCtx.Tracer.PushPhase(TracePhase.ActionCompleted))
                            {
                                var actorName = effectCtx.ActorId.Value;
                                effectCtx.Tracer.BeatActor(actorName,
                                    tier == RollOutcomeTier.CriticalSuccess
                                        ? $"{actorName} hauls in two fish—a great catch."
                                        : $"{actorName} pulls a fish from the water.",
                                    subjectId: "resource:fish", priority: 60);
                            }
                        }
                        else
                        {
                            // Failure: return all reserved fish to the ocean
                            src.ReleaseReservation(key);
                            using (effectCtx.Tracer.PushPhase(TracePhase.ActionCompleted))
                                effectCtx.Tracer.BeatActor(effectCtx.ActorId.Value,
                                    "The line comes back empty.",
                                    subjectId: "resource:fish", priority: 50);
                        }
                    }),
                    Qualities: new Dictionary<QualityType, double>
                    {
                        [QualityType.FoodConsumption] = 1.0,
                        [QualityType.Efficiency]      = 0.5,
                        [QualityType.Fun]             = 0.3
                    }
                ));
            }
        }

        // MaintainRod action - maintain the pole to keep it in good condition
        if (Quality < 80.0 && !IsBroken)
        {
            var baseDC = 10;
            var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC);

            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("maintain_rod"),
                    ActionKind.Interact,
                    parameters,
                    EngineConstants.TimeToTicks(20.0, 25.0, ctx.Random),
                    parameters.ToResultData(),
                    new List<ResourceRequirement> { new ResourceRequirement(FishingPoleResource) }
                ),
                0.4,
                Reason: $"Maintain fishing rod (quality: {Quality:F0}%, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})",
                EffectHandler: new Action<EffectContext>(ApplyMaintainRodEffect),
                Qualities: new Dictionary<QualityType, double>
                {
                    [QualityType.ResourcePreservation] = 1.0,
                    [QualityType.Mastery]              = 0.5
                }
            ));
        }

        // RepairRod action - repair a broken or severely damaged pole
        if (IsBroken || Quality < 30.0)
        {
            var baseDC = IsBroken ? 15 : 13;
            var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC);

            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("repair_rod"),
                    ActionKind.Interact,
                    parameters,
                    EngineConstants.TimeToTicks(40.0, 50.0, ctx.Random),
                    parameters.ToResultData(),
                    new List<ResourceRequirement> { new ResourceRequirement(FishingPoleResource) }
                ),
                0.3,
                Reason: $"Repair fishing rod{(IsBroken ? " (broken)" : "")} (quality: {Quality:F0}%, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})",
                EffectHandler: new Action<EffectContext>(ApplyRepairRodEffect),
                Qualities: new Dictionary<QualityType, double>
                {
                    [QualityType.ResourcePreservation] = 1.0,
                    [QualityType.Preparation]          = 0.5,
                    [QualityType.Mastery]              = 0.4
                }
            ));
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
            ctx.Actor.Morale += 3.0;
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
                ctx.Actor.Morale += 10.0;
            }
            
            ctx.Actor.Morale += 8.0;
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
