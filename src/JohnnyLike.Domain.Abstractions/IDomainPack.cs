namespace JohnnyLike.Domain.Abstractions;

/// <summary>
/// Represents a candidate action that an actor can take.
/// </summary>
/// <param name="Action">The action specification including ID, kind, parameters, and resource requirements</param>
/// <param name="Score">The priority/desirability score (0-1) for this action</param>
/// <param name="Reason">Optional human-readable explanation for why this action was suggested</param>
/// <param name="EffectHandler">Optional effect handler to execute when this action completes.
/// This provides explicit binding between the action and its effects, eliminating the need for
/// string-based actionId lookups. Domain implementations can provide a delegate that takes their
/// specific EffectContext type (e.g., Action&lt;EffectContext&lt;IslandActorState, IslandWorldState&gt;&gt;).</param>
public record ActionCandidate(
    ActionSpec Action,
    double Score,
    string? Reason = null,
    object? EffectHandler = null
);

public interface IDomainPack
{
    string DomainName { get; }
    
    WorldState CreateInitialWorldState();
    ActorState CreateActorState(ActorId actorId, Dictionary<string, object>? initialData = null);
    
    List<ActionCandidate> GenerateCandidates(
        ActorId actorId,
        ActorState actorState,
        WorldState worldState,
        double currentTime,
        Random rng,
        IResourceAvailability resourceAvailability);
    
    void ApplyActionEffects(
        ActorId actorId,
        ActionOutcome outcome,
        ActorState actorState,
        WorldState worldState,
        IRngStream rng,
        IResourceAvailability resourceAvailability);
    
    void OnSignal(Signal signal, ActorState? targetActor, WorldState worldState, double currentTime);
    
    List<SceneTemplate> GetSceneTemplates();
    
    bool ValidateContent(out List<string> errors);
    
    /// <summary>
    /// Returns a snapshot of actor state for tracing purposes.
    /// This is called after action effects are applied to capture the current state.
    /// </summary>
    Dictionary<string, object> GetActorStateSnapshot(ActorState actorState);
    
    /// <summary>
    /// Ticks the world state forward by the specified time delta.
    /// This method handles passive time-based world updates such as weather changes,
    /// resource regeneration, item decay, tide shifts, etc.
    /// Returns a list of trace events for significant world state changes.
    /// </summary>
    /// <param name="worldState">The world state to update</param>
    /// <param name="dtSeconds">Time delta in seconds</param>
    /// <param name="resourceAvailability">Resource availability for checking reservations</param>
    /// <returns>List of trace events for significant world state changes</returns>
    List<TraceEvent> TickWorldState(WorldState worldState, double dtSeconds, IResourceAvailability resourceAvailability);
}
