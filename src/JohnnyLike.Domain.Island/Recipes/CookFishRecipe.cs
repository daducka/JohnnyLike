using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Island.Supply;

namespace JohnnyLike.Domain.Island.Recipes;

/// <summary>
/// Recipe: cook raw fish over a lit campfire to produce cooked fish.
/// Inputs are consumed via PreAction; output is produced in EffectHandler.
/// </summary>
public class CookFishRecipe : IIslandRecipe
{
    public string Id => "cook_fish";

    public void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        var sharedPile = ctx.World.SharedSupplyPile;
        if (sharedPile == null)
            return;

        if (sharedPile.GetQuantity<FishSupply>("fish") < 1.0)
            return;

        var campfire = ctx.World.MainCampfire;
        if (campfire == null || !campfire.IsLit)
            return;

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("cook_fish"),
                ActionKind.Interact,
                new LocationActionParameters("campfire"),
                20.0 + ctx.Random.NextDouble() * 10.0
            ),
            0.4,
            "Cook fish over campfire",
            EffectHandler: (Action<EffectContext>)(effectCtx =>
            {
                var pile = effectCtx.World.SharedSupplyPile;
                if (pile == null)
                    return;
                pile.AddSupply("cooked_fish", 1.0, id => new CookedFishSupply(id));
            }),
            PreAction: (Func<EffectContext, bool>)(effectCtx =>
            {
                var pile = effectCtx.World.SharedSupplyPile;
                return pile != null && pile.TryConsumeSupply<FishSupply>("fish", 1.0);
            }),
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Preparation] = 0.8,
                [QualityType.Efficiency]  = 0.6
            }
        ));
    }
}
