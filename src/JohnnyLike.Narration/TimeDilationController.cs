namespace JohnnyLike.Narration;

/// <summary>
/// Decides the simulation speed factor based on the current audio buffer level.
/// If the buffer is healthy (>= highWatermark), runs at 1.0x.
/// If the buffer is low (< lowWatermark), slows to <slowdownFactor>.
/// Between the two watermarks, returns the current factor unchanged (hysteresis).
/// </summary>
public sealed class TimeDilationController
{
    private readonly double _lowWatermark;
    private readonly double _highWatermark;
    private readonly double _slowdownFactor;
    private double _currentFactor;

    public double CurrentFactor => _currentFactor;

    public TimeDilationController(
        double lowWatermark = 5.0,
        double highWatermark = 15.0,
        double slowdownFactor = 0.7)
    {
        _lowWatermark = lowWatermark;
        _highWatermark = highWatermark;
        _slowdownFactor = slowdownFactor;
        _currentFactor = 1.0;
    }

    /// <summary>
    /// Given <paramref name="bufferSeconds"/> (estimated buffered audio duration),
    /// returns the recommended sim speed factor.
    /// </summary>
    public double Decide(double bufferSeconds)
    {
        if (bufferSeconds >= _highWatermark)
        {
            _currentFactor = 1.0;
        }
        else if (bufferSeconds < _lowWatermark)
        {
            _currentFactor = _slowdownFactor;
        }
        // else: stay at current factor (hysteresis band)

        return _currentFactor;
    }
}
