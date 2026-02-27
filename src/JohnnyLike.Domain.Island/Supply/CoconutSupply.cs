using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Supply;

/// <summary>
/// A coconut retrieved from a palm tree. Must be bashed open before eating.
/// </summary>
public class CoconutSupply : SupplyItem, ISupplyActionCandidate
{
    public CoconutSupply(double quantity)
        : this("coconut", quantity)
    {
    }

    public CoconutSupply(string id = "coconut", double quantity = 0.0)
        : base(id, "supply_coconut", quantity)
    {
    }

    public void AddCandidates(IslandContext ctx, SupplyPile pile, List<ActionCandidate> output)
    {
        if (Quantity < 1.0)
            return;

        var baseDC = 10;
        var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC);

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("bash_and_eat_coconut"),
                ActionKind.Interact,
                parameters,
                EngineConstants.TimeToTicks(8.0, 12.0, ctx.Random),
                parameters.ToResultData()
            ),
            0.5,
            $"Bash and eat coconut (DC {baseDC}, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})",
            EffectHandler: (Action<EffectContext>)(effectCtx =>
            {
                if (effectCtx.Tier == null)
                    return;

                switch (effectCtx.Tier.Value)
                {
                    case RollOutcomeTier.CriticalSuccess:
                        effectCtx.Actor.Satiety += 25.0;
                        effectCtx.Actor.Morale  += 10.0;
                        effectCtx.Actor.Energy  += 5.0;
                        break;
                    case RollOutcomeTier.Success:
                        effectCtx.Actor.Satiety += 15.0;
                        effectCtx.Actor.Morale  += 5.0;
                        break;
                    case RollOutcomeTier.PartialSuccess:
                        effectCtx.Actor.Satiety += 8.0;
                        effectCtx.Actor.Morale  += 2.0;
                        break;
                    case RollOutcomeTier.Failure:
                        effectCtx.Actor.Satiety += 5.0;
                        break;
                    case RollOutcomeTier.CriticalFailure:
                        effectCtx.Actor.Satiety += 3.0;
                        effectCtx.Actor.Morale  -= 5.0;
                        break;
                }
            }),
            PreAction: (Func<EffectContext, bool>)(effectCtx =>
                pile.TryConsumeSupply<CoconutSupply>(1.0)),
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.FoodConsumption] = 0.8,
                [QualityType.Comfort]         = 0.1
            }
        ));
    }
}
