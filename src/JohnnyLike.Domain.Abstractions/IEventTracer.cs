namespace JohnnyLike.Domain.Abstractions;

/// <summary>
/// A queued narration beat captured by <see cref="IEventTracer"/>.
/// Drained by the engine and emitted as <c>NarrationBeat</c> trace events.
/// </summary>
/// <param name="Phase">Simulation phase when the beat was emitted.</param>
/// <param name="Text">One-sentence domain-authored summary of the event.</param>
/// <param name="SubjectId">Optional stable identifier for the narrative subject (e.g. "beach:tide", "resource:fish").</param>
/// <param name="Priority">0–100 importance; higher values are more likely to surface in prompts.</param>
/// <param name="ActorId">Optional actor that caused or is associated with this beat.</param>
public sealed record NarrationBeat(
    TracePhase Phase,
    string Text,
    string? SubjectId,
    int Priority,
    string? ActorId
);

/// <summary>
/// Per-simulation tracer that domain code calls to emit narration-ready beats.
/// Use <see cref="PushPhase"/> to bracket the current simulation phase;
/// call <see cref="Beat"/> to enqueue a narrative beat for that phase.
/// The engine drains beats via <see cref="Drain"/> and converts them to
/// <c>NarrationBeat</c> <see cref="TraceEvent"/>s.
/// </summary>
public interface IEventTracer
{
    /// <summary>
    /// Sets the current simulation phase for the duration of the returned scope.
    /// Calls can be nested; the previous phase is restored when the scope is disposed.
    /// </summary>
    IDisposable PushPhase(TracePhase phase);

    /// <summary>
    /// Enqueues a narration beat tagged with the current phase.
    /// </summary>
    void Beat(string text, string? subjectId = null, int priority = 50, string? actorId = null);

    /// <summary>
    /// Returns all queued beats and clears the internal buffer.
    /// </summary>
    List<NarrationBeat> Drain();
}

/// <summary>
/// No-op implementation used when no tracer is wired up.
/// </summary>
public sealed class NullEventTracer : IEventTracer
{
    /// <summary>Shared singleton; safe because the implementation is stateless.</summary>
    public static readonly NullEventTracer Instance = new();

    private NullEventTracer() { }

    public IDisposable PushPhase(TracePhase phase) => NullScope.Instance;
    public void Beat(string text, string? subjectId = null, int priority = 50, string? actorId = null) { }
    public List<NarrationBeat> Drain() => new();

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        private NullScope() { }
        public void Dispose() { }
    }
}
