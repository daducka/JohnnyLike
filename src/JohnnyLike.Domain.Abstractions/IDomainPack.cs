namespace JohnnyLike.Domain.Abstractions;

/// <summary>
/// Represents a candidate action that an actor can take.
/// </summary>
/// <param name="Action">The action specification including ID, kind, parameters, and resource requirements</param>
/// <param name="IntrinsicScore">Baseline desirability / opportunity score provided by candidate generators</param>
/// <param name="Qualities">Per-quality weights for domain-level scoring post-pass</param>
/// <param name="Reason">Optional human-readable explanation for why this action was suggested</param>
/// <param name="EffectHandler">Optional effect handler to execute when this action completes.</param>
/// <param name="PreAction">Optional callback executed at action start (before the action duration begins).</param>
/// <param name="Score">Final computed score assigned by the domain after gathering all candidates</param>
/// <param name="ProviderItemId">Item ID of the world item that generated this candidate, used for deterministic tie-breaking and room filtering</param>
public record ActionCandidate(
    ActionSpec Action,
    double IntrinsicScore,
    IReadOnlyDictionary<QualityType, double> Qualities,
    string? Reason = null,
    object? EffectHandler = null,
    object? PreAction = null,
    double Score = 0.0,
    string? ProviderItemId = null
);

/// <summary>Domain plug-in: world model, actor model, candidate generation, effect application.</summary>
public interface IDomainPack
{
    string DomainName { get; }
    
    WorldState CreateInitialWorldState();
    ActorState CreateActorState(ActorId actorId, Dictionary<string, object>? initialData = null);
    
    List<ActionCandidate> GenerateCandidates(
        ActorId actorId,
        ActorState actorState,
        WorldState worldState,
        long currentTick,
        Random rng,
        IResourceAvailability resourceAvailability);
    
    void ApplyActionEffects(
        ActorId actorId,
        ActionOutcome outcome,
        ActorState actorState,
        WorldState worldState,
        IRngStream rng,
        IResourceAvailability resourceAvailability,
        object? effectHandler = null);
    
    void OnSignal(Signal signal, ActorState? targetActor, WorldState worldState, long currentTick);
    
    bool ValidateContent(out List<string> errors);
    
    /// <summary>
    /// Returns a snapshot of actor state for tracing purposes.
    /// </summary>
    Dictionary<string, object> GetActorStateSnapshot(ActorState actorState);
    
    /// <summary>
    /// Ticks the world state forward to the specified absolute tick.
    /// Returns a list of trace events for significant world state changes.
    /// Note: ITickableWorldItem ticking is handled by the engine via WorldItemTickOrchestrator.
    /// </summary>
    List<TraceEvent> TickWorldState(WorldState worldState, long currentTick, IResourceAvailability resourceAvailability);

    /// <summary>
    /// Executes the pre-action handler at the moment the action is chosen and committed.
    /// Returns false if the pre-action fails, signalling the candidate should be skipped.
    /// </summary>
    bool TryExecutePreAction(
        ActorId actorId,
        ActorState actorState,
        WorldState worldState,
        IRngStream rng,
        IResourceAvailability resourceAvailability,
        object? preActionHandler) => true;
}

