using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Metabolism;
using JohnnyLike.Domain.Island.Supply;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Items;

public class PalmFrondBlanketItem : ToolItem
{
    private static readonly ResourceId BlanketResource = new("island:resource:palm_frond_blanket");

    public PalmFrondBlanketItem(string id = "palm_frond_blanket")
        : base(id, "palm_frond_blanket", OwnershipType.Shared, baseDecayPerSecond: 0.008)
    {
    }

    public override void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        AddRepairBlanketCandidate(ctx, output);
        AddSleepInBlanketCandidate(ctx, output);
    }

    private void AddRepairBlanketCandidate(IslandContext ctx, List<ActionCandidate> output)
    {
        if (Quality >= 80.0)
            return;

        var pile = ctx.World.SharedSupplyPile;
        var frondsAvailable = pile?.GetQuantity<PalmFrondSupply>() ?? 0.0;
        if (frondsAvailable < 2.0)
            return;

        var baseDC = 11;
        var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC);

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("repair_blanket"),
                ActionKind.Interact,
                parameters,
                Duration.Minutes(25.0, 35.0, ctx.Random),
                "repair the palm frond blanket",
                parameters.ToResultData(),
                new List<ResourceRequirement> { new ResourceRequirement(BlanketResource) }
            ),
            0.22,
            Reason: $"Repair blanket (quality: {Quality:F0}%, fronds: {frondsAvailable:F0}, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})",
            EffectHandler: new Action<EffectContext>(ApplyRepairBlanketEffect),
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Comfort]              = 1.0,
                [QualityType.Safety]               = 0.5,
                [QualityType.ResourcePreservation] = 0.5
            },
            ActorRequirement: CandidateRequirements.AliveOnly
        ));
    }

    private void AddSleepInBlanketCandidate(IslandContext ctx, List<ActionCandidate> output)
    {
        // Quality-scaled base score: higher quality = better sleep, so the planner prefers repair when worn.
        var qualityFactor = Quality / 100.0;
        var baseScore = 0.16 + (0.08 * qualityFactor); // 0.16–0.24, always better than tree when healthy

        var restQuality = 0.5 + (0.5 * qualityFactor); // 0.5–1.0

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("sleep_in_blanket"),
                ActionKind.Interact,
                new LocationActionParameters("blanket"),
                Duration.Hours(5.0, 7.0, ctx.Random),
                NarrationDescription: "sleep wrapped in the palm frond blanket"
            ),
            baseScore,
            Reason: $"Sleep in blanket (quality: {Quality:F0}%)",
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
                effectCtx.Actor.Morale += 2.0 + (4.0 * qf); // 2–6 morale
                effectCtx.SetOutcomeNarration(Quality >= 60.0
                    ? $"{actor} wakes from a comfortable rest in the palm frond blanket."
                    : $"{actor} stirs from a fitful sleep; the worn blanket offers little comfort.");
            }),
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Rest]    = restQuality,
                [QualityType.Comfort] = 0.4 + (0.3 * qualityFactor),
                [QualityType.Safety]  = 0.3
            },
            ActorRequirement: CandidateRequirements.AliveOnly
        ));
    }

    public void ApplyRepairBlanketEffect(EffectContext ctx)
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

            if (pile == null || !pile.TryConsumeSupply<PalmFrondSupply>(frondsNeeded))
            {
                ctx.SetOutcomeNarration($"{actor} reaches for fronds but can't find enough to patch the blanket.");
                return;
            }

            var qualityRestored = tier == RollOutcomeTier.CriticalSuccess ? 35.0 :
                                  tier == RollOutcomeTier.Success ? 22.0 : 10.0;
            Quality = Math.Min(100.0, Quality + qualityRestored);
            ctx.Actor.Morale += 5.0;
            ctx.SetOutcomeNarration($"{actor} weaves fresh fronds into the blanket, restoring its warmth and softness.");
        }
        else
        {
            ctx.SetOutcomeNarration($"{actor}'s attempt to repair the blanket comes undone; the fronds keep slipping.");
        }
    }
}
