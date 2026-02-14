namespace JohnnyLike.Domain.Kit.Dice;

public static class DndMath
{
    public static int AbilityModifier(int stat)
    {
        return (stat - 10) / 2;
    }

    public static double EstimateSuccessChanceD20(int dc, int modifier, AdvantageType advantage)
    {
        if (dc <= 1 + modifier)
            return 1.0;

        if (dc >= 20 + modifier)
            return 0.05;

        var successThreshold = dc - modifier;
        if (successThreshold < 2)
            successThreshold = 2;
        if (successThreshold > 20)
            successThreshold = 20;

        var baseSuccessCount = 21 - successThreshold;

        return advantage switch
        {
            AdvantageType.Advantage => CalculateAdvantageChance(successThreshold),
            AdvantageType.Disadvantage => CalculateDisadvantageChance(successThreshold),
            _ => baseSuccessCount / 20.0
        };
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
