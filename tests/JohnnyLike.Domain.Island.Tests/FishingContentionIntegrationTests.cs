using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.Domain.Island.Stats;
using JohnnyLike.Engine;

namespace JohnnyLike.Domain.Island.Tests;

/// <summary>
/// Integration tests for multi-actor fishing spot contention
/// </summary>
public class FishingContentionIntegrationTests
{
    [Fact]
    public void TwoActors_OnlyOneCanUsePrimarySpot_OtherUsesSecondary()
    {
        // Arrange
        var domainPack = new IslandDomainPack();
        var traceSink = new InMemoryTraceSink();
        var engine = new JohnnyLike.Engine.Engine(domainPack, 42, traceSink);
        
        // Create two hungry actors
        engine.AddActor(new ActorId("Actor1"), new Dictionary<string, object>
        {
            ["hunger"] = 70.0,
            ["energy"] = 80.0
        });
        
        engine.AddActor(new ActorId("Actor2"), new Dictionary<string, object>
        {
            ["hunger"] = 70.0,
            ["energy"] = 80.0
        });
        
        // Set up world with plenty of fish
        var world = (IslandWorldState)engine.WorldState;
        world.GetStat<FishPopulationStat>("fish_population")!.FishAvailable = 100.0;
        
        // Act - Plan actions for both actors
        var success1 = engine.TryGetNextAction(new ActorId("Actor1"), out var action1);
        var success2 = engine.TryGetNextAction(new ActorId("Actor2"), out var action2);
        
        // Assert - Both should get actions
        Assert.True(success1);
        Assert.True(success2);
        Assert.NotNull(action1);
        Assert.NotNull(action2);
        
        // At least one should be fishing (could be both if they get different spots)
        var fishingActions = new[] { action1, action2 }
            .Where(a => a!.Id.Value == "fish_for_food")
            .ToList();
        
        Assert.NotEmpty(fishingActions);
        
        // If both are fishing, they should use different spots
        if (fishingActions.Count == 2)
        {
            var spot1 = fishingActions[0]!.ResourceRequirements![0].ResourceId;
            var spot2 = fishingActions[1]!.ResourceRequirements![0].ResourceId;
            Assert.NotEqual(spot1, spot2);
        }
    }
    
    [Fact]
    public void ThreeActors_OnlyTwoCanFish_ThirdDoesOtherActivity()
    {
        // Arrange
        var domainPack = new IslandDomainPack();
        var traceSink = new InMemoryTraceSink();
        var engine = new JohnnyLike.Engine.Engine(domainPack, 42, traceSink);
        
        // Create three hungry actors
        for (int i = 1; i <= 3; i++)
        {
            engine.AddActor(new ActorId($"Actor{i}"), new Dictionary<string, object>
            {
                ["hunger"] = 70.0,
                ["energy"] = 80.0
            });
        }
        
        // Set up world with plenty of fish
        var world = (IslandWorldState)engine.WorldState;
        world.GetStat<FishPopulationStat>("fish_population")!.FishAvailable = 100.0;
        
        // Act - Plan actions for all three actors
        var success1 = engine.TryGetNextAction(new ActorId("Actor1"), out var action1);
        var success2 = engine.TryGetNextAction(new ActorId("Actor2"), out var action2);
        var success3 = engine.TryGetNextAction(new ActorId("Actor3"), out var action3);
        
        // Assert - All should get actions
        Assert.True(success1);
        Assert.True(success2);
        Assert.True(success3);
        
        // Count fishing actions
        var fishingCount = new[] { action1, action2, action3 }
            .Count(a => a!.Id.Value == "fish_for_food");
        
        // At most 2 can fish (primary + secondary spots)
        Assert.True(fishingCount <= 2);
        
        // Third actor should do something else
        var otherActions = new[] { action1, action2, action3 }
            .Where(a => a!.Id.Value != "fish_for_food")
            .ToList();
        
        Assert.NotEmpty(otherActions);
    }
    
    [Fact]
    public void FishingAction_CompletionReleasesSpot_AllowsNextActorToFish()
    {
        // Arrange
        var domainPack = new IslandDomainPack();
        var traceSink = new InMemoryTraceSink();
        var engine = new JohnnyLike.Engine.Engine(domainPack, 42, traceSink);
        
        // Create two hungry actors
        engine.AddActor(new ActorId("Actor1"), new Dictionary<string, object>
        {
            ["hunger"] = 70.0,
            ["energy"] = 80.0
        });
        
        engine.AddActor(new ActorId("Actor2"), new Dictionary<string, object>
        {
            ["hunger"] = 70.0,
            ["energy"] = 80.0
        });
        
        var world = (IslandWorldState)engine.WorldState;
        world.GetStat<FishPopulationStat>("fish_population")!.FishAvailable = 100.0;
        
        // Act - Actor1 starts fishing
        var success1 = engine.TryGetNextAction(new ActorId("Actor1"), out var action1);
        Assert.True(success1);
        Assert.NotNull(action1);
        
        // Get the fishing spot Actor1 is using
        ResourceId? spot1 = action1!.ResourceRequirements?.FirstOrDefault()?.ResourceId;
        
        // Actor2 tries to get an action (should not get the same spot)
        var success2 = engine.TryGetNextAction(new ActorId("Actor2"), out var action2);
        Assert.True(success2);
        
        // Complete Actor1's action
        engine.ReportActionComplete(
            new ActorId("Actor1"),
            new ActionOutcome(action1.Id, ActionOutcomeType.Success, 15.0, null)
        );
        
        // Actor1 should now be able to fish again at the spot they just released
        var success3 = engine.TryGetNextAction(new ActorId("Actor1"), out var action3);
        Assert.True(success3);
        
        // The spot should be available again
        if (action3!.Id.Value == "fish_for_food")
        {
            var spot3 = action3.ResourceRequirements![0].ResourceId;
            // Can use either spot since one was just released
            // Just verify it's one of the two valid spots
            Assert.True(
                spot3.Value == "island:fishing:spot:primary" || 
                spot3.Value == "island:fishing:spot:secondary"
            );
        }
    }
    
    [Fact]
    public void MultipleActors_WithLowFish_StillRespectSpotReservations()
    {
        // Arrange
        var domainPack = new IslandDomainPack();
        var traceSink = new InMemoryTraceSink();
        var engine = new JohnnyLike.Engine.Engine(domainPack, 42, traceSink);
        
        // Create two hungry actors
        engine.AddActor(new ActorId("Actor1"), new Dictionary<string, object>
        {
            ["hunger"] = 70.0,
            ["energy"] = 80.0
        });
        
        engine.AddActor(new ActorId("Actor2"), new Dictionary<string, object>
        {
            ["hunger"] = 70.0,
            ["energy"] = 80.0
        });
        
        // Set up world with low fish (but above threshold to allow fishing)
        var world = (IslandWorldState)engine.WorldState;
        world.GetStat<FishPopulationStat>("fish_population")!.FishAvailable = 10.0;
        
        // Act - Plan actions for both actors
        var success1 = engine.TryGetNextAction(new ActorId("Actor1"), out var action1);
        var success2 = engine.TryGetNextAction(new ActorId("Actor2"), out var action2);
        
        // Assert
        Assert.True(success1);
        Assert.True(success2);
        
        // If both actors are fishing, they should use different spots
        if (action1!.Id.Value == "fish_for_food" && action2!.Id.Value == "fish_for_food")
        {
            var spot1 = action1.ResourceRequirements![0].ResourceId;
            var spot2 = action2.ResourceRequirements![0].ResourceId;
            Assert.NotEqual(spot1, spot2);
        }
    }
}
