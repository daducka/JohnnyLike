using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Metabolism;
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
                "bash and eat coconut",
                parameters.ToResultData()
            ),
            0.5,
            Reason: $"Bash and eat coconut (DC {baseDC}, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})",
            EffectHandler: (Action<EffectContext>)(effectCtx =>
            {
                if (effectCtx.Tier == null)
                    return;

                var actor = effectCtx.ActorId.Value;
                switch (effectCtx.Tier.Value)
                {
                    case RollOutcomeTier.CriticalSuccess:
                        // ~400 kcal → +20 Satiety; 10% of kcal as immediate Energy boost
                        effectCtx.Actor.Satiety += MetabolismMath.CaloriesToSatietyDelta(MetabolismMath.CoconutKcalCriticalSuccess);
                        effectCtx.Actor.Morale  += 10.0;
                        effectCtx.Actor.Energy  += MetabolismMath.CaloriesToEnergyDelta(MetabolismMath.CoconutKcalCriticalSuccess * 0.1);
                        effectCtx.SetOutcomeNarration($"{actor} cracks the coconut cleanly and savors every drop of sweet water and flesh.");
                        break;
                    case RollOutcomeTier.Success:
                        // ~300 kcal → +15 Satiety
                        effectCtx.Actor.Satiety += MetabolismMath.CaloriesToSatietyDelta(MetabolismMath.CoconutKcalSuccess);
                        effectCtx.Actor.Morale  += 5.0;
                        effectCtx.SetOutcomeNarration($"{actor} bashes open the coconut and enjoys its meat.");
                        break;
                    case RollOutcomeTier.PartialSuccess:
                        // ~160 kcal → +8 Satiety
                        effectCtx.Actor.Satiety += MetabolismMath.CaloriesToSatietyDelta(MetabolismMath.CoconutKcalPartialSuccess);
                        effectCtx.Actor.Morale  += 2.0;
                        effectCtx.SetOutcomeNarration($"It takes a few tries, but {actor} eventually splits the coconut and eats.");
                        break;
                    case RollOutcomeTier.Failure:
                        // ~100 kcal → +5 Satiety
                        effectCtx.Actor.Satiety += MetabolismMath.CaloriesToSatietyDelta(MetabolismMath.CoconutKcalFailure);
                        effectCtx.SetOutcomeNarration($"{actor} barely cracks the shell and only gets a few bites.");
                        break;
                    case RollOutcomeTier.CriticalFailure:
                        // ~60 kcal → +3 Satiety (most spilled)
                        effectCtx.Actor.Satiety += MetabolismMath.CaloriesToSatietyDelta(MetabolismMath.CoconutKcalCriticalFailure);
                        effectCtx.Actor.Morale  -= 5.0;
                        effectCtx.SetOutcomeNarration($"The coconut flies from {actor}'s grip, spilling most of its contents.");
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
