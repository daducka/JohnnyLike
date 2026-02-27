namespace JohnnyLike.Domain.Abstractions;

/// <summary>
/// Interface for world items that update their state each engine tick.
/// Items declare dependencies to ensure topo-sorted tick order.
/// </summary>
public interface ITickableWorldItem
{
    IEnumerable<string> GetDependencies();

    /// <summary>
    /// Called once per engine tick with the absolute current tick counter.
    /// Items compute their own delta by comparing against their stored last-tick.
    /// </summary>
    List<TraceEvent> Tick(long currentTick, WorldState worldState);
}
