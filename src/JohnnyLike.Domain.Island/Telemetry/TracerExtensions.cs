using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Domain.Island.Telemetry;

/// <summary>
/// Convenience extensions for emitting Island-domain narration beats.
/// </summary>
public static class TracerExtensions
{
    /// <summary>
    /// Emits a beat describing a world-state transition (tide, weather, day change, etc.).
    /// </summary>
    public static void BeatWorld(
        this IEventTracer tracer,
        string text,
        string? subjectId = null,
        int priority = 20)
        => tracer.Beat(text, subjectId, priority, actorId: null);

    /// <summary>
    /// Emits a beat associated with a specific actor.
    /// </summary>
    public static void BeatActor(
        this IEventTracer tracer,
        string actorId,
        string text,
        string? subjectId = null,
        int priority = 50)
        => tracer.Beat(text, subjectId, priority, actorId);
}
