namespace JohnnyLike.Domain.Abstractions;

/// <summary>
/// A strongly-typed simulation duration value.
///
/// Durations are stored internally as engine ticks (where <see cref="EngineConstants.TickHz"/>
/// ticks equal one sim-second).  Use the factory methods to construct values in
/// human-readable units:
///
/// <code>
///   Duration.Seconds(35)
///   Duration.Minutes(5)
///   Duration.Hours(2)
/// </code>
///
/// Conversion helpers and comparison operators are provided so Duration can be used
/// wherever a tick count was previously expected.
/// </summary>
public readonly struct Duration : IEquatable<Duration>, IComparable<Duration>
{
    private readonly long _ticks;

    private Duration(long ticks) => _ticks = ticks;

    // ─── Properties ───────────────────────────────────────────────────────────

    /// <summary>The duration expressed as engine ticks.</summary>
    public long Ticks => _ticks;

    /// <summary>The duration expressed in fractional sim-seconds.</summary>
    public double TotalSeconds => _ticks / (double)EngineConstants.TickHz;

    /// <summary>The duration expressed in fractional minutes.</summary>
    public double TotalMinutes => TotalSeconds / 60.0;

    /// <summary>The duration expressed in fractional hours.</summary>
    public double TotalHours => TotalSeconds / 3600.0;

    // ─── Factory methods ──────────────────────────────────────────────────────

    /// <summary>Creates a <see cref="Duration"/> from an exact number of sim-seconds.</summary>
    public static Duration Seconds(double seconds)
        => new((long)(seconds * EngineConstants.TickHz));

    /// <summary>Creates a <see cref="Duration"/> from a random value in [<paramref name="minSeconds"/>, <paramref name="maxSeconds"/>].</summary>
    public static Duration Seconds(double minSeconds, double maxSeconds, Random rng)
        => Seconds(minSeconds + rng.NextDouble() * (maxSeconds - minSeconds));

    /// <summary>Creates a <see cref="Duration"/> from an exact number of minutes.</summary>
    public static Duration Minutes(double minutes)
        => Seconds(minutes * 60.0);

    /// <summary>Creates a <see cref="Duration"/> from a random value in [<paramref name="minMinutes"/>, <paramref name="maxMinutes"/>] minutes.</summary>
    public static Duration Minutes(double minMinutes, double maxMinutes, Random rng)
        => Minutes(minMinutes + rng.NextDouble() * (maxMinutes - minMinutes));

    /// <summary>Creates a <see cref="Duration"/> from an exact number of hours.</summary>
    public static Duration Hours(double hours)
        => Minutes(hours * 60.0);

    /// <summary>Creates a <see cref="Duration"/> from a random value in [<paramref name="minHours"/>, <paramref name="maxHours"/>] hours.</summary>
    public static Duration Hours(double minHours, double maxHours, Random rng)
        => Hours(minHours + rng.NextDouble() * (maxHours - minHours));

    /// <summary>Creates a <see cref="Duration"/> directly from a raw tick count.</summary>
    public static Duration FromTicks(long ticks) => new(ticks);

    // ─── Comparison ───────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public bool Equals(Duration other) => _ticks == other._ticks;

    /// <inheritdoc/>
    public int CompareTo(Duration other) => _ticks.CompareTo(other._ticks);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Duration d && Equals(d);

    /// <inheritdoc/>
    public override int GetHashCode() => _ticks.GetHashCode();

    public static bool operator ==(Duration left, Duration right) => left._ticks == right._ticks;
    public static bool operator !=(Duration left, Duration right) => left._ticks != right._ticks;
    public static bool operator < (Duration left, Duration right) => left._ticks <  right._ticks;
    public static bool operator > (Duration left, Duration right) => left._ticks >  right._ticks;
    public static bool operator <=(Duration left, Duration right) => left._ticks <= right._ticks;
    public static bool operator >=(Duration left, Duration right) => left._ticks >= right._ticks;

    // ─── Formatting ───────────────────────────────────────────────────────────

    /// <summary>Returns a human-readable representation of the duration, e.g. <c>t=120.0s (00:02:00)</c>.</summary>
    public override string ToString()
    {
        var totalSec = (long)TotalSeconds;
        var h = totalSec / 3600;
        var m = (totalSec % 3600) / 60;
        var s = totalSec % 60;
        return $"{TotalSeconds:F1}s ({h:D2}:{m:D2}:{s:D2})";
    }
}
