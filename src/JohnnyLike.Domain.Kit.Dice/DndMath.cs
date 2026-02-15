namespace JohnnyLike.Domain.Kit.Dice;

public static class DndMath
{
    public static int AbilityModifier(int stat)
    {
        return (int)Math.Floor((stat - 10) / 2.0);
    }

    public static double EstimateSuccessChanceD20(int dc, int modifier, AdvantageType advantage)
    {
        // Calculate base success threshold
        var successThreshold = dc - modifier;
        if (successThreshold < 2)
            successThreshold = 2;
        if (successThreshold > 20)
            successThreshold = 20;

        var baseSuccessCount = 21 - successThreshold;

        double chance = advantage switch
        {
            AdvantageType.Advantage => CalculateAdvantageChance(successThreshold),
            AdvantageType.Disadvantage => CalculateDisadvantageChance(successThreshold),
            _ => baseSuccessCount / 20.0
        };

        // Respect nat-1 auto-fail: max success chance is 0.95 (19/20)
        if (chance > 0.95)
            chance = 0.95;

        // Respect nat-20 auto-success: min success chance is 0.05 (1/20)
        if (chance < 0.05)
            chance = 0.05;

        return chance;
    }

    private static double CalculateAdvantageChance(int successThreshold)
    {
        var failCount = successThreshold - 1;
        var bothFailChance = (failCount / 20.0) * (failCount / 20.0);
        return 1.0 - bothFailChance;
    }

    private static double CalculateDisadvantageChance(int successThreshold)
    {
        var successCount = 21 - successThreshold;
        var bothSucceedChance = (successCount / 20.0) * (successCount / 20.0);
        return bothSucceedChance;
    }
}
