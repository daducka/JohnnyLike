using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Tests;

public class SharkTests
{
    private class FixedRngStream : IRngStream
    {
        private readonly Queue<int> _rolls;

        public FixedRngStream(params int[] rolls)
        {
            _rolls = new Queue<int>(rolls);
        }

        public int Next(int minValue, int maxValue)
        {
            if (_rolls.Count > 0)
                return _rolls.Dequeue();
            // Throw exception to make exhausted RNG more obvious during testing
            throw new InvalidOperationException("FixedRngStream exhausted - all preset rolls have been consumed");
        }

        public double NextDouble()
        {
            return 0.5;
        }
    }

    // Simple test-only implementation of IResourceReservationService
    private class TestResourceReservationService : IResourceReservationService, IResourceAvailability
    {
        private readonly HashSet<ResourceId> _reservedResources = new();

        public bool TryReserve(ResourceId resourceId, ReservationOwner owner, double until)
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

        public bool IsReserved(ResourceId resourceId)
        {
            return _reservedResources.Contains(resourceId);
        }
    }

    [Fact]
    public void SwimCriticalFailure_SpawnsShark()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        // Set up reservation service for tests
        world.ReservationService = new TestResourceReservationService();
        
        // Ensure shark is not present initially
        Assert.Null(world.Shark);
        
        var currentTime = 100.0;
        world.CurrentTime = currentTime;
        
        // Create a swim action
        var swimAction = new ActionSpec(
            new ActionId("swim"),
            ActionKind.Interact,
            new SkillCheckActionParameters(
                    new SkillCheckRequest(10, 0, AdvantageType.Normal, "Survival"),
                    new SkillCheckResult(10, 10 + 0, RollOutcomeTier.Success, true, 0.5)),
            15.0
        );
        
        // Set current action on actor
        actor.CurrentAction = swimAction;
        
        var resultData = new Dictionary<string, object>
        {
            ["tier"] = "CriticalFailure"
        };
        var outcome = new ActionOutcome(
            new ActionId("swim"),
            ActionOutcomeType.Success,
            15.0,
            resultData
        );
        
        // No longer need RNG since tier is pre-populated
        var rng = new FixedRngStream(1);
        
        domain.ApplyActionEffects(actorId, outcome, actor, world, rng);
        
        // Verify shark is spawned
        Assert.NotNull(world.Shark);
        Assert.True(world.Shark.ExpiresAt > currentTime);
        Assert.True(world.Shark.ExpiresAt <= currentTime + 180.0); // max duration
        
        // Verify ResultData annotations
        Assert.NotNull(resultData);
        Assert.Equal("swim_crit_failure_shark", resultData["variant_id"]);
        Assert.Equal("shark", resultData["encounter_type"]);
        Assert.True(resultData.ContainsKey("shark_duration"));
    }

    [Fact]
    public void Shark_BlocksSwimming_WhilePresent()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        // Set up reservation service
        var reservationService = new TestResourceReservationService();
        world.ReservationService = reservationService;
        
        actor.Energy = 50.0; // Ensure actor has enough energy
        
        // First, verify swim candidates are generated when no shark (and resource is available)
        var emptyAvailability = new EmptyResourceAvailability();
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0.0, new Random(42), emptyAvailability);
        Assert.Contains(candidates, c => c.Action.Id.Value == "swim");
        
        // Spawn a shark and reserve the water resource
        var shark = new SharkItem();
        shark.ExpiresAt = 100.0;
        world.WorldItems.Add(shark);
        
        // Manually reserve the water resource to simulate what happens in ApplyEffects
        var waterResource = new ResourceId("island:resource:water");
        var sharkOwner = ReservationOwner.FromWorldItem(shark.Id);
        reservationService.TryReserve(waterResource, sharkOwner, shark.ExpiresAt);
        shark.ReservedResourceId = waterResource;
        
        // Verify swim candidates are still generated (domain doesn't block them)
        // But the resource availability check will indicate water is reserved
        candidates = domain.GenerateCandidates(actorId, actor, world, 0.0, new Random(42), reservationService);
        Assert.Contains(candidates, c => c.Action.Id.Value == "swim");
        
        // Verify that the water resource is indeed reserved
        Assert.True(reservationService.IsReserved(waterResource));
    }

    [Fact]
    public void Shark_AutoDespawns_AfterTimeExpires()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        // Set up reservation service for tests
        world.ReservationService = new TestResourceReservationService();
        
        // Spawn a shark
        var shark = new SharkItem();
        shark.ExpiresAt = 100.0;
        world.WorldItems.Add(shark);
        
        // Advance time before expiration
        world.OnTimeAdvanced(50.0, 50.0);
        Assert.NotNull(world.Shark); // Shark should still be present before expiration
        
        // Advance time past expiration
        world.OnTimeAdvanced(150.0, 100.0);
        Assert.Null(world.Shark); // Shark should despawn after expiration
        
        // Verify swim candidates reappear
        actor.Energy = 50.0;
        var candidates = domain.GenerateCandidates(actorId, actor, world, 150.0, new Random(42), new EmptyResourceAvailability());
        Assert.Contains(candidates, c => c.Action.Id.Value == "swim");
    }

    [Fact]
    public void Shark_ReservesAndReleasesWaterResource()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        // Set up reservation service
        var reservationService = new TestResourceReservationService();
        world.ReservationService = reservationService;
        
        var currentTime = 100.0;
        world.CurrentTime = currentTime;
        
        var waterResource = new ResourceId("island:resource:water");
        
        // Verify water resource is not reserved initially
        Assert.False(reservationService.IsReserved(waterResource));
        
        // Simulate swim critical failure that spawns a shark
        var resultData = new Dictionary<string, object>
        {
            ["tier"] = "CriticalFailure"
        };
        var outcome = new ActionOutcome(
            new ActionId("swim"),
            ActionOutcomeType.Success,
            15.0,
            resultData
        );
        
        var rng = new FixedRngStream(1);
        domain.ApplyActionEffects(actorId, outcome, actor, world, rng);
        
        // Verify shark was spawned and reserved the water resource
        Assert.NotNull(world.Shark);
        Assert.True(reservationService.IsReserved(waterResource));
        Assert.Equal(waterResource, world.Shark.ReservedResourceId);
        
        // Advance time past shark expiration
        world.OnTimeAdvanced(world.Shark.ExpiresAt + 1.0, world.Shark.ExpiresAt + 1.0 - currentTime);
        
        // Verify shark despawned and released the water resource
        Assert.Null(world.Shark);
        Assert.False(reservationService.IsReserved(waterResource));
    }

    [Fact]
    public void WorldState_Serialization_PreservesShark()
    {
        var world = new IslandWorldState();
        
        // Set up shark
        var shark = new SharkItem();
        shark.ExpiresAt = 123.456;
        world.WorldItems.Add(shark);
        
        // Serialize
        var serialized = world.Serialize();
        
        // Deserialize into new instance
        var newWorld = new IslandWorldState();
        newWorld.Deserialize(serialized);
        
        // Verify shark exists and properties are preserved
        Assert.NotNull(newWorld.Shark);
        Assert.Equal(world.Shark!.ExpiresAt, newWorld.Shark.ExpiresAt);
    }
}
