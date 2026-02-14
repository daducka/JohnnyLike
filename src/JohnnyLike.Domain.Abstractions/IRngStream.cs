namespace JohnnyLike.Domain.Abstractions;

public interface IRngStream
{
    int Next(int minValue, int maxValue);
    double NextDouble();
}
