using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Supply;
using JohnnyLike.Domain.Island.Telemetry;
using JohnnyLike.Domain.Kit.Dice;
using System.Text.Json;

namespace JohnnyLike.Domain.Island.Items;

/// <summary>
/// A shared passive net that can catch multiple fish per check.
/// Mid-game upgrade over the fishing pole: baited, left in water, then checked.
/// Uses existing <see cref="ToolItem"/> / <see cref="MaintainableWorldItem"/> quality-decay path.
/// Only one fishing net should exist at a time (like the fishing pole).
/// </summary>
public class FishingNetItem : ToolItem, IFoodSource
{
    public const double BreakageQualityThreshold = 20.0;
    public const double MinUsableQualityThreshold = 10.0;

    /// <summary>Minimum ticks between baiting and a meaningful catch chance.</summary>
    public const long MinSoakTicks = 20L * 60 * 20; // ~20 sim-minutes

    public int BaitCharges { get; private set; }
    public long? LastBaitedTick { get; private set; }
    public long? LastCheckedTick { get; private set; }
    public bool HasBait => BaitCharges > 0;

    public FishingNetItem(string id = "fishing_net")
        : base(id, "fishing_net", OwnershipType.Shared, baseDecayPerSecond: 0.004)
    {
    }

    public override void Tick(long dtTicks, IslandWorldState world)
    {
        base.Tick(dtTicks, world);

        if (Quality < BreakageQualityThreshold && !IsBroken)
            IsBroken = true;
    }

    protected override void EmitDegradationBeat(IEventTracer tracer, double threshold)
    {
        var description = threshold switch
        {
            >= 75.0 => "showing early wear",
            >= 50.0 => "fraying in places",
            >= 25.0 => "badly torn",
            _ => "barely holding together"
        };
        using (tracer.PushPhase(TracePhase.WorldTick))
            tracer.BeatWorld($"The fishing net is {description}.", subjectId: "item:fishing_net", priority: 30);
    }

    protected override void EmitBrokenBeat(IEventTracer tracer)
    {
        using (tracer.PushPhase(TracePhase.WorldTick))
            tracer.BeatWorld("The fishing net tears apart and is no longer usable.", subjectId: "item:fishing_net", priority: 40);
    }

    public override void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        var sharedPile = ctx.World.SharedSupplyPile;
        var hasBaitInPile = (sharedPile?.GetQuantity<BaitSupply>() ?? 0.0) >= 1.0;
        var canAcceptBait = !IsBroken && BaitCharges == 0;

