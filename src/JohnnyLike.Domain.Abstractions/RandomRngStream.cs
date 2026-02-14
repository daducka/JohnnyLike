namespace JohnnyLike.Domain.Abstractions;

public class RandomRngStream : IRngStream
{
    private readonly Random _random;

    public RandomRngStream(Random random)
    {
        _random = random;
    }

    public int Next(int minValue, int maxValue)
    {
        return _random.Next(minValue, maxValue);
    }

    public double NextDouble()
    {
        return _random.NextDouble();
    }
}
