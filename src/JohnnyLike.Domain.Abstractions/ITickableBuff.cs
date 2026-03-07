namespace JohnnyLike.Domain.Abstractions;

/// <summary>
/// Interface for buffs that apply periodic effects each world tick.
/// Implement this on any <see cref="ActiveBuff"/>-derived class that needs to update
/// actor or world state continuously (e.g., metabolic decay, poison, regeneration).
///
/// Non-periodic buffs (skill bonuses, advantage markers, rain protection) do not need
/// this interface and can continue to rely on <c>ExpiresAtTick</c> for removal.
/// </summary>
public interface ITickableBuff
{
    /// <summary>
    /// Called once per world tick for each actor that carries this buff.
    /// Implementations should read <paramref name="currentTick"/> alongside any
    /// internally stored last-tick to compute the elapsed delta.
    /// </summary>
    /// <param name="actor">The actor state to read from and mutate.</param>
    /// <param name="world">The world state, available for context if needed.</param>
    /// <param name="currentTick">The current absolute engine tick counter.</param>
    void OnTick(ActorState actor, WorldState world, long currentTick);
}
