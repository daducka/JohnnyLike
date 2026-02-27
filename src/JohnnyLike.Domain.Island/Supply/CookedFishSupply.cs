using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;

namespace JohnnyLike.Domain.Island.Supply;

/// <summary>
/// Fish that has been cooked over the campfire — more filling and better for morale.
/// </summary>
public class CookedFishSupply : SupplyItem, ISupplyActionCandidate
{
    public CookedFishSupply(double quantity)
        : this("cooked_fish", quantity)
    {
    }

    public CookedFishSupply(string id = "cooked_fish", double quantity = 0.0)
        : base(id, "supply_cooked_fish", quantity)
    {
    }

    public void AddCandidates(IslandContext ctx, SupplyPile pile, List<ActionCandidate> output)
    {
        if (Quantity < 1.0)
            return;

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("eat_cooked_fish"),
                ActionKind.Interact,
                new LocationActionParameters("camp"),
                EngineConstants.TimeToTicks(8.0, 10.0, ctx.Random)
            ),
            0.6,
            "Eat cooked fish",
            EffectHandler: (Action<EffectContext>)(effectCtx =>
            {
                effectCtx.Actor.Satiety += 20.0;
                effectCtx.Actor.Morale  += 5.0;
            }),
            PreAction: (Func<EffectContext, bool>)(effectCtx =>
                pile.TryConsumeSupply<CookedFishSupply>(1.0)),
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.FoodConsumption] = 1.0,
                [QualityType.Comfort]         = 0.3
            }
        ));
    }
}
