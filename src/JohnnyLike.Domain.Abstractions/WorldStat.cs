namespace JohnnyLike.Domain.Abstractions;

/// <summary>
/// Base class for world statistics that update over time.
/// WorldStats are ticked in dependency order to ensure proper state updates.
/// </summary>
public abstract class WorldStat : WorldItem
{
    protected WorldStat(string id, string type) : base(id, type)
    {
    }

    /// <summary>
    /// Update the stat's state based on elapsed time and world state.
    /// </summary>
    /// <param name="dtSeconds">Time elapsed in seconds since last tick</param>
    /// <param name="worldState">Current world state for accessing other stats</param>
    public abstract void Tick(double dtSeconds, WorldState worldState);

    /// <summary>
    /// Declare dependencies on other stats by their IDs.
    /// Stats will be ticked in topological order based on these dependencies.
    /// </summary>
    /// <returns>Enumerable of stat IDs this stat depends on</returns>
    public virtual IEnumerable<string> GetDependencies()
    {
        return Enumerable.Empty<string>();
    }
}
