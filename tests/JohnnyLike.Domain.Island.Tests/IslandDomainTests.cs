using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Tests;

public class IslandDomainPackTests
{
    [Fact]
    public void CreateInitialWorldState_ReturnsIslandWorldState()
    {
        var domain = new IslandDomainPack();
        var world = domain.CreateInitialWorldState();
        
        Assert.IsType<IslandWorldState>(world);
        var islandWorld = (IslandWorldState)world;
        Assert.Equal(0, islandWorld.DayCount);
        Assert.InRange(islandWorld.FishAvailable, 0, 100);
    }

    [Fact]
    public void CreateActorState_WithDefaultData_ReturnsIslandActorState()
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        
        var state = domain.CreateActorState(actorId);
        
        Assert.IsType<IslandActorState>(state);
        var islandState = (IslandActorState)state;
        Assert.Equal(actorId, islandState.Id);
        Assert.Equal(10, islandState.STR);
        Assert.Equal(10, islandState.DEX);
        Assert.Equal(100.0, islandState.Energy);
    }

    [Fact]
    public void CreateActorState_WithCustomAttributes_UsesProvidedValues()
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var initialData = new Dictionary<string, object>
        {
            ["STR"] = 14,
            ["DEX"] = 16,
            ["WIS"] = 12,
            ["hunger"] = 50.0
        };
        
        var state = domain.CreateActorState(actorId, initialData);
        
        var islandState = (IslandActorState)state;
        Assert.Equal(14, islandState.STR);
        Assert.Equal(16, islandState.DEX);
        Assert.Equal(12, islandState.WIS);
        Assert.Equal(50.0, islandState.Hunger);
    }

    [Fact]
    public void GenerateCandidates_GeneratesFishingAction()
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actorState = domain.CreateActorState(actorId, new Dictionary<string, object> { ["hunger"] = 60.0 });
        var worldState = domain.CreateInitialWorldState();
        
        var candidates = domain.GenerateCandidates(actorId, actorState, worldState, 0.0, new Random(42));
        
        Assert.Contains(candidates, c => c.Action.Id.Value == "fish_for_food");
    }

    [Fact]
    public void GenerateCandidates_FishingScoreIncreasesWithHunger()
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        
        var lowHungerState = domain.CreateActorState(actorId, new Dictionary<string, object> { ["hunger"] = 20.0 });
        var highHungerState = domain.CreateActorState(actorId, new Dictionary<string, object> { ["hunger"] = 80.0 });
        var worldState = domain.CreateInitialWorldState();
        
        var lowHungerCandidates = domain.GenerateCandidates(actorId, lowHungerState, worldState, 0.0, new Random(42));
        var highHungerCandidates = domain.GenerateCandidates(actorId, highHungerState, worldState, 0.0, new Random(42));
        
        var lowFishingScore = lowHungerCandidates.First(c => c.Action.Id.Value == "fish_for_food").Score;
        var highFishingScore = highHungerCandidates.First(c => c.Action.Id.Value == "fish_for_food").Score;
        
        Assert.True(highFishingScore > lowFishingScore);
    }

    [Fact]
    public void GenerateCandidates_SleepScoreIncreasesWithLowEnergy()
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        
        var highEnergyState = domain.CreateActorState(actorId, new Dictionary<string, object> { ["energy"] = 90.0 });
        var lowEnergyState = domain.CreateActorState(actorId, new Dictionary<string, object> { ["energy"] = 20.0 });
        var worldState = domain.CreateInitialWorldState();
        
        var highEnergyCandidates = domain.GenerateCandidates(actorId, highEnergyState, worldState, 0.0, new Random(42));
        var lowEnergyCandidates = domain.GenerateCandidates(actorId, lowEnergyState, worldState, 0.0, new Random(42));
        
        var highSleepScore = highEnergyCandidates.First(c => c.Action.Id.Value == "sleep_under_tree").Score;
        var lowSleepScore = lowEnergyCandidates.First(c => c.Action.Id.Value == "sleep_under_tree").Score;
        
        Assert.True(lowSleepScore > highSleepScore);
    }

    [Fact]
    public void ValidateContent_ReturnsTrue()
    {
        var domain = new IslandDomainPack();
        var isValid = domain.ValidateContent(out var errors);
        
        Assert.True(isValid);
        Assert.Empty(errors);
    }
}

public class IslandWorldStateTests
{
    [Fact]
    public void OnTimeAdvanced_AdvancesTimeOfDay()
    {
        var world = new IslandWorldState { TimeOfDay = 0.5 };
        
        world.OnTimeAdvanced(0.0, 21600.0);
        
        Assert.InRange(world.TimeOfDay, 0.74, 0.76);
    }

    [Fact]
    public void OnTimeAdvanced_IncrementsDay()
    {
        var world = new IslandWorldState { TimeOfDay = 0.9, DayCount = 0 };
        
        world.OnTimeAdvanced(0.0, 8640.0);
        
        Assert.Equal(1, world.DayCount);
        Assert.InRange(world.TimeOfDay, 0.0, 0.1);
    }

