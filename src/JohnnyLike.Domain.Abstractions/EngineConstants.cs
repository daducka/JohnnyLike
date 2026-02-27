namespace JohnnyLike.Domain.Abstractions;

public static class EngineConstants
{
    /// <summary>Ticks per second. Seconds = Ticks / TickHz.</summary>
    public const int TickHz = 20;

    /// <summary>
    /// Converts seconds to ticks using <see cref="TickHz"/>.
    /// </summary>
    public static long TimeToTicks(double seconds)
        => (long)(seconds * TickHz);

    /// <summary>
    /// Converts a uniformly random duration in [minSeconds, maxSeconds] to ticks.
    /// </summary>
    public static long TimeToTicks(double minSeconds, double maxSeconds, Random rng)
        => (long)((minSeconds + rng.NextDouble() * (maxSeconds - minSeconds)) * TickHz);
}
