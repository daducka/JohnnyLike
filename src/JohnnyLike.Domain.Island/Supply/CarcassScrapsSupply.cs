using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Metabolism;

namespace JohnnyLike.Domain.Island.Supply;

/// <summary>
/// Leftover fish scraps from eating raw or cooked fish, or occasionally found on the beach.
/// Can be crafted into fishing bait.
/// Implements <see cref="ISupplyActionCandidate"/> to offer a scavenger-only
/// <c>scavenge_carcass_scraps</c> action when sufficient quantity is present.
/// </summary>
public class CarcassScrapsSupply : SupplyItem, ISupplyActionCandidate
{
    /// <summary>
    /// Satiety gained per scavenging event in kcal. Raw scraps are not very nutritious —
    /// roughly 100 kcal worth (~5 Satiety points) per scavenge action.
    /// </summary>
    private const double ScavengeKcal = 100.0;

    /// <summary>Minimum quantity of scraps required before the scavenge action is offered.</summary>
    private const double MinQuantityToScavenge = 1.0;

    /// <summary>Amount of scraps consumed by one scavenging event.</summary>
    private const double ScavengeConsumeAmount = 1.0;

    public CarcassScrapsSupply(double quantity)
        : this("carcass_scraps", quantity)
    {
    }

    public CarcassScrapsSupply(string id = "carcass_scraps", double quantity = 0.0)
        : base(id, "supply_carcass_scraps", quantity)
    {
    }

    /// <summary>
    /// Provides the <c>scavenge_carcass_scraps</c> action candidate to scavengers when
    /// there is at least <see cref="MinQuantityToScavenge"/> of scraps available.
    /// The action is gated by <see cref="CandidateRequirements.IsScavenger"/> and
    /// <see cref="CandidateRequirements.AliveOnly"/>, so humans never see it.
    /// </summary>
    public void AddCandidates(IslandContext ctx, SupplyPile pile, List<ActionCandidate> output)
    {
        if (Quantity < MinQuantityToScavenge)
            return;

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("scavenge_carcass_scraps"),
                ActionKind.Interact,
                EmptyActionParameters.Instance,
                Duration.Minutes(3.0, 5.0, ctx.Random),
                NarrationDescription: "scavenge carcass scraps"
            ),
            0.30,
            Reason: "Scavenge carcass scraps for food",
            EffectHandler: (Action<EffectContext>)(effectCtx =>
            {
                effectCtx.Actor.Satiety += MetabolismMath.CaloriesToSatietyDelta(ScavengeKcal);
                var actor = effectCtx.ActorId.Value;
                effectCtx.SetOutcomeNarration($"{actor} picks through the scraps and finds enough to eat.");
            }),
            PreAction: (Func<EffectContext, bool>)(effectCtx =>
                pile.TryConsumeSupply<CarcassScrapsSupply>(ScavengeConsumeAmount)),
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.FoodConsumption] = 1.0
            },
            ActorRequirement: actor => CandidateRequirements.IsScavenger(actor) && CandidateRequirements.AliveOnly(actor)
        ));
    }
}
