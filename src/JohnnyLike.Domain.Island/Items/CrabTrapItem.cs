using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Supply;
using JohnnyLike.Domain.Island.Telemetry;
using JohnnyLike.Domain.Kit.Dice;
using System.Text.Json;

namespace JohnnyLike.Domain.Island.Items;

/// <summary>
/// A shared passive-capture tool. Set bait, wait, then check for crabs.
/// Uses existing <see cref="ToolItem"/> / <see cref="MaintainableWorldItem"/> quality-decay path.
/// Only one crab trap should exist at a time (like the fishing pole).
/// </summary>
public class CrabTrapItem : ToolItem, IFoodSource
{
    public const double BreakageQualityThreshold = 20.0;
    public const double MinUsableQualityThreshold = 10.0;

    /// <summary>Minimum ticks between baiting and a meaningful catch chance.</summary>
    public const long MinSoakTicks = 20L * 60 * 30; // ~30 sim-minutes

    public int BaitCharges { get; private set; }
    public long? LastBaitedTick { get; private set; }
    public long? LastCheckedTick { get; private set; }
    public bool HasBait => BaitCharges > 0;

    public CrabTrapItem(string id = "crab_trap")
        : base(id, "crab_trap", OwnershipType.Shared, baseDecayPerSecond: 0.003)
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
            >= 50.0 => "getting rusty and worn",
            >= 25.0 => "in poor condition",
            _ => "barely holding together"
        };
        using (tracer.PushPhase(TracePhase.WorldTick))
            tracer.BeatWorld($"The crab trap is {description}.", subjectId: "item:crab_trap", priority: 30);
    }

    protected override void EmitBrokenBeat(IEventTracer tracer)
    {
        using (tracer.PushPhase(TracePhase.WorldTick))
            tracer.BeatWorld("The crab trap falls apart and is no longer usable.", subjectId: "item:crab_trap", priority: 40);
    }

    public override void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        // ── add_bait_to_crab_trap ─────────────────────────────────────────────
        var sharedPile = ctx.World.SharedSupplyPile;
        var hasBaitInPile = (sharedPile?.GetQuantity<BaitSupply>() ?? 0.0) >= 1.0;
        var canAcceptBait = !IsBroken && BaitCharges == 0;

        if (canAcceptBait && hasBaitInPile)
        {
            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("add_bait_to_crab_trap"),
                    ActionKind.Interact,
                    new LocationActionParameters("camp"),
                    Duration.Minutes(2.0, 3.0, ctx.Random),
                    NarrationDescription: "add bait to crab trap"
                ),
                0.15,
                Reason: "Bait the crab trap",
                PreAction: new Func<EffectContext, bool>(effectCtx =>
                {
                    // Consume the bait atomically before the action duration starts so
                    // a concurrent actor cannot claim the same bait charge.
                    var pile = effectCtx.World.SharedSupplyPile;
                    return pile != null && pile.TryConsumeSupply<BaitSupply>(1.0);
                }),
                EffectHandler: (Action<EffectContext>)(effectCtx =>
                {
                    BaitCharges    = 1;
                    LastBaitedTick = effectCtx.World.CurrentTick;
                    effectCtx.SetOutcomeNarration($"{effectCtx.ActorId.Value} baits the crab trap and sets it in a promising spot.");
                }),
                Qualities: new Dictionary<QualityType, double>
                {
                    [QualityType.Preparation]     = 0.8,
                    [QualityType.FoodAcquisition] = 0.6
                },
                ActorRequirement: actor => CandidateRequirements.IsHuman(actor) && CandidateRequirements.AliveOnly(actor)
            ));
        }

        // ── check_crab_trap ───────────────────────────────────────────────────
        // Passive model: bait + soak time + quality determine catch chance.
        // Does not require an active crab actor — the trap catches from the
        // ambient crab population independently of the actor-lifecycle system.
        if (!IsBroken && Quality > MinUsableQualityThreshold && HasBait)
        {
            var elapsedTicks = ctx.NowTick - (LastBaitedTick ?? ctx.NowTick);
            var soakFactor   = Math.Min(1.0, (double)elapsedTicks / MinSoakTicks);

            // Catch DC decreases the longer the trap has been soaking (DC 10–14).
            // Crab is premium, so keep catches relatively rare.
            var catchDC = 14 - (int)(soakFactor * 4.0);
            if (Quality < 50.0) catchDC += 2;

            var parameters = ctx.RollSkillCheck(SkillType.Survival, catchDC);

            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("check_crab_trap"),
                    ActionKind.Interact,
                    parameters,
                    Duration.Minutes(5.0, 8.0, ctx.Random),
                    NarrationDescription: "check crab trap",
                    ResultData: parameters.ToResultData()
                ),
                0.18,
                Reason: $"Check crab trap (bait: {BaitCharges}, soak: {soakFactor:P0}, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})",
                EffectHandler: (Action<EffectContext>)(effectCtx =>
                {
                    effectCtx.World.Metrics.CrabTrapChecks++;
                    LastCheckedTick = effectCtx.World.CurrentTick;

                    // Apply active-use quality reduction
                    Quality = Math.Max(0.0, Quality - 1.5);

                    if (effectCtx.Tier == null || effectCtx.Tier.Value < RollOutcomeTier.PartialSuccess)
                    {
                        // No catch — bait consumed anyway (crab escaped or wasn't lured)
                        BaitCharges = 0;
                        effectCtx.SetOutcomeNarration($"{effectCtx.ActorId.Value} checks the trap — empty. The bait is gone.");
                        return;
                    }

                    var pile = effectCtx.World.SharedSupplyPile;
                    if (pile != null)
                    {
                        pile.AddSupply(1.0, () => new CrabSupply());
                        effectCtx.World.Metrics.CrabTrapCatches++;
                    }

                    BaitCharges = 0;
                    effectCtx.Actor.Morale += 6.0;
                    effectCtx.SetOutcomeNarration(
                        $"{effectCtx.ActorId.Value} checks the crab trap and finds a crab inside!");
                }),
                Qualities: new Dictionary<QualityType, double>
                {
                    [QualityType.FoodAcquisition] = 0.9,
                    [QualityType.Preparation]     = 0.3
                },
                ActorRequirement: actor => CandidateRequirements.IsHuman(actor) && CandidateRequirements.AliveOnly(actor)
            ));
        }

        // ── repair_crab_trap ──────────────────────────────────────────────────
        if (IsBroken || Quality < 30.0)
        {
            var baseDC = IsBroken ? 15 : 13;
            var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC);

            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("repair_crab_trap"),
                    ActionKind.Interact,
                    parameters,
                    Duration.Minutes(30.0, 40.0, ctx.Random),
                    NarrationDescription: "repair crab trap",
                    ResultData: parameters.ToResultData()
                ),
                0.15,
                Reason: $"Repair crab trap{(IsBroken ? " (broken)" : "")} (quality: {Quality:F0}%, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})",
                EffectHandler: (Action<EffectContext>)(ApplyRepairTrapEffect),
                Qualities: new Dictionary<QualityType, double>
                {
                    [QualityType.ResourcePreservation] = 1.0,
                    [QualityType.Preparation]          = 0.5,
                    [QualityType.Mastery]              = 0.4
                },
                ActorRequirement: actor => CandidateRequirements.IsHuman(actor) && CandidateRequirements.AliveOnly(actor)
            ));
        }
    }

    private void ApplyRepairTrapEffect(EffectContext ctx)
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
                ctx.Actor.Morale += 8.0;
            }

            ctx.Actor.Morale += 5.0;
            ctx.SetOutcomeNarration($"{actor} mends the crab trap, making it functional again.");
        }
        else
        {
            ctx.SetOutcomeNarration("The trap proves too damaged to repair this time.");
        }
    }

    // IFoodSource: a baited, usable trap may yield crab soon.
    double IFoodSource.GetAcquirableFoodUnits(HumanActorState actor, IslandWorldState world)
    {
        if (IsBroken || Quality <= MinUsableQualityThreshold || !HasBait)
            return 0.0;

        return 0.5; // Half a unit — crab is premium but catch is probabilistic
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
