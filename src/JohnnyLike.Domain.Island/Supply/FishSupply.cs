using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Supply;

/// <summary>
/// Raw fish caught from the ocean. Can be eaten raw (minor benefit) or cooked for better results.
/// </summary>
public class FishSupply : SupplyItem, ISupplyActionCandidate
{
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
                5.0 + ctx.Random.NextDouble() * 2.0
            ),
            0.4,
            "Eat raw fish",
            EffectHandler: (Action<EffectContext>)(effectCtx =>
            {
                effectCtx.Actor.Satiety += 10.0;
                effectCtx.Actor.Morale  -= 5.0;
            }),
            PreAction: (Func<EffectContext, bool>)(effectCtx =>
                pile.TryConsumeSupply<FishSupply>(Id, 1.0)),
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.FoodConsumption] = 0.6,
                [QualityType.Comfort]         = -0.2
            }
        ));
    }
}
