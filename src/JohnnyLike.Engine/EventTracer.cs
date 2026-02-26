using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Engine;

/// <summary>
/// Per-simulation event tracer. Domain code calls <see cref="PushPhase"/> and
/// <see cref="Beat"/> during world ticks and action effects; the engine drains
/// the buffer at appropriate boundaries and emits the beats as
/// <c>NarrationBeat</c> <see cref="TraceEvent"/>s.
/// </summary>
public sealed class EventTracer : IEventTracer
{
    private readonly List<NarrationBeat> _beats = new();
    private readonly Stack<TracePhase> _phaseStack = new();

    private TracePhase CurrentPhase =>
        _phaseStack.Count > 0 ? _phaseStack.Peek() : TracePhase.WorldTick;

    /// <inheritdoc/>
    public IDisposable PushPhase(TracePhase phase)
    {
        _phaseStack.Push(phase);
        return new PhaseScope(this);
    }

    /// <inheritdoc/>
    public void Beat(string text, string? subjectId = null, int priority = 50, string? actorId = null)
    {
        _beats.Add(new NarrationBeat(CurrentPhase, text, subjectId, priority, actorId));
    }

    /// <inheritdoc/>
    public List<NarrationBeat> Drain()
    {
        if (_beats.Count == 0)
            return new List<NarrationBeat>();

        var result = new List<NarrationBeat>(_beats);
        _beats.Clear();
        return result;
    }

    private void PopPhase() => _phaseStack.TryPop(out _);

    private sealed class PhaseScope : IDisposable
    {
        private readonly EventTracer _tracer;
        private bool _disposed;

        public PhaseScope(EventTracer tracer) => _tracer = tracer;

        public void Dispose()
        {
            if (!_disposed)
            {
                _tracer.PopPhase();
                _disposed = true;
            }
        }
    }
}
