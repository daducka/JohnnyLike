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
    private static readonly ResourceId PrimaryFishingSpot = new("island:fishing:spot:primary");
    private static readonly ResourceId SecondaryFishingSpot = new("island:fishing:spot:secondary");
    
    [Fact]
    public void Fishing_WithNoReservations_UsesPrimarySpot()
    {
        // Arrange
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        actor.Hunger = 50.0;
        world.GetStat<FishPopulationStat>("fish_population")!.FishAvailable = 100.0;
        
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
        var fishingCandidate = candidates.FirstOrDefault(c => c.Action.Id.Value == "fish_for_food");
        Assert.NotNull(fishingCandidate);
        Assert.NotNull(fishingCandidate.Action.ResourceRequirements);
        Assert.Single(fishingCandidate.Action.ResourceRequirements);
        Assert.Equal(PrimaryFishingSpot, fishingCandidate.Action.ResourceRequirements[0].ResourceId);
    }
    
    [Fact]
    public void Fishing_WithPrimaryReserved_UsesSecondarySpot()
    {
        // Arrange
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        actor.Hunger = 50.0;
        world.GetStat<FishPopulationStat>("fish_population")!.FishAvailable = 100.0;
        
        var resourceAvailability = new MockResourceAvailability(PrimaryFishingSpot);
        
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
        var fishingCandidate = candidates.FirstOrDefault(c => c.Action.Id.Value == "fish_for_food");
        Assert.NotNull(fishingCandidate);
        Assert.NotNull(fishingCandidate.Action.ResourceRequirements);
        Assert.Single(fishingCandidate.Action.ResourceRequirements);
        Assert.Equal(SecondaryFishingSpot, fishingCandidate.Action.ResourceRequirements[0].ResourceId);
    }
    
    [Fact]
    public void Fishing_WithBothSpotsReserved_NoFishingCandidate()
    {
        // Arrange
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        actor.Hunger = 50.0;
        world.GetStat<FishPopulationStat>("fish_population")!.FishAvailable = 100.0;
        
        var resourceAvailability = new MockResourceAvailability(PrimaryFishingSpot, SecondaryFishingSpot);
        
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
        var fishingCandidate = candidates.FirstOrDefault(c => c.Action.Id.Value == "fish_for_food");
        Assert.Null(fishingCandidate);
        
        // Should have other candidates, just not fishing
        Assert.NotEmpty(candidates);
    }
    
    [Fact]
    public void Fishing_SecondarySpot_UsesSecondaryResource()
    {
        // Arrange
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        actor.Hunger = 50.0;
        world.GetStat<FishPopulationStat>("fish_population")!.FishAvailable = 100.0;
        
        var resourceAvailability = new MockResourceAvailability(PrimaryFishingSpot);
        
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
        var fishingCandidate = candidates.FirstOrDefault(c => c.Action.Id.Value == "fish_for_food");
        Assert.NotNull(fishingCandidate);
        
        // Check that the resource requirement uses secondary spot
        Assert.NotNull(fishingCandidate.Action.ResourceRequirements);
        Assert.Single(fishingCandidate.Action.ResourceRequirements);
        Assert.Equal(SecondaryFishingSpot, fishingCandidate.Action.ResourceRequirements[0].ResourceId);
    }
    
    [Fact]
    public void Fishing_PrimarySpot_UsesPrimaryResource()
    {
        // Arrange
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        actor.Hunger = 50.0;
        world.GetStat<FishPopulationStat>("fish_population")!.FishAvailable = 100.0;
        
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
        var fishingCandidate = candidates.FirstOrDefault(c => c.Action.Id.Value == "fish_for_food");
        Assert.NotNull(fishingCandidate);
        
        // Check that the resource requirement uses primary spot
        Assert.NotNull(fishingCandidate.Action.ResourceRequirements);
        Assert.Single(fishingCandidate.Action.ResourceRequirements);
        Assert.Equal(PrimaryFishingSpot, fishingCandidate.Action.ResourceRequirements[0].ResourceId);
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
