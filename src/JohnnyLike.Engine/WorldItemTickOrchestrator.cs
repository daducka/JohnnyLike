using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Engine;

/// <summary>
/// Engine-level orchestration of <see cref="ITickableWorldItem"/> execution.
/// Performs topological sort by dependency, then ticks items in stable deterministic order.
/// Domains register items via <see cref="WorldState.GetAllItems"/>; the engine calls this
/// once per <c>AdvanceTicks</c> before the domain's own <c>TickWorldState</c>.
/// </summary>
internal static class WorldItemTickOrchestrator
{
    public static List<TraceEvent> Tick(IReadOnlyList<WorldItem> worldItems, long currentTick, WorldState worldState)
    {
        var sorted = TopologicalSort(worldItems);
        var traceEvents = new List<TraceEvent>();
        foreach (var tickable in sorted)
        {
            var events = tickable.Tick(currentTick, worldState);
            traceEvents.AddRange(events);
        }
        return traceEvents;
    }

    public static List<ITickableWorldItem> TopologicalSort(IReadOnlyList<WorldItem> worldItems)
    {
        var tickables = worldItems.OfType<ITickableWorldItem>().OrderBy(t => ((WorldItem)t).Id).ToList();
        var sorted = new List<ITickableWorldItem>();
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>();
        var itemById = tickables
            .Select(t => (item: t, id: ((WorldItem)t).Id))
            .ToDictionary(x => x.id, x => x.item);
        var path = new List<string>();

        void Visit(ITickableWorldItem tickable)
        {
            var id = ((WorldItem)tickable).Id;
            if (visited.Contains(id)) return;

            if (visiting.Contains(id))
            {
                var cycleStart = path.IndexOf(id);
                var cycle = string.Join(" -> ", path.Skip(cycleStart).Append(id));
                throw new InvalidOperationException(
                    $"Circular dependency detected in WorldItems: {cycle}");
            }

            visiting.Add(id);
            path.Add(id);

            foreach (var depId in tickable.GetDependencies())
            {
                if (itemById.TryGetValue(depId, out var dep))
                    Visit(dep);
            }

            path.RemoveAt(path.Count - 1);
            visiting.Remove(id);
            visited.Add(id);
            sorted.Add(tickable);
        }

        foreach (var tickable in tickables)
            Visit(tickable);

        return sorted;
    }
}
