using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Domain.Island.Items;

public interface ITickableWorldItem
{
    IEnumerable<string> GetDependencies();
    List<TraceEvent> Tick(double dtSeconds, IslandWorldState world, double currentTime);
}
