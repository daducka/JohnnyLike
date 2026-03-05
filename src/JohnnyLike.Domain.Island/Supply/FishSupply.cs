using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Metabolism;

namespace JohnnyLike.Domain.Island.Supply;

/// <summary>
/// Raw fish caught from the ocean. Can be eaten raw (minor benefit) or cooked for better results.
/// </summary>
public class FishSupply : SupplyItem, ISupplyActionCandidate
{
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
            0.4,
            Reason: "Eat raw fish",
            EffectHandler: (Action<EffectContext>)(effectCtx =>
            {
                // 200 kcal raw fish → +10 Satiety (cold and unpalatable, hence the Morale hit)
                effectCtx.Actor.Satiety += MetabolismMath.CaloriesToSatietyDelta(MetabolismMath.RawFishKcal);
                effectCtx.Actor.Morale  -= 5.0;
                var actor = effectCtx.ActorId.Value;
                effectCtx.SetOutcomeNarration($"{actor} gulps down the raw fish; cold and slimy, but it fills the belly.");
            }),
            PreAction: (Func<EffectContext, bool>)(effectCtx =>
                pile.TryConsumeSupply<FishSupply>(1.0)),
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.FoodConsumption] = 0.6,
                [QualityType.Comfort]         = -0.2
            }
        ));
    }
}
