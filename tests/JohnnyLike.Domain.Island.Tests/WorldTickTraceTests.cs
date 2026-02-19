using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.Domain.Island.Stats;
using JohnnyLike.Engine;

namespace JohnnyLike.Domain.Island.Tests;

/// <summary>
/// Tests for world state tick trace logging functionality.
/// Verifies that significant world state changes are properly logged during time advancement.
/// </summary>
public class WorldTickTraceTests
{
    [Fact]
    public void TickWorldState_FishRegeneration_LogsTraceEvent()
    {
        // Arrange
        var domainPack = new IslandDomainPack();
        var world = (IslandWorldState)domainPack.CreateInitialWorldState();
        world.GetStat<FishPopulationStat>("fish_population")!.FishAvailable = 50.0; // Start with partial fish
        world.CurrentTime = 0.0;
        var reservations = new EmptyResourceAvailability();

        // Act - Tick for enough time to regenerate fish (1 minute = 5 fish)
        var events = domainPack.TickWorldState(world, 60.0, reservations);

        // Assert
        var fishEvent = events.FirstOrDefault(e => e.EventType == "FishRegenerated");
        Assert.NotNull(fishEvent);
        Assert.Equal(50.0, Math.Round((double)fishEvent.Details["oldAvailable"], 2));
        Assert.Equal(55.0, Math.Round((double)fishEvent.Details["newAvailable"], 2));
        Assert.Equal(5.0, Math.Round((double)fishEvent.Details["regenerated"], 2));
    }

    [Fact]
    public void TickWorldState_NewDay_LogsCoconutRegeneration()
    {
        // Arrange
        var domainPack = new IslandDomainPack();
        var world = (IslandWorldState)domainPack.CreateInitialWorldState();
        world.GetStat<TimeOfDayStat>("time_of_day")!.TimeOfDay = 0.9; // Near end of day
        world.GetStat<CoconutAvailabilityStat>("coconut_availability")!.CoconutsAvailable = 3;
        world.GetStat<TimeOfDayStat>("time_of_day")!.DayCount = 0;
        world.CurrentTime = 0.0;
        var reservations = new EmptyResourceAvailability();

        // Act - Tick past midnight (0.15 of a day = 12960 seconds)
        var events = domainPack.TickWorldState(world, 12960.0, reservations);

        // Assert
        Assert.Equal(1, world.GetStat<TimeOfDayStat>("time_of_day")!.DayCount);
        var coconutEvent = events.FirstOrDefault(e => e.EventType == "CoconutsRegenerated");
        Assert.NotNull(coconutEvent);
        Assert.Equal(3, coconutEvent.Details["oldCount"]);
        Assert.Equal(6, coconutEvent.Details["newCount"]);
        Assert.Equal(3, coconutEvent.Details["added"]);
        Assert.Equal(1, coconutEvent.Details["dayCount"]);
    }

    [Fact]
    public void TickWorldState_TideChange_LogsTraceEvent()
    {
        // Arrange
        var domainPack = new IslandDomainPack();
        var world = (IslandWorldState)domainPack.CreateInitialWorldState();
        world.GetStat<TimeOfDayStat>("time_of_day")!.TimeOfDay = 0.0; // Start at midnight, low tide
        world.GetStat<TideStat>("tide")!.TideLevel = TideLevel.Low;
        world.CurrentTime = 0.0;
        var reservations = new EmptyResourceAvailability();

        // Act - Tick forward 6 hours (0.25 of a day = 21600 seconds) to high tide
        var events = domainPack.TickWorldState(world, 21600.0, reservations);

        // Assert
        var tideEvent = events.FirstOrDefault(e => e.EventType == "TideChanged");
        Assert.NotNull(tideEvent);
        Assert.Equal("Low", tideEvent.Details["oldTide"]);
        Assert.Equal("High", tideEvent.Details["newTide"]);
    }

