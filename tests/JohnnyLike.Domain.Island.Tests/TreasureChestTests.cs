using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Tests;

public class TreasureChestTests
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

    [Fact]
    public void SwimCriticalSuccess_SpawnsTreasureChest()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        // Ensure chest is not present initially
        Assert.Null(world.TreasureChest);
        
        // Create a swim action
        var swimAction = new ActionSpec(
            new ActionId("swim"),
            ActionKind.Interact,
            new SkillCheckActionParameters(10, 0, AdvantageType.Normal, "water", "Survival"),
            15.0
        );
        
        // Set current action on actor
        actor.CurrentAction = swimAction;
        
        var resultData = new Dictionary<string, object>
        {
            ["tier"] = "CriticalSuccess"
        };
        var outcome = new ActionOutcome(
            new ActionId("swim"),
            ActionOutcomeType.Success,
            15.0,
            resultData
        );
        
        // No longer need RNG since tier is pre-populated
        var rng = new FixedRngStream(20);
        
        domain.ApplyActionEffects(actorId, outcome, actor, world, rng);
        
        // Verify chest is spawned
        Assert.NotNull(world.TreasureChest);
        Assert.False(world.TreasureChest.IsOpened);
        Assert.Equal(100.0, world.TreasureChest.Health);
        Assert.NotNull(world.TreasureChest.Position);
        
        // Verify ResultData annotations
        Assert.NotNull(resultData);
        Assert.Equal("swim_crit_success_treasure", resultData["variant_id"]);
        Assert.Equal("treasure_chest", resultData["encounter_type"]);
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
        var chest = new TreasureChestItem();
        chest.IsOpened = false;
        chest.Health = 100.0;
        world.WorldItems.Add(chest);
        
        // Bash candidate should appear
        candidates = domain.GenerateCandidates(actorId, actor, world, 0.0, new Random(42));
        Assert.Contains(candidates, c => c.Action.Id.Value == "bash_open_treasure_chest");
        
        // Open chest - bash candidate should disappear
        world.TreasureChest!.IsOpened = true;
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
        var chest = new TreasureChestItem();
        chest.IsOpened = false;
        chest.Health = 100.0;
        world.WorldItems.Add(chest);
        
        // Create bash action with high DC to force failure
        var bashAction = new ActionSpec(
            new ActionId("bash_open_treasure_chest"),
            ActionKind.Interact,
            new SkillCheckActionParameters(20, 0, AdvantageType.Normal, "treasure_chest", "Athletics"),
            20.0
        );
        
        // Set current action on actor
        actor.CurrentAction = bashAction;
        
        var resultData = new Dictionary<string, object>
        {
            ["tier"] = "Failure"
        };
        var outcome = new ActionOutcome(
            new ActionId("bash_open_treasure_chest"),
            ActionOutcomeType.Success,
            20.0,
            resultData
        );
        
        // No longer need RNG since tier is pre-populated
        var rng = new FixedRngStream(8);
        
        domain.ApplyActionEffects(actorId, outcome, actor, world, rng);
        
        // Chest should still be present but damaged
        Assert.NotNull(world.TreasureChest);
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
        var chest = new TreasureChestItem();
        chest.IsOpened = false;
        chest.Health = 100.0;
        chest.Position = "shore";
        world.WorldItems.Add(chest);
        
        // Create bash action
        var bashAction = new ActionSpec(
            new ActionId("bash_open_treasure_chest"),
            ActionKind.Interact,
            new SkillCheckActionParameters(10, 0, AdvantageType.Normal, "treasure_chest", "Athletics"),
            20.0
        );
        
        // Set current action on actor
        actor.CurrentAction = bashAction;
        
        var resultData = new Dictionary<string, object>
        {
            ["tier"] = "Success"
        };
        var outcome = new ActionOutcome(
            new ActionId("bash_open_treasure_chest"),
            ActionOutcomeType.Success,
            20.0,
            resultData
        );
        
        // No longer need RNG since tier is pre-populated
        var rng = new FixedRngStream(15);
        
        domain.ApplyActionEffects(actorId, outcome, actor, world, rng);
        
        // Chest should be removed
        Assert.Null(world.TreasureChest);
        
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
        var chest = new TreasureChestItem();
        chest.IsOpened = false;
        chest.Health = 100.0;
        world.WorldItems.Add(chest);
        
        var initialHealth = world.TreasureChest!.Health;
        
        // First failed bash
        var bashAction = new ActionSpec(
            new ActionId("bash_open_treasure_chest"),
            ActionKind.Interact,
            new SkillCheckActionParameters(20, 0, AdvantageType.Normal, "treasure_chest", "Athletics"),
            20.0
        );
        
        // Set current action on actor
        actor.CurrentAction = bashAction;
        
        var resultData1 = new Dictionary<string, object>
        {
            ["tier"] = "Failure"
        };
        var outcome1 = new ActionOutcome(
            new ActionId("bash_open_treasure_chest"),
            ActionOutcomeType.Success,
            20.0,
            resultData1
        );
        var rng1 = new FixedRngStream(8); // No longer used
        
        domain.ApplyActionEffects(actorId, outcome1, actor, world, rng1);
        
        var healthAfterFirst = world.TreasureChest!.Health;
        Assert.True(healthAfterFirst < initialHealth);
        Assert.NotNull(world.TreasureChest);
        
        // Second failed bash on weakened chest
        // Reset current action for second bash
        actor.CurrentAction = bashAction;
        
        var resultData2 = new Dictionary<string, object>
        {
            ["tier"] = "Failure"
        };
        var outcome2 = new ActionOutcome(
            new ActionId("bash_open_treasure_chest"),
            ActionOutcomeType.Success,
            20.0,
            resultData2
        );
        var rng2 = new FixedRngStream(8); // No longer used
        
        domain.ApplyActionEffects(actorId, outcome2, actor, world, rng2);
        
        var healthAfterSecond = world.TreasureChest!.Health;
        Assert.True(healthAfterSecond < healthAfterFirst);
        Assert.NotNull(world.TreasureChest);
    }

    [Fact]
    public void BashChest_DC_DecreasesWithHealth()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        // High health chest
        var chest = new TreasureChestItem();
        chest.IsOpened = false;
        chest.Health = 100.0;
        world.WorldItems.Add(chest);
        
        var candidatesHigh = domain.GenerateCandidates(actorId, actor, world, 0.0, new Random(42));
        var bashCandidateHigh = candidatesHigh.First(c => c.Action.Id.Value == "bash_open_treasure_chest");
        var paramsHigh = (SkillCheckActionParameters)bashCandidateHigh.Action.Parameters;
        
        // Low health chest
        world.TreasureChest!.Health = 10.0;
        
        var candidatesLow = domain.GenerateCandidates(actorId, actor, world, 0.0, new Random(42));
        var bashCandidateLow = candidatesLow.First(c => c.Action.Id.Value == "bash_open_treasure_chest");
        var paramsLow = (SkillCheckActionParameters)bashCandidateLow.Action.Parameters;
        
        // DC should be lower for damaged chest
        Assert.True(paramsLow.DC < paramsHigh.DC, 
            $"Damaged chest DC ({paramsLow.DC}) should be lower than full health DC ({paramsHigh.DC})");
    }

    [Fact]
    public void WorldState_Serialization_PreservesTreasureChest()
    {
        var world = new IslandWorldState();
        
        // Set up treasure chest
        var chest = new TreasureChestItem();
        chest.IsOpened = false;
        chest.Health = 75.5;
        chest.Position = "beach";
        world.WorldItems.Add(chest);
        
        // Serialize
        var serialized = world.Serialize();
        
        // Deserialize into new instance
        var newWorld = new IslandWorldState();
        newWorld.Deserialize(serialized);
        
        // Verify treasure chest exists and properties are preserved
        Assert.NotNull(newWorld.TreasureChest);
        Assert.Equal(world.TreasureChest!.IsOpened, newWorld.TreasureChest.IsOpened);
        Assert.Equal(world.TreasureChest.Health, newWorld.TreasureChest.Health);
        Assert.Equal(world.TreasureChest.Position, newWorld.TreasureChest.Position);
    }
}
