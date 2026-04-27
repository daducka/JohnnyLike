using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Metabolism;

namespace JohnnyLike.Domain.Island.Supply;

/// <summary>
/// A live crab caught by a human actor. Can be eaten raw or cooked for better results.
/// Produced when a human successfully catches a <see cref="CrabActorState"/>.
/// </summary>
public class CrabSupply : SupplyItem, ISupplyActionCandidate, IEdibleSupply
{
    // ─── Calorie value ────────────────────────────────────────────────────────
    // Raw crab is more calorie-dense than raw fish and better-tolerated (no morale penalty).
    private const double Kcal = 280.0; // raw crab → +14 Satiety

    public CrabSupply(double quantity)
        : this("crab", quantity)
    {
    }

    public CrabSupply(string id = "crab", double quantity = 0.0)
        : base(id, "supply_crab", quantity)
    {
    }

    public void AddCandidates(IslandContext ctx, SupplyPile pile, List<ActionCandidate> output)
    {
        if (Quantity < 1.0)
            return;

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("eat_raw_crab"),
                ActionKind.Interact,
                new LocationActionParameters("camp"),
                Duration.Minutes(5.0, 7.0, ctx.Random),
                NarrationDescription: "eat raw crab"
            ),
            0.14,
            Reason: "Eat raw crab",
            EffectHandler: (Action<EffectContext>)(effectCtx =>
            {
                // 280 kcal raw crab → +14 Satiety (better than raw fish, no morale penalty)
                effectCtx.Actor.Satiety += MetabolismMath.CaloriesToSatietyDelta(Kcal);
                effectCtx.Actor.Morale  += 1.0; // slight positive — crabs are a treat even raw
                var actor = effectCtx.ActorId.Value;
                effectCtx.SetOutcomeNarration($"{actor} cracks open the crab and eats it; surprisingly satisfying.");

                var sharedPile = effectCtx.World.SharedSupplyPile;
                if (sharedPile != null)
                    sharedPile.AddSupply(1.0, () => new CarcassScrapsSupply());
            }),
            PreAction: (Func<EffectContext, bool>)(effectCtx =>
                pile.TryConsumeSupply<CrabSupply>(1.0)),
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.FoodConsumption] = 0.7,
                [QualityType.Comfort]         = 0.1
            },
            ActorRequirement: actor => CandidateRequirements.IsHuman(actor) && CandidateRequirements.AliveOnly(actor)
        ));
    }

    // IEdibleSupply: raw crab in the supply pile can be eaten immediately.
    double IEdibleSupply.GetImmediateFoodUnits(HumanActorState actor, IslandWorldState world)
        => Quantity;
}
