namespace JohnnyLike.Domain.Island;

/// <summary>
/// Represents whether an actor is alive, downed, or dead.
/// Every actor is assigned an <see cref="AlivenessBuff"/> with <see cref="AlivenessState.Alive"/>
/// on initialization.
/// </summary>
public enum AlivenessState
{
    Alive  = 0,
    Downed = 1,
    Dead   = 2
}

/// <summary>
/// Permanent actor buff that tracks aliveness state.
/// Assigned to every actor at creation with <see cref="AlivenessState.Alive"/>.
/// Used by candidate requirements to gate actions on actor condition.
/// </summary>
public class AlivenessBuff : ActiveBuff
{
    /// <summary>Current aliveness state of the actor.</summary>
    public AlivenessState State { get; set; } = AlivenessState.Alive;

    /// <inheritdoc/>
    public override string Describe(long currentTick) => $"{Name}(state={State})";
}
