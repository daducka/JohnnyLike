using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Domain.Island;

/// <summary>
/// Implemented by world items that can trigger autonomous world events each tick,
/// without requiring an actor decision context.
///
/// Unlike <see cref="IIslandActionCandidate"/> (which provides candidates for an actor's
/// decision loop), implementors of this interface drive events that happen independently
/// of any actor — for example, a supply pile attracting new animal actors when certain
/// supply thresholds are met.
///
/// <see cref="IslandDomainPack.TickWorldState"/> iterates all world items implementing
/// this interface and calls <see cref="ExecuteWorldEvents"/> each tick.
/// </summary>
public interface IIslandWorldEventProvider
{
    /// <summary>
    /// Evaluate and optionally execute autonomous world events for this tick.
    /// Implementations are responsible for their own threshold checks and probability rolls.
    /// </summary>
    /// <param name="world">Current island world state.</param>
    /// <param name="currentTick">Absolute engine tick counter.</param>
    /// <param name="mutableActors">
    /// Mutable actor dictionary from the engine; may be <c>null</c> if the caller did not
    /// provide a mutable dictionary (e.g., the no-actors overload of TickWorldState).
    /// Implementations that need to add new actors must null-check this parameter.
    /// </param>
    void ExecuteWorldEvents(
        IslandWorldState world,
        long currentTick,
        Dictionary<ActorId, ActorState>? mutableActors);
}
