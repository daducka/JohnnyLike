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
        world.CurrentTime = 0.0;
        var reservations = new EmptyResourceAvailability();

        // Set campfire to have very little fuel
        var campfire = world.MainCampfire;
        Assert.NotNull(campfire);
        campfire.IsLit = true;
        campfire.FuelSeconds = 5.0; // Only 5 seconds of fuel

        // Act - Tick for 10 seconds (campfire should go out)
        var events = domainPack.TickWorldState(world, 10.0, reservations);

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
        world.CurrentTime = 100.0;
        var reservations = new EmptyResourceAvailability();

        // Add a shark that should expire
        var shark = new Items.SharkItem("test_shark");
        shark.ExpiresAt = 105.0; // Expires at time 105
        world.WorldItems.Add(shark);

        // Act - Tick forward 10 seconds (shark should expire at time 110)
        var events = domainPack.TickWorldState(world, 10.0, reservations);

        // Assert
        Assert.Equal(110.0, world.CurrentTime);
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
        world.CurrentTime = 0.0;
        var reservations = new EmptyResourceAvailability();

        var calendar = world.GetItem<CalendarItem>("calendar")!;
        calendar.TimeOfDay = 0.5; // noon

        // Act - Tick for 6 hours
        domainPack.TickWorldState(world, 21600.0, reservations);

        // Assert - time should have advanced ~0.25 of a day
        Assert.InRange(calendar.TimeOfDay, 0.74, 0.76);
    }

    [Fact]
    public void TickWorldState_CalendarNewDay_IncrementsDayCount()
    {
        // Arrange
        var domainPack = new IslandDomainPack();
        var world = (IslandWorldState)domainPack.CreateInitialWorldState();
        world.CurrentTime = 0.0;
        var reservations = new EmptyResourceAvailability();

        var calendar = world.GetItem<CalendarItem>("calendar")!;
        calendar.TimeOfDay = 0.9;

        // Act - tick enough to cross midnight
        domainPack.TickWorldState(world, 8640.0, reservations);

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
        engine.AdvanceTime(12960.0);

        // Assert - calendar should have advanced past midnight
        Assert.Equal(1, calendar.DayCount);
    }
}
