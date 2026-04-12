using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Domain.Island.Supply;

/// <summary>
/// Implemented by supply items that can trigger autonomous world events each tick
/// when hosted inside a <see cref="SupplyPile"/>.
///
/// <see cref="SupplyPile"/> implements <see cref="IIslandWorldEventProvider"/> by
/// delegating to each of its supplies that implements this interface.
/// </summary>
public interface ISupplyWorldEventProvider
{
    /// <summary>
    /// Evaluate and optionally execute world events driven by this supply item.
    /// </summary>
    /// <param name="pile">The containing supply pile.</param>
    /// <param name="world">Current island world state.</param>
    /// <param name="currentTick">Absolute engine tick counter.</param>
    /// <param name="mutableActors">
    /// Mutable actor dictionary from the engine; may be <c>null</c>.
    /// Implementations that need to add new actors must null-check this parameter.
    /// </param>
    void ExecuteWorldEvents(
        SupplyPile pile,
        IslandWorldState world,
        long currentTick,
        Dictionary<ActorId, ActorState>? mutableActors);
}
