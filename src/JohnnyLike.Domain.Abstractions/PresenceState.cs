namespace JohnnyLike.Domain.Abstractions;

/// <summary>
/// Indicates whether a living actor is currently active in the world simulation
/// or stashed offstage.
/// </summary>
public enum PresenceState
{
    /// <summary>
    /// The actor participates in the normal simulation: candidates are generated,
    /// decisions are made, and active ticking occurs each world tick.
    /// </summary>
    Active,

    /// <summary>
    /// The actor is offstage. The normal action-decision loop is bypassed and
    /// active ticking is skipped. A dedicated stash tick hook may be invoked
    /// instead for lightweight offstage bookkeeping.
    /// </summary>
    Stashed
}