    [Fact]
    public void OnTimeAdvanced_RegensFish()
    {
        var world = new IslandWorldState { FishAvailable = 50.0, FishRegenRatePerMinute = 5.0 };
        
        world.OnTimeAdvanced(0.0, 60.0);
        
        Assert.Equal(55.0, world.FishAvailable, 1);
    }

    [Fact]
    public void OnTimeAdvanced_RegensCoconutsDaily()
    {
        var world = new IslandWorldState { TimeOfDay = 0.9, CoconutsAvailable = 2 };
        
        world.OnTimeAdvanced(0.0, 10000.0);
        
        Assert.Equal(5, world.CoconutsAvailable);
    }

    [Fact]
    public void OnTimeAdvanced_UpdatesTideLevel()
    {
        var world = new IslandWorldState { TimeOfDay = 0.0 };
        
        world.OnTimeAdvanced(0.0, 0.0);
        
        Assert.True(world.TideLevel == TideLevel.Low || world.TideLevel == TideLevel.High);
    }
}

public class IslandActorStateTests
{
    [Fact]
    public void GetSkillModifier_CalculatesFishingSkill()
    {
        var state = new IslandActorState
        {
            DEX = 14,
            WIS = 16
        };
        
        var modifier = state.GetSkillModifier("Fishing");
        
        Assert.Equal(2 + 3, modifier);
    }

    [Fact]
    public void GetSkillModifier_CalculatesSurvivalSkill()
    {
        var state = new IslandActorState
        {
            STR = 16,
            WIS = 14
        };
        
        var modifier = state.GetSkillModifier("Survival");
        
        Assert.Equal(3 + 2, modifier);
    }

    [Fact]
    public void GetSkillModifier_IncludesBuffs()
    {
        var state = new IslandActorState
        {
            WIS = 14,
            ActiveBuffs = new List<ActiveBuff>
            {
                new ActiveBuff { SkillId = "Perception", Type = BuffType.SkillBonus, Value = 2, ExpiresAt = 100.0 }
            }
        };
        
        var modifier = state.GetSkillModifier("Perception");
        
        Assert.Equal(2 + 2, modifier);
    }

    [Fact]
    public void GetAdvantage_ReturnsAdvantageWhenBuffActive()
    {
        var state = new IslandActorState
        {
            ActiveBuffs = new List<ActiveBuff>
            {
                new ActiveBuff { SkillId = "Fishing", Type = BuffType.Advantage, ExpiresAt = 100.0 }
            }
        };
        
        var advantage = state.GetAdvantage("Fishing");
        
        Assert.Equal(AdvantageType.Advantage, advantage);
    }

    [Fact]
    public void GetAdvantage_ReturnsNormalWhenNoBuffActive()
    {
        var state = new IslandActorState();
        
        var advantage = state.GetAdvantage("Fishing");
        
        Assert.Equal(AdvantageType.Normal, advantage);
    }
}

public class IslandActionEffectsTests
{
    [Fact]
    public void ApplyActionEffects_FishingSuccess_ReducesHunger()
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actorState = new IslandActorState
        {
            Id = actorId,
            Hunger = 60.0,
            CurrentAction = new ActionSpec(
                new ActionId("fish_for_food"),
                ActionKind.Interact,
                new Dictionary<string, object>
                {
                    ["dc"] = 10,
                    ["modifier"] = 3,
                    ["advantage"] = "Normal"
                },
                15.0
            )
        };
        var worldState = new IslandWorldState { FishAvailable = 100.0 };
        
        var outcome = new ActionOutcome(
            new ActionId("fish_for_food"),
            ActionOutcomeType.Success,
            15.0,
            new Dictionary<string, object>
            {
                ["tier"] = "Success"
            }
        );
        
        domain.ApplyActionEffects(actorId, outcome, actorState, worldState);
        
        Assert.True(actorState.Hunger < 60.0);
    }

    [Fact]
    public void ApplyActionEffects_SleepUnderTree_RestoresEnergy()
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actorState = new IslandActorState
        {
            Id = actorId,
            Energy = 30.0,
            CurrentAction = new ActionSpec(
                new ActionId("sleep_under_tree"),
                ActionKind.Interact,
                new Dictionary<string, object>(),
                30.0
            )
        };
        var worldState = new IslandWorldState();
        
        var outcome = new ActionOutcome(
            new ActionId("sleep_under_tree"),
            ActionOutcomeType.Success,
            30.0,
            null
        );
        
        domain.ApplyActionEffects(actorId, outcome, actorState, worldState);
        
        Assert.True(actorState.Energy > 30.0);
    }

    [Fact]
    public void ApplyActionEffects_PassiveStatsDecay()
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actorState = new IslandActorState
        {
            Id = actorId,
            Hunger = 30.0,
            Energy = 80.0,
            Boredom = 20.0
        };
        var worldState = new IslandWorldState();
        
        var outcome = new ActionOutcome(
            new ActionId("idle"),
            ActionOutcomeType.Success,
            10.0,
            null
        );
        
        domain.ApplyActionEffects(actorId, outcome, actorState, worldState);
        
        Assert.True(actorState.Hunger > 30.0);
        Assert.True(actorState.Energy < 80.0);
        Assert.True(actorState.Boredom > 20.0);
    }
}
