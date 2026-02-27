using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.Domain.Island.Items;

namespace JohnnyLike.Domain.Island.Tests;

/// <summary>
/// Tests for v0.2 topo-sort and cycle-detection invariants.
/// </summary>
public class TopoSortTests
{
    [Fact]
    public void TopologicalSort_NoDependencies_StableIdOrder()
    {
        var world = new IslandWorldState();
        world.WorldItems.Add(new WeatherItem("weather"));
        world.WorldItems.Add(new CalendarItem("calendar"));

        // calendar < weather alphabetically, so calendar should come first
        var sorted = world.TopologicalSortTickables();

        Assert.Equal("calendar", ((WorldItem)sorted[0]).Id);
        Assert.Equal("weather", ((WorldItem)sorted[1]).Id);
    }

    [Fact]
    public void TopologicalSort_DependencyRespected_DependencyTicksFirst()
    {
        var world = new IslandWorldState();
        world.WorldItems.Add(new BeachItem("beach"));
        world.WorldItems.Add(new OceanItem("ocean"));
        // OceanItem depends on "beach"
        var sorted = world.TopologicalSortTickables();

        var ids = sorted.Select(t => ((WorldItem)t).Id).ToList();
        Assert.True(ids.IndexOf("beach") < ids.IndexOf("ocean"),
            "beach must be ticked before ocean (dependency)");
    }

    [Fact]
    public void TopologicalSort_CycleDetected_ThrowsInvalidOperation()
    {
        var world = new IslandWorldState();
        world.WorldItems.Add(new CyclicItemA("a", "b"));
        world.WorldItems.Add(new CyclicItemB("b", "a"));

        Assert.Throws<InvalidOperationException>(() => world.TopologicalSortTickables());
    }

    // ---- helpers ----

    private class CyclicItemA : WorldItem, ITickableWorldItem
    {
        private readonly string _depId;
        public CyclicItemA(string id, string depId) : base(id, "cyclic_a") { _depId = depId; RoomId = "beach"; }
        public IEnumerable<string> GetDependencies() => new[] { _depId };
        public List<TraceEvent> Tick(long currentTick, WorldState worldState) => new();
    }

    private class CyclicItemB : WorldItem, ITickableWorldItem
    {
        private readonly string _depId;
        public CyclicItemB(string id, string depId) : base(id, "cyclic_b") { _depId = depId; RoomId = "beach"; }
        public IEnumerable<string> GetDependencies() => new[] { _depId };
        public List<TraceEvent> Tick(long currentTick, WorldState worldState) => new();
    }
}