        // ── add_bait_to_fishing_net ───────────────────────────────────────────
        if (canAcceptBait && hasBaitInPile)
        {
            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("add_bait_to_fishing_net"),
                    ActionKind.Interact,
                    new LocationActionParameters("camp"),
                    Duration.Minutes(3.0, 5.0, ctx.Random),
                    NarrationDescription: "add bait to fishing net"
                ),
                0.15,
                Reason: "Bait the fishing net",
                EffectHandler: (Action<EffectContext>)(effectCtx =>
                {
                    var pile = effectCtx.World.SharedSupplyPile;
                    if (pile == null) return;
                    pile.TryConsumeSupply<BaitSupply>(1.0);
                    BaitCharges = 1;
                    LastBaitedTick = effectCtx.World.CurrentTick;
                    effectCtx.SetOutcomeNarration($"{effectCtx.ActorId.Value} baits and deploys the fishing net in the shallows.");
                }),
                Qualities: new Dictionary<QualityType, double>
                {
                    [QualityType.Preparation]     = 0.8,
                    [QualityType.FoodAcquisition] = 0.7
                },
                ActorRequirement: actor => CandidateRequirements.IsHuman(actor) && CandidateRequirements.AliveOnly(actor)
            ));
        }

        // ── check_fishing_net ─────────────────────────────────────────────────
        if (!IsBroken && Quality > MinUsableQualityThreshold && HasBait)
        {
            var ocean = ctx.World.GetItem<OceanItem>("ocean") as ISupplyBounty;
            var fishAvailable = ocean?.GetQuantity<FishSupply>() ?? 0.0;

            if (fishAvailable >= 1.0)
            {
                var elapsedTicks = ctx.NowTick - (LastBaitedTick ?? ctx.NowTick);
                var soakFactor   = Math.Min(1.0, (double)elapsedTicks / MinSoakTicks);

                var catchDC  = 10 - (int)(soakFactor * 3.0); // DC 7–10
                if (Quality < 50.0) catchDC += 2;

                var parameters = ctx.RollSkillCheck(SkillType.Fishing, catchDC);
                var actorKey   = ctx.ActorId.Value;

                // Reserve up to 3 fish (CriticalSuccess = 3, Success = 2, Partial = 1)
                BountyCollectionContext? fishCtx = null;

                output.Add(new ActionCandidate(
                    new ActionSpec(
                        new ActionId("check_fishing_net"),
                        ActionKind.Interact,
                        parameters,
                        Duration.Minutes(10.0, 15.0, ctx.Random),
                        NarrationDescription: "check fishing net",
                        ResultData: parameters.ToResultData()
                    ),
                    0.22,
                    Reason: $"Check fishing net (bait: {BaitCharges}, soak: {soakFactor:P0}, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})",
                    PreAction: new Func<EffectContext, bool>(_ =>
                    {
                        if (ocean == null) return false;
                        var available = ocean.GetQuantity<FishSupply>();
                        if (available < 1.0) return false;
                        ocean.ReserveSupply<FishSupply>(actorKey, Math.Min(available, 3.0));
                        fishCtx = new BountyCollectionContext(ocean, actorKey);
                        return true;
                    }),
                    EffectHandler: (Action<EffectContext>)(effectCtx =>
                    {
                        effectCtx.World.Metrics.FishingNetChecks++;
                        LastCheckedTick = effectCtx.World.CurrentTick;
                        Quality = Math.Max(0.0, Quality - 2.0);
                        BaitCharges = 0;

                        if (effectCtx.Tier == null || fishCtx == null ||
                            effectCtx.Tier.Value < RollOutcomeTier.PartialSuccess)
                        {
                            fishCtx?.Source.ReleaseReservation(fishCtx.ReservationKey);
                            effectCtx.SetOutcomeNarration($"{effectCtx.ActorId.Value} pulls in the net — empty this time.");
                            return;
                        }

                        var tier = effectCtx.Tier.Value;
                        var commitFish = tier == RollOutcomeTier.CriticalSuccess ? 3.0 :
                                         tier == RollOutcomeTier.Success         ? 2.0 : 1.0;

                        var pile = effectCtx.World.SharedSupplyPile;
                        if (pile != null)
                        {
                            fishCtx.Source.CommitReservation<FishSupply>(
                                fishCtx.ReservationKey, commitFish, pile, () => new FishSupply());
                        }
                        else
                        {
                            fishCtx.Source.ReleaseReservation(fishCtx.ReservationKey);
                        }

                        effectCtx.World.Metrics.FishingNetCatches++;
                        effectCtx.World.Metrics.FishCaught += (int)commitFish;

                        effectCtx.Actor.Morale += 8.0;
                        effectCtx.SetOutcomeNarration(
                            $"{effectCtx.ActorId.Value} hauls in the net and finds {(int)commitFish} fish inside!");
                    }),
                    Qualities: new Dictionary<QualityType, double>
                    {
                        [QualityType.FoodAcquisition] = 1.0,
                        [QualityType.Efficiency]      = 0.6,
                        [QualityType.Fun]             = 0.2
                    },
                    ActorRequirement: actor => CandidateRequirements.IsHuman(actor) && CandidateRequirements.AliveOnly(actor)
                ));
            }
        }

        // ── repair_fishing_net ────────────────────────────────────────────────
        if (IsBroken || Quality < 30.0)
        {
            var baseDC = IsBroken ? 16 : 14; // harder than rod repair — the net is rope-heavy
            var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC);

            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("repair_fishing_net"),
                    ActionKind.Interact,
                    parameters,
                    Duration.Minutes(40.0, 50.0, ctx.Random),
                    NarrationDescription: "repair fishing net",
                    ResultData: parameters.ToResultData()
                ),
                0.15,
                Reason: $"Repair fishing net{(IsBroken ? " (broken)" : "")} (quality: {Quality:F0}%, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})",
                EffectHandler: (Action<EffectContext>)(ApplyRepairNetEffect),
                Qualities: new Dictionary<QualityType, double>
                {
                    [QualityType.ResourcePreservation] = 1.0,
                    [QualityType.Preparation]          = 0.6,
                    [QualityType.Mastery]              = 0.5
                },
                ActorRequirement: actor => CandidateRequirements.IsHuman(actor) && CandidateRequirements.AliveOnly(actor)
            ));
        }
    }

    private void ApplyRepairNetEffect(EffectContext ctx)
    {
        if (ctx.Tier == null)
            return;

        var tier = ctx.Tier.Value;
        var actor = ctx.ActorId.Value;

        if (tier >= RollOutcomeTier.Success)
        {
            var qualityRestored = tier == RollOutcomeTier.CriticalSuccess ? 50.0 : 35.0;
            Quality = Math.Min(100.0, Quality + qualityRestored);

            if (IsBroken)
            {
                IsBroken = false;
                ctx.Actor.Morale += 10.0;
            }

            ctx.Actor.Morale += 5.0;
            ctx.SetOutcomeNarration($"{actor} re-ties the torn sections of the net, making it usable again.");
        }
        else
        {
            ctx.SetOutcomeNarration("The net is too badly torn to repair this time.");
        }
    }

    // IFoodSource: a baited, usable net in a fish-bearing ocean may yield fish.
    double IFoodSource.GetAcquirableFoodUnits(HumanActorState actor, IslandWorldState world)
    {
        if (IsBroken || Quality <= MinUsableQualityThreshold || !HasBait)
            return 0.0;

        var ocean = world.GetItem<OceanItem>("ocean") as ISupplyBounty;
        var fishAvailable = ocean?.GetQuantity<FishSupply>() ?? 0.0;
        return Math.Min(fishAvailable, 2.0); // net can catch up to 2–3 but return conservative estimate
    }

    public override Dictionary<string, object> SerializeToDict()
    {
        var dict = base.SerializeToDict();
        dict["BaitCharges"]     = BaitCharges;
        dict["LastBaitedTick"]  = LastBaitedTick.HasValue  ? (object)LastBaitedTick.Value  : (object)"";
        dict["LastCheckedTick"] = LastCheckedTick.HasValue ? (object)LastCheckedTick.Value : (object)"";
        return dict;
    }

    public override void DeserializeFromDict(Dictionary<string, JsonElement> data)
    {
        base.DeserializeFromDict(data);

        if (data.TryGetValue("BaitCharges", out var bc))
            BaitCharges = bc.GetInt32();

        if (data.TryGetValue("LastBaitedTick", out var lbt))
        {
            if (lbt.ValueKind == JsonValueKind.Number)
                LastBaitedTick = lbt.GetInt64();
        }

        if (data.TryGetValue("LastCheckedTick", out var lct))
        {
            if (lct.ValueKind == JsonValueKind.Number)
                LastCheckedTick = lct.GetInt64();
        }
    }
}
