using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Stats;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Tests;

/// <summary>
/// Tests for fishing spot selection and resource reservation behavior
/// </summary>
public class FishingSpotSelectionTests
{
    private static readonly ResourceId FishingPoleResource = new("island:resource:fishing_pole");
    
    [Fact]
    public void Fishing_WithNoReservations_UsesFishingPole()
    {
        // Arrange
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        actor.Hunger = 50.0;
        world.GetStat<FishPopulationStat>("fish_population")!.FishAvailable = 100.0;
        domain.InitializeActorItems(actorId, world);
        
        var resourceAvailability = new EmptyResourceAvailability();
        
        // Act
        var candidates = domain.GenerateCandidates(
            actorId,
            actor,
            world,
            0.0,
            new Random(42),
            resourceAvailability
        );
        
        // Assert
        var fishingCandidate = candidates.FirstOrDefault(c => c.Action.Id.Value == "go_fishing");
        Assert.NotNull(fishingCandidate);
        Assert.NotNull(fishingCandidate.Action.ResourceRequirements);
        Assert.Single(fishingCandidate.Action.ResourceRequirements);
        Assert.Equal(FishingPoleResource, fishingCandidate.Action.ResourceRequirements[0].ResourceId);
    }
    
    [Fact(Skip = "Resource filtering happens in engine, not during candidate generation")]
    public void Fishing_WithPoleReserved_NoFishingCandidate()
    {
        // Note: In the current architecture, candidates are always generated with resource requirements
        // The Engine is responsible for filtering candidates based on resource availability
        // This test is not applicable to the domain pack level
    }
    
    [Fact(Skip = "Resource filtering happens in engine, not during candidate generation")]
    public void Fishing_WithBothSpotsReserved_NoFishingCandidate()
    {
        // Note: In the current architecture, candidates are always generated with resource requirements
        // The Engine is responsible for filtering candidates based on resource availability
        // This test is not applicable to the domain pack level
    }
    
    [Fact]
    public void Fishing_PoleAvailable_UsesPoleResource()
    {
        // Arrange
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        actor.Hunger = 50.0;
        world.GetStat<FishPopulationStat>("fish_population")!.FishAvailable = 100.0;
        domain.InitializeActorItems(actorId, world);
        
        var resourceAvailability = new EmptyResourceAvailability();
        
        // Act
        var candidates = domain.GenerateCandidates(
            actorId,
            actor,
            world,
            0.0,
            new Random(42),
            resourceAvailability
        );
        
        // Assert
        var fishingCandidate = candidates.FirstOrDefault(c => c.Action.Id.Value == "go_fishing");
        Assert.NotNull(fishingCandidate);
        
        // Check that the resource requirement uses fishing pole
        Assert.NotNull(fishingCandidate.Action.ResourceRequirements);
        Assert.Single(fishingCandidate.Action.ResourceRequirements);
        Assert.Equal(FishingPoleResource, fishingCandidate.Action.ResourceRequirements[0].ResourceId);
    }
    
    [Fact]
    public void Fishing_MultipleActorsWithOwnPoles_CanFishConcurrently()
    {
        // Arrange
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId1 = new ActorId("Actor1");
        var actorId2 = new ActorId("Actor2");
        var actor1 = (IslandActorState)domain.CreateActorState(actorId1);
        var actor2 = (IslandActorState)domain.CreateActorState(actorId2);
        
        actor1.Hunger = 50.0;
        actor2.Hunger = 50.0;
        world.GetStat<FishPopulationStat>("fish_population")!.FishAvailable = 100.0;
        
        // Initialize fishing poles for both actors
        domain.InitializeActorItems(actorId1, world);
        domain.InitializeActorItems(actorId2, world);
        
        var resourceAvailability = new EmptyResourceAvailability();
        
        // Act
        var candidates1 = domain.GenerateCandidates(actorId1, actor1, world, 0.0, new Random(42), resourceAvailability);
        var candidates2 = domain.GenerateCandidates(actorId2, actor2, world, 0.0, new Random(42), resourceAvailability);
        
        // Assert - Both actors should be able to fish with their own poles
        var fishingCandidate1 = candidates1.FirstOrDefault(c => c.Action.Id.Value == "go_fishing");
        var fishingCandidate2 = candidates2.FirstOrDefault(c => c.Action.Id.Value == "go_fishing");
        Assert.NotNull(fishingCandidate1);
        Assert.NotNull(fishingCandidate2);
    }
    
    /// <summary>
    /// Mock resource availability that marks specific resources as reserved
    /// </summary>
    private class MockResourceAvailability : IResourceAvailability
    {
        private readonly HashSet<ResourceId> _reservedResources;
        
        public MockResourceAvailability(params ResourceId[] reservedResources)
        {
            _reservedResources = new HashSet<ResourceId>(reservedResources);
        }
        
        public bool IsReserved(ResourceId resourceId)
        {
            return _reservedResources.Contains(resourceId);
        }
        
        public bool TryReserve(ResourceId resourceId, string utilityId, double until)
        {
            if (_reservedResources.Contains(resourceId))
                return false;
            _reservedResources.Add(resourceId);
            return true;
        }
        
        public void Release(ResourceId resourceId)
        {
            _reservedResources.Remove(resourceId);
        }
    }
}
