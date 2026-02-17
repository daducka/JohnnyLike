using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Tests;

public class TreasureChestAndSharkTests
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
            return minValue; // Default if queue is empty
        }

        public double NextDouble()
        {
            return 0.5;
        }
    }

    [Fact]
    public void SwimCriticalSuccess_SpawnsTreasureChest()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        // Ensure chest is not present initially
        Assert.False(world.TreasureChest.IsPresent);
        
        // Create a swim action
        var swimAction = new ActionSpec(
            new ActionId("swim"),
            ActionKind.Interact,
            new SkillCheckActionParameters(10, 0, AdvantageType.Normal, "water"),
            15.0
        );
        
        // Set current action on actor
        actor.CurrentAction = swimAction;
        
        var resultData = new Dictionary<string, object>();
        var outcome = new ActionOutcome(
            new ActionId("swim"),
            ActionOutcomeType.Success,
            15.0,
            resultData
        );
        
        // Use a fixed RNG to force critical success
        // A roll of 20 is always a critical success
        var rng = new FixedRngStream(20);
        
        domain.ApplyActionEffects(actorId, outcome, actor, world, rng);
        
        // Verify chest is spawned
        Assert.True(world.TreasureChest.IsPresent);
        Assert.False(world.TreasureChest.IsOpened);
        Assert.Equal(100.0, world.TreasureChest.Health);
        Assert.NotNull(world.TreasureChest.Position);
        
        // Verify ResultData annotations
        Assert.NotNull(resultData);
        Assert.Equal("swim_crit_success_treasure", resultData["variant_id"]);
        Assert.Equal("treasure_chest", resultData["encounter_type"]);
    }

    [Fact]
    public void SwimCriticalFailure_SpawnsShark()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        // Ensure shark is not present initially
        Assert.False(world.Shark.IsPresent);
        
        var currentTime = 100.0;
        world.CurrentTime = currentTime;
        
        // Create a swim action
        var swimAction = new ActionSpec(
            new ActionId("swim"),
            ActionKind.Interact,
            new SkillCheckActionParameters(10, 0, AdvantageType.Normal, "water"),
            15.0
        );
        
        // Set current action on actor
        actor.CurrentAction = swimAction;
        
        var resultData = new Dictionary<string, object>();
        var outcome = new ActionOutcome(
            new ActionId("swim"),
            ActionOutcomeType.Success,
            15.0,
            resultData
        );
        
        // Use a fixed RNG to force critical failure
        // A roll of 1 with DC 10 is always a critical failure
        var rng = new FixedRngStream(1);
        
        domain.ApplyActionEffects(actorId, outcome, actor, world, rng);
        
        // Verify shark is spawned
        Assert.True(world.Shark.IsPresent);
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
        
        actor.Energy = 50.0; // Ensure actor has enough energy
        
        // First, verify swim candidates are generated when no shark
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0.0, new Random(42));
        Assert.Contains(candidates, c => c.Action.Id.Value == "swim");
        
        // Spawn a shark
        world.Shark.IsPresent = true;
        world.Shark.ExpiresAt = 100.0;
        
        // Verify no swim candidates are generated while shark is present
        candidates = domain.GenerateCandidates(actorId, actor, world, 0.0, new Random(42));
        Assert.DoesNotContain(candidates, c => c.Action.Id.Value == "swim");
    }

    [Fact]
    public void Shark_AutoDespawns_AfterTimeExpires()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        // Spawn a shark
        world.Shark.IsPresent = true;
        world.Shark.ExpiresAt = 100.0;
        
        // Advance time before expiration
        world.OnTimeAdvanced(50.0, 50.0);
        Assert.True(world.Shark.IsPresent, "Shark should still be present before expiration");
        
        // Advance time past expiration
        world.OnTimeAdvanced(150.0, 100.0);
        Assert.False(world.Shark.IsPresent, "Shark should despawn after expiration");
        
        // Verify swim candidates reappear
        actor.Energy = 50.0;
        var candidates = domain.GenerateCandidates(actorId, actor, world, 150.0, new Random(42));
        Assert.Contains(candidates, c => c.Action.Id.Value == "swim");
    }

    [Fact]
    public void BashChest_CandidateAppearsOnlyWhenChestExists()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        // No chest present - no bash candidate
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0.0, new Random(42));
        Assert.DoesNotContain(candidates, c => c.Action.Id.Value == "bash_open_treasure_chest");
        
        // Spawn chest
        world.TreasureChest.IsPresent = true;
        world.TreasureChest.IsOpened = false;
        world.TreasureChest.Health = 100.0;
        
        // Bash candidate should appear
        candidates = domain.GenerateCandidates(actorId, actor, world, 0.0, new Random(42));
        Assert.Contains(candidates, c => c.Action.Id.Value == "bash_open_treasure_chest");
        
        // Open chest - bash candidate should disappear
        world.TreasureChest.IsOpened = true;
        candidates = domain.GenerateCandidates(actorId, actor, world, 0.0, new Random(42));
        Assert.DoesNotContain(candidates, c => c.Action.Id.Value == "bash_open_treasure_chest");
    }

    [Fact]
    public void BashChest_Failure_WeakensChest()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        // Spawn chest
        world.TreasureChest.IsPresent = true;
        world.TreasureChest.IsOpened = false;
        world.TreasureChest.Health = 100.0;
        
        // Create bash action with high DC to force failure
        var bashAction = new ActionSpec(
            new ActionId("bash_open_treasure_chest"),
            ActionKind.Interact,
            new SkillCheckActionParameters(20, 0, AdvantageType.Normal, "treasure_chest"),
            20.0
        );
        
        // Set current action on actor
        actor.CurrentAction = bashAction;
        
        var resultData = new Dictionary<string, object>();
        var outcome = new ActionOutcome(
            new ActionId("bash_open_treasure_chest"),
            ActionOutcomeType.Success,
            20.0,
            resultData
        );
        
        // Use fixed RNG for regular failure (roll 8, DC 20 = failure)
        var rng = new FixedRngStream(8);
        
        domain.ApplyActionEffects(actorId, outcome, actor, world, rng);
        
        // Chest should still be present but damaged
        Assert.True(world.TreasureChest.IsPresent);
        Assert.False(world.TreasureChest.IsOpened);
        Assert.True(world.TreasureChest.Health < 100.0, "Health should decrease on failure");
        
        // Verify ResultData
        Assert.NotNull(resultData);
        Assert.Equal("bash_chest_failure", resultData["variant_id"]);
        Assert.True(resultData.ContainsKey("chest_health_after"));
    }

    [Fact]
    public void BashChest_Success_OpensAndRemovesChest()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        var initialMorale = actor.Morale;
        
        // Spawn chest
        world.TreasureChest.IsPresent = true;
        world.TreasureChest.IsOpened = false;
        world.TreasureChest.Health = 100.0;
        world.TreasureChest.Position = "shore";
        
        // Create bash action
        var bashAction = new ActionSpec(
            new ActionId("bash_open_treasure_chest"),
            ActionKind.Interact,
            new SkillCheckActionParameters(10, 0, AdvantageType.Normal, "treasure_chest"),
            20.0
        );
        
        // Set current action on actor
        actor.CurrentAction = bashAction;
        
        var resultData = new Dictionary<string, object>();
        var outcome = new ActionOutcome(
            new ActionId("bash_open_treasure_chest"),
            ActionOutcomeType.Success,
            20.0,
            resultData
        );
        
        // Use fixed RNG for success (roll 15, DC 10 = success)
        var rng = new FixedRngStream(15);
        
        domain.ApplyActionEffects(actorId, outcome, actor, world, rng);
        
        // Chest should be removed
        Assert.False(world.TreasureChest.IsPresent);
        Assert.True(world.TreasureChest.IsOpened);
        Assert.Equal(0.0, world.TreasureChest.Health);
        Assert.Null(world.TreasureChest.Position);
        
        // Actor should get morale reward
        Assert.True(actor.Morale > initialMorale, "Morale should increase on success");
        
        // Verify ResultData
        Assert.NotNull(resultData);
        Assert.Equal("bash_chest_success", resultData["variant_id"]);
        Assert.True((bool)resultData["loot_placeholder"]!);
    }

    [Fact]
    public void BashChest_MultipleFailures_ProgressivelyWeakenChest()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        // Spawn chest
        world.TreasureChest.IsPresent = true;
        world.TreasureChest.IsOpened = false;
        world.TreasureChest.Health = 100.0;
        
        var initialHealth = world.TreasureChest.Health;
        
        // First failed bash
        var bashAction = new ActionSpec(
            new ActionId("bash_open_treasure_chest"),
            ActionKind.Interact,
            new SkillCheckActionParameters(20, 0, AdvantageType.Normal, "treasure_chest"),
            20.0
        );
        
        // Set current action on actor
        actor.CurrentAction = bashAction;
        
        var resultData1 = new Dictionary<string, object>();
        var outcome1 = new ActionOutcome(
            new ActionId("bash_open_treasure_chest"),
            ActionOutcomeType.Success,
            20.0,
            resultData1
        );
        var rng1 = new FixedRngStream(8); // Regular failure
        
        domain.ApplyActionEffects(actorId, outcome1, actor, world, rng1);
        
        var healthAfterFirst = world.TreasureChest.Health;
        Assert.True(healthAfterFirst < initialHealth);
        Assert.True(world.TreasureChest.IsPresent);
        
        // Second failed bash on weakened chest
        // Reset current action for second bash
        actor.CurrentAction = bashAction;
        
        var resultData2 = new Dictionary<string, object>();
        var outcome2 = new ActionOutcome(
            new ActionId("bash_open_treasure_chest"),
            ActionOutcomeType.Success,
            20.0,
            resultData2
        );
        var rng2 = new FixedRngStream(8); // Regular failure
        
        domain.ApplyActionEffects(actorId, outcome2, actor, world, rng2);
        
        var healthAfterSecond = world.TreasureChest.Health;
        Assert.True(healthAfterSecond < healthAfterFirst);
        Assert.True(world.TreasureChest.IsPresent);
    }

    [Fact]
    public void BashChest_DC_DecreasesWithHealth()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        // High health chest
        world.TreasureChest.IsPresent = true;
        world.TreasureChest.IsOpened = false;
        world.TreasureChest.Health = 100.0;
        
        var candidatesHigh = domain.GenerateCandidates(actorId, actor, world, 0.0, new Random(42));
        var bashCandidateHigh = candidatesHigh.First(c => c.Action.Id.Value == "bash_open_treasure_chest");
        var paramsHigh = (SkillCheckActionParameters)bashCandidateHigh.Action.Parameters;
        
        // Low health chest
        world.TreasureChest.Health = 10.0;
        
        var candidatesLow = domain.GenerateCandidates(actorId, actor, world, 0.0, new Random(42));
        var bashCandidateLow = candidatesLow.First(c => c.Action.Id.Value == "bash_open_treasure_chest");
        var paramsLow = (SkillCheckActionParameters)bashCandidateLow.Action.Parameters;
        
        // DC should be lower for damaged chest
        Assert.True(paramsLow.DC < paramsHigh.DC, 
            $"Damaged chest DC ({paramsLow.DC}) should be lower than full health DC ({paramsHigh.DC})");
    }

    [Fact]
    public void WorldState_Serialization_PreservesChestAndShark()
    {
        var world = new IslandWorldState();
        
        // Set up treasure chest
        world.TreasureChest.IsPresent = true;
        world.TreasureChest.IsOpened = false;
        world.TreasureChest.Health = 75.5;
        world.TreasureChest.Position = "beach";
        
        // Set up shark
        world.Shark.IsPresent = true;
        world.Shark.ExpiresAt = 123.456;
        
        // Serialize
        var serialized = world.Serialize();
        
        // Deserialize into new instance
        var newWorld = new IslandWorldState();
        newWorld.Deserialize(serialized);
        
        // Verify treasure chest
        Assert.Equal(world.TreasureChest.IsPresent, newWorld.TreasureChest.IsPresent);
        Assert.Equal(world.TreasureChest.IsOpened, newWorld.TreasureChest.IsOpened);
        Assert.Equal(world.TreasureChest.Health, newWorld.TreasureChest.Health);
        Assert.Equal(world.TreasureChest.Position, newWorld.TreasureChest.Position);
        
        // Verify shark
        Assert.Equal(world.Shark.IsPresent, newWorld.Shark.IsPresent);
        Assert.Equal(world.Shark.ExpiresAt, newWorld.Shark.ExpiresAt);
    }
}
