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
    /// Actor-aware overload: also ticks each actor's <see cref="ITickableBuff"/> buffs.
    /// Defaults to calling the base overload (ignoring actors) for domains that do not need
    /// per-actor periodic effects. Override in domains that implement tickable buffs.
    /// </summary>
    List<TraceEvent> TickWorldState(
        WorldState worldState,
        IReadOnlyDictionary<ActorId, ActorState> actors,
        long currentTick,
        IResourceAvailability resourceAvailability)
        => TickWorldState(worldState, currentTick, resourceAvailability);

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

    /// <summary>
    /// Returns the order in which candidates should be attempted for selection.
    /// The input list is already variety-penalised and deterministically sorted by the engine.
    /// Domains can override this to implement custom attempt orderings (e.g. softmax sampling).
    /// The default implementation returns <paramref name="sortedCandidates"/> unchanged,
    /// preserving the deterministic best-first ordering for domains that do not override.
    /// </summary>
    IReadOnlyList<ActionCandidate> OrderCandidatesForSelection(
        ActorId actorId,
        ActorState actorState,
        WorldState worldState,
        long currentTick,
        IReadOnlyList<ActionCandidate> sortedCandidates,
        Random rng) => sortedCandidates;
}

