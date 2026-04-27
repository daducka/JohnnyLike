using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Metabolism;

namespace JohnnyLike.Domain.Island.Supply;

/// <summary>
/// Crab that has been cooked over the campfire — more filling, great for morale,
/// and a small health bonus from the thorough cooking process.
/// </summary>
public class CookedCrabSupply : SupplyItem, ISupplyActionCandidate, IEdibleSupply
{
    // ─── Calorie value ────────────────────────────────────────────────────────
    // Cooked crab is the premium food: better satiety than cooked fish, positive morale,
    // and a small immediate energy/health boost.
    private const double Kcal = 560.0; // cooked crab → +28 Satiety, small Energy boost

    public CookedCrabSupply(double quantity)
        : this("cooked_crab", quantity)
    {
    }

    public CookedCrabSupply(string id = "cooked_crab", double quantity = 0.0)
        : base(id, "supply_cooked_crab", quantity)
    {
    }

    public void AddCandidates(IslandContext ctx, SupplyPile pile, List<ActionCandidate> output)
    {
        if (Quantity < 1.0)
            return;

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("eat_cooked_crab"),
                ActionKind.Interact,
                new LocationActionParameters("camp"),
                Duration.Minutes(8.0, 10.0, ctx.Random),
                NarrationDescription: "eat cooked crab"
            ),
            0.28,
            Reason: "Eat cooked crab",
            EffectHandler: (Action<EffectContext>)(effectCtx =>
            {
                // 560 kcal cooked crab → +28 Satiety; 5% of kcal as immediate Energy boost
                effectCtx.Actor.Satiety += MetabolismMath.CaloriesToSatietyDelta(Kcal);
                effectCtx.Actor.Morale  += 12.0; // premium meal — great morale boost
                effectCtx.Actor.Energy  += MetabolismMath.CaloriesToEnergyDelta(Kcal * 0.05);
                effectCtx.Actor.Health  += 1.0; // small health benefit from quality protein
                var actor = effectCtx.ActorId.Value;
                effectCtx.SetOutcomeNarration(
                    $"{actor} devours the perfectly cooked crab — the best meal on the island so far.");

                var sharedPile = effectCtx.World.SharedSupplyPile;
                if (sharedPile != null)
                    sharedPile.AddSupply(1.0, () => new CarcassScrapsSupply());
            }),
            PreAction: (Func<EffectContext, bool>)(effectCtx =>
                pile.TryConsumeSupply<CookedCrabSupply>(1.0)),
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.FoodConsumption] = 1.3,
                [QualityType.Comfort]         = 0.5
            },
            ActorRequirement: actor => CandidateRequirements.IsHuman(actor) && CandidateRequirements.AliveOnly(actor)
        ));
    }

    // IEdibleSupply: cooked crab in the supply pile can be eaten immediately.
    double IEdibleSupply.GetImmediateFoodUnits(HumanActorState actor, IslandWorldState world)
        => Quantity;
}
