using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Metabolism;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Supply;

/// <summary>
/// A coconut retrieved from a palm tree. Must be bashed open before eating.
/// </summary>
public class CoconutSupply : SupplyItem, ISupplyActionCandidate, IEdibleSupply
{
    // ─── Calorie values by outcome tier ──────────────────────────────────────
    private const double KcalCriticalSuccess = 400.0; // cracked cleanly → +20 Satiety
    private const double KcalSuccess         = 300.0; // normal success   → +15 Satiety
    private const double KcalPartialSuccess  = 160.0; // partial success  → +8  Satiety
    private const double KcalFailure         = 100.0; // only a few bites → +5  Satiety
    private const double KcalCriticalFailure =  60.0; // most spilled     → +3  Satiety
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

        // Diminishing returns: food appeal scales down when actor is already satisfied.
        // satietyFactor approaches 1 when very hungry, drops to 0 when fully satiated.
        var satietyFactor = Math.Clamp((100.0 - ctx.Actor.Satiety) / 60.0, 0.0, 1.0);

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("bash_and_eat_coconut"),
                ActionKind.Interact,
                parameters,
                Duration.Minutes(8.0, 12.0, ctx.Random),
                "bash and eat coconut",
                parameters.ToResultData()
            ),
            0.20,
            Reason: $"Bash and eat coconut (DC {baseDC}, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})",
            EffectHandler: (Action<EffectContext>)(effectCtx =>
            {
                if (effectCtx.Tier == null)
                    return;

                var actor = effectCtx.ActorId.Value;
                switch (effectCtx.Tier.Value)
                {
                    case RollOutcomeTier.CriticalSuccess:
                        // 10% of kcal as immediate Energy boost (quick glucose hit from coconut water)
                        effectCtx.Actor.Satiety += MetabolismMath.CaloriesToSatietyDelta(KcalCriticalSuccess);
                        effectCtx.Actor.Morale  += 10.0;
                        effectCtx.Actor.Energy  += MetabolismMath.CaloriesToEnergyDelta(KcalCriticalSuccess * 0.1);
                        effectCtx.SetOutcomeNarration($"{actor} cracks the coconut cleanly and savors every drop of sweet water and flesh.");
                        break;
                    case RollOutcomeTier.Success:
                        effectCtx.Actor.Satiety += MetabolismMath.CaloriesToSatietyDelta(KcalSuccess);
                        effectCtx.Actor.Morale  += 5.0;
                        effectCtx.SetOutcomeNarration($"{actor} bashes open the coconut and enjoys its meat.");
                        break;
                    case RollOutcomeTier.PartialSuccess:
                        effectCtx.Actor.Satiety += MetabolismMath.CaloriesToSatietyDelta(KcalPartialSuccess);
                        effectCtx.Actor.Morale  += 2.0;
                        effectCtx.SetOutcomeNarration($"It takes a few tries, but {actor} eventually splits the coconut and eats.");
                        break;
                    case RollOutcomeTier.Failure:
                        effectCtx.Actor.Satiety += MetabolismMath.CaloriesToSatietyDelta(KcalFailure);
                        effectCtx.SetOutcomeNarration($"{actor} barely cracks the shell and only gets a few bites.");
                        break;
                    case RollOutcomeTier.CriticalFailure:
                        effectCtx.Actor.Satiety += MetabolismMath.CaloriesToSatietyDelta(KcalCriticalFailure);
                        effectCtx.Actor.Morale  -= 5.0;
                        effectCtx.SetOutcomeNarration($"The coconut flies from {actor}'s grip, spilling most of its contents.");
                        break;
                }
            }),
            PreAction: (Func<EffectContext, bool>)(effectCtx =>
                pile.TryConsumeSupply<CoconutSupply>(1.0)),
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.FoodConsumption] = 0.8 * satietyFactor,
                [QualityType.Comfort]         = 0.1
            },
            ActorRequirement: CandidateRequirements.AliveOnly
        ));
    }

    // IEdibleSupply: coconuts in the supply pile are immediately edible (bash-and-eat).
    double IEdibleSupply.GetImmediateFoodUnits(IslandActorState actor, IslandWorldState world)
        => Quantity;
}
