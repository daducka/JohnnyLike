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
/// Permanent actor buff that tracks aliveness state, including death-save progress
/// while the actor is in the <see cref="AlivenessState.Downed"/> state.
/// Assigned to every actor at creation with <see cref="AlivenessState.Alive"/>.
/// Used by candidate requirements to gate actions on actor condition.
/// </summary>
public class AlivenessBuff : ActiveBuff
{
    /// <summary>Current aliveness state of the actor.</summary>
    public AlivenessState State { get; set; } = AlivenessState.Alive;

    /// <summary>Number of successful death saves accumulated while Downed. Resets when state changes.</summary>
    public int DeathSaveSuccesses { get; set; } = 0;

    /// <summary>Number of failed death saves accumulated while Downed. Resets when state changes.</summary>
    public int DeathSaveFailures { get; set; } = 0;

    /// <inheritdoc/>
    public override string Describe(long currentTick) =>
        State == AlivenessState.Downed
            ? $"{Name}(state={State}, saves={DeathSaveSuccesses}/3, fails={DeathSaveFailures}/3)"
            : $"{Name}(state={State})";
}
