namespace JohnnyLike.Domain.Abstractions;

public record ActionCandidate(
    ActionSpec Action,
    double Score,
    string? Reason = null
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
}
