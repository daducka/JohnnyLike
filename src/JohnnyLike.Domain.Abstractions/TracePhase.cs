namespace JohnnyLike.Domain.Abstractions;

/// <summary>
/// Identifies the simulation phase during which a trace beat was emitted.
/// </summary>
public enum TracePhase
{
    /// <summary>An action was chosen and assigned to an actor.</summary>
    ActionAssigned,

    /// <summary>An action is currently being executed.</summary>
    ActionExecuting,

    /// <summary>An action has completed (outcome known).</summary>
    ActionCompleted,

    /// <summary>The world state is being ticked forward in time.</summary>
    WorldTick,

    /// <summary>Planning / candidate generation phase (future-proof).</summary>
    Planning
}
