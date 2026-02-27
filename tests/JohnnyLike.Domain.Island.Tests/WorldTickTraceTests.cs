using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Engine;

namespace JohnnyLike.Domain.Island.Tests;

/// <summary>
/// Tests for world state tick trace logging functionality.
/// </summary>
public class WorldTickTraceTests
{
    [Fact]
    public void TickWorldState_CampfireExtinguished_LogsTraceEvent()
    {
        // Arrange
        var domainPack = new IslandDomainPack();
        var world = (IslandWorldState)domainPack.CreateInitialWorldState();
        world.WorldItems.Add(new CampfireItem("main_campfire"));
        world.CurrentTick = 0L;
        var reservations = new EmptyResourceAvailability();

        // Set campfire to have very little fuel
        var campfire = world.MainCampfire;
        Assert.NotNull(campfire);
        campfire.IsLit = true;
        campfire.FuelSeconds = 5.0; // Only 5 seconds of fuel

        // Act - Tick for 10 seconds (campfire should go out)
        var events = domainPack.TickWorldState(world, 200L, reservations);

        // Assert
        Assert.False(campfire.IsLit);
        var campfireEvent = events.FirstOrDefault(e => e.EventType == "CampfireExtinguished");
        Assert.NotNull(campfireEvent);
        Assert.Equal("main_campfire", campfireEvent.Details["itemId"]);
        Assert.True(campfireEvent.Details.ContainsKey("quality"));
    }

    [Fact]
    public void TickWorldState_ItemExpired_LogsTraceEvent()
    {
        // Arrange
        var domainPack = new IslandDomainPack();
        var world = (IslandWorldState)domainPack.CreateInitialWorldState();
        world.CurrentTick = 2000L;
        var reservations = new EmptyResourceAvailability();

        // Add a shark that should expire
        var shark = new Items.SharkItem("test_shark");
        shark.ExpiresAtTick = 2100L; // Expires at tick 2100 (= 105s)
        world.WorldItems.Add(shark);

        // Act - Tick forward to tick 2200 (= 110s)
        var events = domainPack.TickWorldState(world, 2200L, reservations);

        // Assert
        Assert.Equal(2200L, world.CurrentTick);
        Assert.DoesNotContain(shark, world.WorldItems); // Shark should be removed
        var expiryEvent = events.FirstOrDefault(e => e.EventType == "WorldItemExpired");
        Assert.NotNull(expiryEvent);
        Assert.Equal("test_shark", expiryEvent.Details["itemId"]);
        Assert.Equal("shark", expiryEvent.Details["itemType"]);
    }

    [Fact]
    public void TickWorldState_CalendarAdvances_TimeProgresses()
    {
        // Arrange
        var domainPack = new IslandDomainPack();
        var world = (IslandWorldState)domainPack.CreateInitialWorldState();
        world.CurrentTick = 0L;
        var reservations = new EmptyResourceAvailability();

        var calendar = world.GetItem<CalendarItem>("calendar")!;
        calendar.TimeOfDay = 0.5; // noon

        // Act - Tick ITickableWorldItems first (as engine does), then domain tick
        var tick = 432000L;
        foreach (var tickable in world.TopologicalSortTickables())
            tickable.Tick(tick, world);
        domainPack.TickWorldState(world, tick, reservations);

        // Assert - time should have advanced ~0.25 of a day
        Assert.InRange(calendar.TimeOfDay, 0.74, 0.76);
    }

    [Fact]
    public void TickWorldState_CalendarNewDay_IncrementsDayCount()
    {
        // Arrange
        var domainPack = new IslandDomainPack();
        var world = (IslandWorldState)domainPack.CreateInitialWorldState();
        world.CurrentTick = 0L;
        var reservations = new EmptyResourceAvailability();

        var calendar = world.GetItem<CalendarItem>("calendar")!;
        calendar.TimeOfDay = 0.9;

        // Act - Tick ITickableWorldItems first (as engine does), then domain tick
        var tick = 172800L;
        foreach (var tickable in world.TopologicalSortTickables())
            tickable.Tick(tick, world);
        domainPack.TickWorldState(world, tick, reservations);

        // Assert
        Assert.Equal(1, calendar.DayCount);
    }

    [Fact]
    public void EngineAdvanceTime_CallsTickWorldState_AndWorldChanges()
    {
        // Arrange
        var domainPack = new IslandDomainPack();
        var traceSink = new InMemoryTraceSink();
        var engine = new JohnnyLike.Engine.Engine(domainPack, 42, traceSink);

        var world = (IslandWorldState)engine.WorldState;
        var calendar = world.GetItem<CalendarItem>("calendar")!;
        calendar.TimeOfDay = 0.9;

        // Act
        engine.AdvanceTicks((long)(12960.0 * 20));

        // Assert - calendar should have advanced past midnight
        Assert.Equal(1, calendar.DayCount);
    }
}
