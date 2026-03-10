using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Metabolism;

namespace JohnnyLike.Domain.Island.Supply;

/// <summary>
/// Raw fish caught from the ocean. Can be eaten raw (minor benefit) or cooked for better results.
/// </summary>
public class FishSupply : SupplyItem, ISupplyActionCandidate
{
    // ─── Calorie value ────────────────────────────────────────────────────────
    // Less bioavailable than cooked fish due to moisture content and proteins.
    private const double Kcal = 200.0; // raw fish → +10 Satiety
    public FishSupply(double quantity)
        : this("fish", quantity)
    {
    }

    public FishSupply(string id = "fish", double quantity = 0.0)
        : base(id, "supply_fish", quantity)
    {
    }

    public void AddCandidates(IslandContext ctx, SupplyPile pile, List<ActionCandidate> output)
    {
        if (Quantity < 1.0)
            return;

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("eat_raw_fish"),
                ActionKind.Interact,
                new LocationActionParameters("camp"),
                EngineConstants.TimeToTicks(5.0, 7.0, ctx.Random),
                NarrationDescription: "eat raw fish"
            ),
            0.12,
            Reason: "Eat raw fish",
            EffectHandler: (Action<EffectContext>)(effectCtx =>
            {
                // 200 kcal raw fish → +10 Satiety (cold and unpalatable, hence the Morale hit)
                effectCtx.Actor.Satiety += MetabolismMath.CaloriesToSatietyDelta(Kcal);
                effectCtx.Actor.Morale  -= 5.0;
                var actor = effectCtx.ActorId.Value;
                effectCtx.SetOutcomeNarration($"{actor} gulps down the raw fish; cold and slimy, but it fills the belly.");

                var sharedPile = effectCtx.World.SharedSupplyPile;
                if (sharedPile != null)
                    sharedPile.AddSupply(1.0, () => new CarcassScrapsSupply());
            }),
            PreAction: (Func<EffectContext, bool>)(effectCtx =>
                pile.TryConsumeSupply<FishSupply>(1.0)),
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.FoodConsumption] = 0.6,
                [QualityType.Comfort]         = -0.2
            },
            ActorRequirement: CandidateRequirements.AliveOnly
        ));
    }
}
