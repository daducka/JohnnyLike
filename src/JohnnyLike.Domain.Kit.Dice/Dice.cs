using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Domain.Kit.Dice;

public static class Dice
{
    public static int RollD20(IRngStream rng)
    {
        return rng.Next(1, 21);
    }

    public static int RollD20WithAdvantage(IRngStream rng)
    {
        var roll1 = rng.Next(1, 21);
        var roll2 = rng.Next(1, 21);
        return Math.Max(roll1, roll2);
    }

    public static int RollD20WithDisadvantage(IRngStream rng)
    {
        var roll1 = rng.Next(1, 21);
        var roll2 = rng.Next(1, 21);
        return Math.Min(roll1, roll2);
    }
}
