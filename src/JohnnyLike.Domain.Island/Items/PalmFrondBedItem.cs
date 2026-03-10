using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Metabolism;
using JohnnyLike.Domain.Island.Supply;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Items;

public class PalmFrondBedItem : ToolItem
{
    private static readonly ResourceId BedResource = new("island:resource:palm_frond_bed");

    public PalmFrondBedItem(string id = "palm_frond_bed")
        : base(id, "palm_frond_bed", OwnershipType.Shared, baseDecayPerSecond: 0.004)
    {
    }

    public override void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        AddRepairBedCandidate(ctx, output);
        AddSleepInBedCandidate(ctx, output);
    }

    private void AddRepairBedCandidate(IslandContext ctx, List<ActionCandidate> output)
    {
        if (Quality >= 80.0)
            return;

        var pile = ctx.World.SharedSupplyPile;
        var frondsAvailable = pile?.GetQuantity<PalmFrondSupply>() ?? 0.0;
        var sticksAvailable = pile?.GetQuantity<StickSupply>() ?? 0.0;
        var ropeAvailable   = pile?.GetQuantity<RopeSupply>() ?? 0.0;

        if (frondsAvailable < 2.0 || sticksAvailable < 2.0 || ropeAvailable < 1.0)
            return;

        var baseDC = 13;
        var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC);

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("repair_bed"),
                ActionKind.Interact,
                parameters,
                EngineConstants.TimeToTicks(35.0, 45.0, ctx.Random),
                "repair the palm frond bed",
                parameters.ToResultData(),
                new List<ResourceRequirement> { new ResourceRequirement(BedResource) }
            ),
            0.24,
            Reason: $"Repair bed (quality: {Quality:F0}%, fronds: {frondsAvailable:F0}, sticks: {sticksAvailable:F0}, rope: {ropeAvailable:F0}, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})",
            EffectHandler: new Action<EffectContext>(ApplyRepairBedEffect),
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Comfort]              = 1.0,
                [QualityType.Safety]               = 0.6,
                [QualityType.ResourcePreservation] = 0.5
            },
            ActorRequirement: CandidateRequirements.AliveOnly
        ));
    }

    private void AddSleepInBedCandidate(IslandContext ctx, List<ActionCandidate> output)
    {
        // Quality-scaled base score: always better than blanket when healthy.
        var qualityFactor = Quality / 100.0;
        var baseScore = 0.20 + (0.10 * qualityFactor); // 0.20–0.30

        var restQuality = 0.6 + (0.4 * qualityFactor); // 0.6–1.0

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("sleep_in_bed"),
                ActionKind.Interact,
                new LocationActionParameters("bed"),
                600L + (long)(ctx.Rng.NextDouble() * 200),
                NarrationDescription: "sleep in the palm frond bed"
            ),
            baseScore,
            Reason: $"Sleep in bed (quality: {Quality:F0}%)",
            PreAction: (Func<EffectContext, bool>)(effectCtx =>
            {
                var metabolicBuff = effectCtx.Actor.ActiveBuffs.OfType<MetabolicBuff>().FirstOrDefault();
                if (metabolicBuff != null)
                    metabolicBuff.Intensity = MetabolicIntensity.Sleeping;
                return true;
            }),
            EffectHandler: new Action<EffectContext>(effectCtx =>
            {
                var actor = effectCtx.ActorId.Value;
                var qf = Quality / 100.0;
                // Morale bonus scales with quality
                effectCtx.Actor.Morale += 4.0 + (6.0 * qf); // 4–10 morale
                effectCtx.SetOutcomeNarration(Quality >= 60.0
                    ? $"{actor} rises refreshed after a deep sleep in the comfortable palm frond bed."
                    : $"{actor} wakes from an uncomfortable rest; the deteriorating bed needs repair.");
            }),
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Rest]    = restQuality,
                [QualityType.Comfort] = 0.6 + (0.3 * qualityFactor),
                [QualityType.Safety]  = 0.4
            },
            ActorRequirement: CandidateRequirements.AliveOnly
        ));
    }

    public void ApplyRepairBedEffect(EffectContext ctx)
    {
        if (ctx.Tier == null)
            return;

        var tier = ctx.Tier.Value;
        var actor = ctx.ActorId.Value;
        var pile = ctx.World.SharedSupplyPile;

        if (tier >= RollOutcomeTier.PartialSuccess)
        {
            var frondsNeeded = tier == RollOutcomeTier.CriticalSuccess ? 2.0 :
                               tier == RollOutcomeTier.Success ? 3.0 : 4.0;
            var sticksNeeded = tier == RollOutcomeTier.CriticalSuccess ? 2.0 :
                               tier == RollOutcomeTier.Success ? 3.0 : 4.0;
            var ropeNeeded = 1.0;

            if (pile == null
                || !pile.TryConsumeSupply<PalmFrondSupply>(frondsNeeded)
                || !pile.TryConsumeSupply<StickSupply>(sticksNeeded)
                || !pile.TryConsumeSupply<RopeSupply>(ropeNeeded))
            {
                ctx.SetOutcomeNarration($"{actor} starts repairing the bed but runs short of materials.");
                return;
            }

            var qualityRestored = tier == RollOutcomeTier.CriticalSuccess ? 40.0 :
                                  tier == RollOutcomeTier.Success ? 25.0 : 12.0;
            Quality = Math.Min(100.0, Quality + qualityRestored);
            ctx.Actor.Morale += 7.0;
            ctx.SetOutcomeNarration($"{actor} rebuilds the bed frame and re-layers fresh fronds, making it sturdy and comfortable again.");
        }
        else
        {
            ctx.SetOutcomeNarration($"{actor}'s repair effort falls short; the bed frame shifts but doesn't hold.");
        }
    }
}