    [Fact]
    public void TickWorldState_CampfireExtinguished_LogsTraceEvent()
    {
        // Arrange
        var domainPack = new IslandDomainPack();
        var world = (IslandWorldState)domainPack.CreateInitialWorldState();
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
    public void EngineAdvanceTime_CallsTickWorldState_AndRecordsTraces()
    {
        // Arrange
        var domainPack = new IslandDomainPack();
        var traceSink = new InMemoryTraceSink();
        var engine = new JohnnyLike.Engine.Engine(domainPack, 42, traceSink);
        
        var world = (IslandWorldState)engine.WorldState;
        world.GetStat<FishPopulationStat>("fish_population")!.FishAvailable = 50.0;
        world.GetStat<TimeOfDayStat>("time_of_day")!.TimeOfDay = 0.9; // Near end of day
        world.GetStat<CoconutAvailabilityStat>("coconut_availability")!.CoconutsAvailable = 3;

        // Act - Advance time by enough to trigger multiple events
        engine.AdvanceTime(12960.0); // About 0.15 of a day

        // Assert
        var events = traceSink.GetEvents();
        
        // Should have coconut regeneration event
        var coconutEvent = events.FirstOrDefault(e => e.EventType == "CoconutsRegenerated");
        Assert.NotNull(coconutEvent);
        
        // Should have fish regeneration events
        var fishEvents = events.Where(e => e.EventType == "FishRegenerated").ToList();
        Assert.NotEmpty(fishEvents);
    }

    [Fact]
    public void EngineAdvanceTime_DuringLongActorSleep_WorldStateChangesIndependently()
    {
        // Arrange - This test demonstrates the key feature: world ticks independently of actor actions
        var domainPack = new IslandDomainPack();
        var traceSink = new InMemoryTraceSink();
        var engine = new JohnnyLike.Engine.Engine(domainPack, 42, traceSink);
        
        var actorId = new ActorId("Sleeper");
        engine.AddActor(actorId, new Dictionary<string, object>
        {
            ["energy"] = 10.0, // Low energy so actor will sleep
            ["satiety"] = 80.0
        });

        var world = (IslandWorldState)engine.WorldState;
        world.GetStat<FishPopulationStat>("fish_population")!.FishAvailable = 50.0;
        world.GetStat<TimeOfDayStat>("time_of_day")!.TimeOfDay = 0.1; // Early morning
        world.GetStat<TideStat>("tide")!.TideLevel = TideLevel.Low;
        
        // Get an action (should be sleep due to low energy)
        engine.TryGetNextAction(actorId, out var action);
        Assert.NotNull(action);
        Assert.Contains("sleep", action.Id.Value);

        // Actor starts sleeping (simulate a long sleep of 8 hours = 28800 seconds)
        var sleepDuration = 28800.0;
        
        // Clear traces before the main test
        traceSink.Clear();

        // Act - Advance time while actor is sleeping
        // This should trigger world state changes even though no actor action is completing
        engine.AdvanceTime(sleepDuration);

        // Assert - World should have changed during the sleep
        var events = traceSink.GetEvents();
        
        // Fish should have regenerated multiple times during the sleep
        var fishEvents = events.Where(e => e.EventType == "FishRegenerated").ToList();
        Assert.NotEmpty(fishEvents);
        
        // Tide should have changed (8 hours covers multiple tide cycles)
        var tideEvents = events.Where(e => e.EventType == "TideChanged").ToList();
        Assert.NotEmpty(tideEvents);
        
        // Verify world state actually changed
        Assert.True(world.GetStat<FishPopulationStat>("fish_population")!.FishAvailable > 50.0, "Fish should have regenerated during sleep");
        Assert.True(world.GetStat<TimeOfDayStat>("time_of_day")!.TimeOfDay > 0.1, "Time of day should have advanced");
        
        // Now complete the actor's sleep action
        engine.ReportActionComplete(actorId, new ActionOutcome(
            action.Id,
            ActionOutcomeType.Success,
            sleepDuration,
            null
        ));
        
        // Verify actor is ready for next action after sleep
        var actors = engine.Actors;
        Assert.Equal(ActorStatus.Ready, actors[actorId].Status);
    }

    [Fact]
    public void TickWorldState_NoSignificantChanges_ReturnsEmptyEventList()
    {
        // Arrange
        var domainPack = new IslandDomainPack();
        var world = (IslandWorldState)domainPack.CreateInitialWorldState();
        world.GetStat<FishPopulationStat>("fish_population")!.FishAvailable = 99.5; // Almost full
        world.CurrentTime = 0.0;
        var reservations = new EmptyResourceAvailability();

        // Act - Tick for a very short time (not enough for significant regeneration)
        var events = domainPack.TickWorldState(world, 0.1, reservations);

        // Assert - Should be no significant events (fish regen < 1.0)
        var fishEvent = events.FirstOrDefault(e => e.EventType == "FishRegenerated");
        Assert.Null(fishEvent);
    }
}
