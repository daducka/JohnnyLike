namespace JohnnyLike.Domain.Abstractions;

/// <summary>
/// Base class for effect execution context. Provides common data needed when applying action effects.
/// Domain-specific implementations should extend this with their own actor and world state types.
/// </summary>
/// <typeparam name="TActorState">The domain-specific actor state type</typeparam>
/// <typeparam name="TWorldState">The domain-specific world state type</typeparam>
public class EffectContext<TActorState, TWorldState>
    where TActorState : ActorState
    where TWorldState : WorldState
{
    public required ActorId ActorId { get; init; }
    public required ActionOutcome Outcome { get; init; }
    public required TActorState Actor { get; init; }
    public required TWorldState World { get; init; }
    public required IRngStream Rng { get; init; }
    public required IResourceAvailability Reservations { get; init; }
    /// <summary>
    /// Tracer for emitting narration beats from within effect handlers.
    /// Defaults to <see cref="NullEventTracer.Instance"/> when not wired up.
    /// </summary>
    public IEventTracer Tracer { get; init; } = NullEventTracer.Instance;

    /// <summary>
    /// Domain-authored narrative context describing what happened during the action.
    /// Call <see cref="SetOutcomeNarration"/> from within an effect handler to populate this.
    /// The engine propagates it into the <c>ActionCompleted</c> trace event so the
    /// narration prompt builder can include it as an <c>## Outcome Context</c> section.
    /// </summary>
    public string? OutcomeNarration { get; private set; }

    /// <summary>
    /// Sets a vivid, domain-authored sentence describing what just happened.
    /// Use this instead of <see cref="IEventTracer.Beat"/> for outcome-specific narrative
    /// so that the description is tied to the <c>ActionCompleted</c> event rather than
    /// generating a separate <c>NarrationBeat</c> world-event narration.
    /// </summary>
    public void SetOutcomeNarration(string text) => OutcomeNarration = text;
}
