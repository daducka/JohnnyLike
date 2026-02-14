using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Domain.Kit.Dice;

public static class SkillCheckResolver
{
    public static SkillCheckResult Resolve(IRngStream rng, SkillCheckRequest request)
    {
        var roll = request.Advantage switch
        {
            AdvantageType.Advantage => Dice.RollD20WithAdvantage(rng),
            AdvantageType.Disadvantage => Dice.RollD20WithDisadvantage(rng),
            _ => Dice.RollD20(rng)
        };

        var total = roll + request.Modifier;
        var isSuccess = roll == 20 || (roll != 1 && total >= request.DC);

        var outcomeTier = DetermineOutcomeTier(roll, total, request.DC);

        var estimatedSuccessChance = DndMath.EstimateSuccessChanceD20(
            request.DC,
            request.Modifier,
            request.Advantage
        );

        return new SkillCheckResult(
            roll,
            total,
            outcomeTier,
            isSuccess,
            estimatedSuccessChance
        );
    }

    private static RollOutcomeTier DetermineOutcomeTier(int roll, int total, int dc)
    {
        if (roll == 1)
            return RollOutcomeTier.CriticalFailure;

        if (roll == 20)
            return RollOutcomeTier.CriticalSuccess;

        if (total >= dc)
        {
            var margin = total - dc;
            if (margin >= 5)
                return RollOutcomeTier.CriticalSuccess;
            return RollOutcomeTier.Success;
        }

        var shortfall = dc - total;
        if (shortfall <= 2)
            return RollOutcomeTier.PartialSuccess;

        return RollOutcomeTier.Failure;
    }
}
