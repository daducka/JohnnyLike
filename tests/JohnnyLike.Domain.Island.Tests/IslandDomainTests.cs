using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Kit.Dice;
using JohnnyLike.Engine;
using JohnnyLike.SimRunner;

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

    [Fact]
    public void ProviderDiscovery_FindsAllAttributedProviders()
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actorState = domain.CreateActorState(actorId);
        var worldState = domain.CreateInitialWorldState();
        
        var candidates = domain.GenerateCandidates(actorId, actorState, worldState, 0.0, new Random(42));
        
        // Should have candidates from at least these providers:
        // - ChatCandidateProvider (may or may not add depending on pending chat actions)
        // - SleepCandidateProvider
        // - FishingCandidateProvider
        // - CoconutCandidateProvider
        // - SandCastleCandidateProvider
        // - SwimCandidateProvider
        // - IdleCandidateProvider (always present)
        
        // Verify we have multiple candidate types present
        Assert.Contains(candidates, c => c.Action.Id.Value == "sleep_under_tree");
        Assert.Contains(candidates, c => c.Action.Id.Value == "fish_for_food");
        Assert.Contains(candidates, c => c.Action.Id.Value == "idle");
    }

    [Fact]
    public void IdleCandidate_AlwaysPresentEvenWhenOtherCandidatesExist()
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actorState = domain.CreateActorState(actorId, new Dictionary<string, object> { ["hunger"] = 60.0 });
        var worldState = domain.CreateInitialWorldState();
        
        var candidates = domain.GenerateCandidates(actorId, actorState, worldState, 0.0, new Random(42));
        
        // Idle must always be present
        Assert.Contains(candidates, c => c.Action.Id.Value == "idle");
        
        // Other candidates should also be present
        Assert.True(candidates.Count > 1, "Should have more than just idle candidate");
        Assert.Contains(candidates, c => c.Action.Id.Value == "fish_for_food");
    }

    [Fact]
    public void ChatCandidateProvider_ProcessesPendingChatActions()
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actorState = domain.CreateActorState(actorId) as IslandActorState;
        var worldState = domain.CreateInitialWorldState();
        
        // Add a pending chat action
        actorState!.PendingChatActions.Enqueue(new PendingIntent
        {
            ActionId = "clap_emote",
            Type = "sub",
            Data = new Dictionary<string, object>(),
            EnqueuedAt = 0.0
        });
        
        var candidates = domain.GenerateCandidates(actorId, actorState, worldState, 0.0, new Random(42));
        
        // Should have the clap emote candidate
        Assert.Contains(candidates, c => c.Action.Id.Value == "clap_emote");
    }

    [Fact]
    public void ChatCandidateProvider_SkipsChatWhenSurvivalCritical()
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actorState = domain.CreateActorState(actorId, new Dictionary<string, object> 
        { 
            ["hunger"] = 85.0,  // Survival critical
            ["energy"] = 50.0 
        }) as IslandActorState;
        var worldState = domain.CreateInitialWorldState();
        
        // Add a pending chat action
        actorState!.PendingChatActions.Enqueue(new PendingIntent
        {
            ActionId = "clap_emote",
            Type = "sub",
            Data = new Dictionary<string, object>(),
            EnqueuedAt = 0.0
        });
        
        var candidates = domain.GenerateCandidates(actorId, actorState, worldState, 0.0, new Random(42));
        
        // Should NOT have the clap emote candidate because survival is critical
        Assert.DoesNotContain(candidates, c => c.Action.Id.Value == "clap_emote");
        
        // But should have survival actions like fishing
        Assert.Contains(candidates, c => c.Action.Id.Value == "fish_for_food");
    }

    [Fact]
    public void Providers_DiscoveredInCorrectOrder()
    {
        var domain = new IslandDomainPack();
        
        // Get the private _providers field via reflection to inspect order
        var providersField = typeof(IslandDomainPack).GetField("_providers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var providers = (List<IIslandCandidateProvider>)providersField!.GetValue(domain)!;
        
        // Verify we have all expected providers
        Assert.Equal(9, providers.Count); // Chat, Sleep, Fishing, Coconut, SandCastle, Swim, PlaneSighting, MermaidEncounter, Idle
        
        // Verify order by checking types
        var providerTypes = providers.Select(p => p.GetType().Name).ToList();
        
        // Expected order based on Order attribute values:
        // ChatCandidateProvider (50)
        // SleepCandidateProvider (100)
        // FishingCandidateProvider (200)
        // CoconutCandidateProvider (210)
        // SandCastleCandidateProvider (400)
        // SwimCandidateProvider (410)
        // PlaneSightingCandidateProvider (800)
        // MermaidEncounterCandidateProvider (810)
        // IdleCandidateProvider (9999)
        
        Assert.Equal("ChatCandidateProvider", providerTypes[0]);
        Assert.Equal("SleepCandidateProvider", providerTypes[1]);
        Assert.Equal("FishingCandidateProvider", providerTypes[2]);
        Assert.Equal("CoconutCandidateProvider", providerTypes[3]);
        Assert.Equal("SandCastleCandidateProvider", providerTypes[4]);
        Assert.Equal("SwimCandidateProvider", providerTypes[5]);
        Assert.Equal("PlaneSightingCandidateProvider", providerTypes[6]);
        Assert.Equal("MermaidEncounterCandidateProvider", providerTypes[7]);
        Assert.Equal("IdleCandidateProvider", providerTypes[8]);
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
        
        var rng = new RandomRngStream(new Random(42));
        domain.ApplyActionEffects(actorId, outcome, actorState, worldState, rng);
        
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
        
        var rng = new RandomRngStream(new Random(42));
        domain.ApplyActionEffects(actorId, outcome, actorState, worldState, rng);
        
        Assert.True(actorState.Energy > 30.0);
    }

    [Fact]
    public void ApplyActionEffects_SkillCheck_StoresDetailedResultData()
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

        // Outcome with empty ResultData and no tier pre-set — forces skill check resolution
        var resultData = new Dictionary<string, object>();
        var outcome = new ActionOutcome(
            new ActionId("fish_for_food"),
            ActionOutcomeType.Success,
            15.0,
            resultData
        );

        var rng = new RandomRngStream(new Random(42));
        domain.ApplyActionEffects(actorId, outcome, actorState, worldState, rng);

        Assert.True(resultData.ContainsKey("dc"));
        Assert.True(resultData.ContainsKey("modifier"));
        Assert.True(resultData.ContainsKey("advantage"));
        Assert.True(resultData.ContainsKey("roll"));
        Assert.True(resultData.ContainsKey("total"));
        Assert.True(resultData.ContainsKey("tier"));

        Assert.Equal(10, resultData["dc"]);
        Assert.Equal(3, resultData["modifier"]);
        Assert.Equal("Normal", resultData["advantage"]);
        Assert.IsType<int>(resultData["roll"]);
        Assert.IsType<int>(resultData["total"]);
        Assert.IsType<string>(resultData["tier"]);
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
        
        var rng = new RandomRngStream(new Random(42));
        domain.ApplyActionEffects(actorId, outcome, actorState, worldState, rng);
        
        Assert.True(actorState.Hunger > 30.0);
        Assert.True(actorState.Energy < 80.0);
        Assert.True(actorState.Boredom > 20.0);
    }
}

public class IslandSignalHandlingTests
{
    [Fact]
    public void OnSignal_ChatRedeemWriteNameSand_EnqueuesIntent()
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actorState = new IslandActorState { Id = actorId };
        var worldState = new IslandWorldState();
        
        var signal = new Signal(
            "chat_redeem",
            0.0,
            actorId,
            new Dictionary<string, object>
            {
                ["redeem_name"] = "write_name_sand",
                ["viewer_name"] = "TestViewer"
            }
        );
        
        domain.OnSignal(signal, actorState, worldState, 0.0);
        
        Assert.Single(actorState.PendingChatActions);
        var intent = actorState.PendingChatActions.Peek();
        Assert.Equal("write_name_sand", intent.ActionId);
        Assert.Equal("chat_redeem", intent.Type);
    }

    [Fact]
    public void OnSignal_SubOrCheer_AddsInspirationBuffAndEnqueuesClap()
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actorState = new IslandActorState { Id = actorId };
        var worldState = new IslandWorldState();
        
        var signal = new Signal(
            "sub",
            0.0,
            actorId,
            new Dictionary<string, object> { ["subscriber"] = "TestSub" }
        );
        
        domain.OnSignal(signal, actorState, worldState, 10.0);
        
        // Check Inspiration buff was added
        Assert.Contains(actorState.ActiveBuffs, b => b.Name == "Inspiration");
        var inspirationBuff = actorState.ActiveBuffs.First(b => b.Name == "Inspiration");
        Assert.Equal(BuffType.SkillBonus, inspirationBuff.Type);
        Assert.Equal(1, inspirationBuff.Value);
        Assert.Equal(310.0, inspirationBuff.ExpiresAt);
        
        // Check clap emote intent was enqueued
        Assert.Single(actorState.PendingChatActions);
        var intent = actorState.PendingChatActions.Peek();
        Assert.Equal("clap_emote", intent.ActionId);
        Assert.Equal("sub", intent.Type);
    }

    [Fact]
    public void GenerateCandidates_WithPendingChatAction_ProducesHighPriorityCandidate()
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actorState = new IslandActorState
        {
            Id = actorId,
            Hunger = 30.0,  // Not critical
            Energy = 60.0   // Not critical
        };
        actorState.PendingChatActions.Enqueue(new PendingIntent
        {
            ActionId = "write_name_sand",
            Type = "chat_redeem",
            Data = new Dictionary<string, object> { ["viewer_name"] = "TestViewer" },
            EnqueuedAt = 0.0
        });
        var worldState = new IslandWorldState();
        
        var candidates = domain.GenerateCandidates(actorId, actorState, worldState, 10.0, new Random(42));
        
        // Should have a write_name_sand candidate with high priority
        var writeSandCandidate = candidates.FirstOrDefault(c => c.Action.Id.Value == "write_name_sand");
        Assert.NotNull(writeSandCandidate);
        Assert.Equal(2.0, writeSandCandidate.Score);
        Assert.Contains("TestViewer", writeSandCandidate.Reason);
    }

    [Fact]
    public void GenerateCandidates_SurvivalCritical_SkipsPendingChatActions()
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actorState = new IslandActorState
        {
            Id = actorId,
            Hunger = 85.0,  // Critical hunger
            Energy = 60.0
        };
        actorState.PendingChatActions.Enqueue(new PendingIntent
        {
            ActionId = "clap_emote",
            Type = "sub",
            Data = new Dictionary<string, object>(),
            EnqueuedAt = 0.0
        });
        var worldState = new IslandWorldState();
        
        var candidates = domain.GenerateCandidates(actorId, actorState, worldState, 10.0, new Random(42));
        
        // Should NOT have clap emote when survival is critical
        var clapCandidate = candidates.FirstOrDefault(c => c.Action.Id.Value == "clap_emote");
        Assert.Null(clapCandidate);
        
        // Should have survival actions (fishing should be very high priority)
        var fishingCandidate = candidates.FirstOrDefault(c => c.Action.Id.Value == "fish_for_food");
        Assert.NotNull(fishingCandidate);
    }

    [Fact]
    public void IslandOnSignal_SchedulesActionWithinBoundedTime()
    {
        // This test proves that Island OnSignal results in a corresponding action 
        // being scheduled within a bounded time (unless survival critical)
        // 
        // Bounded time is set to 30 seconds as a reasonable upper limit for non-critical
        // actions to be scheduled. In practice, actions are typically scheduled much faster
        // (within a few seconds) at decision boundaries when the actor becomes ready.
        var domainPack = new IslandDomainPack();
        var traceSink = new InMemoryTraceSink();
        var engine = new JohnnyLike.Engine.Engine(domainPack, 42, traceSink);
        
        engine.AddActor(new ActorId("TestActor"), new Dictionary<string, object>
        {
            ["hunger"] = 30.0,
            ["energy"] = 70.0
        });
        
        // Enqueue a chat redeem signal
        var signal = new Signal(
            "chat_redeem",
            0.0,
            new ActorId("TestActor"),
            new Dictionary<string, object>
            {
                ["redeem_name"] = "write_name_sand",
                ["viewer_name"] = "TestViewer"
            }
        );
        engine.EnqueueSignal(signal);
        
        // Process signal
        engine.AdvanceTime(0.1);
        
        // Create executor and run simulation
        var executor = new FakeExecutor(engine);
        var timeStep = 0.5;
        var elapsed = 0.0;
        var maxWaitTime = 30.0; // Bounded time limit - should be much faster in practice
        var actionFound = false;
        
        while (elapsed < maxWaitTime && !actionFound)
        {
            executor.Update(timeStep);
            elapsed += timeStep;
            
            // Check trace for the write_name_sand action
            var trace = traceSink.GetEvents();
            if (trace.Any(e => e.EventType == "ActionAssigned" && 
                              e.Details.ContainsKey("actionId") && 
                              e.Details["actionId"].ToString() == "write_name_sand"))
            {
                actionFound = true;
            }
        }
        
        Assert.True(actionFound, $"write_name_sand action should be scheduled within {maxWaitTime} seconds");
        Assert.True(elapsed <= maxWaitTime, "Action should be scheduled within bounded time");
    }

    [Fact]
    public void ApplyActionEffects_CompletingChatAction_DequeuesIntent()
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actorState = new IslandActorState { Id = actorId };
        actorState.PendingChatActions.Enqueue(new PendingIntent
        {
            ActionId = "write_name_sand",
            Type = "chat_redeem",
            Data = new Dictionary<string, object>(),
            EnqueuedAt = 0.0
        });
        var worldState = new IslandWorldState();
        
        var outcome = new ActionOutcome(
            new ActionId("write_name_sand"),
            ActionOutcomeType.Success,
            8.0,
            null
        );
        
        var rng = new RandomRngStream(new Random(42));
        domain.ApplyActionEffects(actorId, outcome, actorState, worldState, rng);
        
        // Intent should be dequeued after completion
        Assert.Empty(actorState.PendingChatActions);
        // Morale should increase
        Assert.True(actorState.Morale > 50.0);
    }
}

public class IslandDeterminismTests
{
    [Fact]
    public void SameSeed_ProducesSameTrace_WithSkillChecks()
    {
        // Run the same simulation twice with the same seed
        var hash1 = RunIslandSimulation(42);
        var hash2 = RunIslandSimulation(42);

        // Verify traces are identical, proving skill checks are deterministic
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void DifferentSeed_ProducesDifferentTrace_WithSkillChecks()
    {
        // Run simulations with different seeds
        var hash1 = RunIslandSimulation(42);
        var hash2 = RunIslandSimulation(43);

        // Verify traces differ, proving RNG affects outcomes
        Assert.NotEqual(hash1, hash2);
    }

    private string RunIslandSimulation(int seed)
    {
        var domainPack = new IslandDomainPack();
        var traceSink = new InMemoryTraceSink();
        var engine = new JohnnyLike.Engine.Engine(domainPack, seed, traceSink);

        // Add an actor with specific attributes that will trigger skill checks
        engine.AddActor(new ActorId("Survivor"), new Dictionary<string, object>
        {
            ["STR"] = 12,
            ["DEX"] = 14,
            ["CON"] = 10,
            ["INT"] = 10,
            ["WIS"] = 16,
            ["CHA"] = 8,
            ["hunger"] = 60.0,
            ["energy"] = 70.0,
            ["morale"] = 50.0,
            ["boredom"] = 30.0
        });

        var executor = new FakeExecutor(engine);
        var timeStep = 0.5;
        var elapsed = 0.0;

        // Run simulation for a short period to ensure skill checks are executed
        while (elapsed < 60.0)
        {
            executor.Update(timeStep);
            elapsed += timeStep;
        }

        return TraceHelper.ComputeTraceHash(traceSink.GetEvents());
    }
}

public class IslandDCTuningTests
{
    [Fact]
    public void FishingDC_MorningIsLowerThanAfternoon()
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actorState = domain.CreateActorState(actorId, new Dictionary<string, object> { ["hunger"] = 60.0 });
        
        // Morning scenario (timeOfDay = 0.1)
        var morningWorld = new IslandWorldState
        {
            TimeOfDay = 0.1,
            Weather = Weather.Clear,
            FishAvailable = 100.0
        };
        
        // Afternoon scenario (timeOfDay = 0.5)
        var afternoonWorld = new IslandWorldState
        {
            TimeOfDay = 0.5,
            Weather = Weather.Clear,
            FishAvailable = 100.0
        };
        
        var morningCandidates = domain.GenerateCandidates(actorId, actorState, morningWorld, 0.0, new Random(42));
        var afternoonCandidates = domain.GenerateCandidates(actorId, actorState, afternoonWorld, 0.0, new Random(42));
        
        var morningFishing = morningCandidates.First(c => c.Action.Id.Value == "fish_for_food");
        var afternoonFishing = afternoonCandidates.First(c => c.Action.Id.Value == "fish_for_food");
        
        var morningDC = (int)morningFishing.Action.Parameters["dc"];
        var afternoonDC = (int)afternoonFishing.Action.Parameters["dc"];
        
        Assert.True(morningDC < afternoonDC, $"Morning DC ({morningDC}) should be lower than afternoon DC ({afternoonDC})");
    }

    [Fact]
    public void FishingDC_RainyIsLowerThanClear()
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actorState = domain.CreateActorState(actorId, new Dictionary<string, object> { ["hunger"] = 60.0 });
        
        // Rainy scenario
        var rainyWorld = new IslandWorldState
        {
            TimeOfDay = 0.5,
            Weather = Weather.Rainy,
            FishAvailable = 100.0
        };
        
        // Clear scenario
        var clearWorld = new IslandWorldState
        {
            TimeOfDay = 0.5,
            Weather = Weather.Clear,
            FishAvailable = 100.0
        };
        
        var rainyCandidates = domain.GenerateCandidates(actorId, actorState, rainyWorld, 0.0, new Random(42));
        var clearCandidates = domain.GenerateCandidates(actorId, actorState, clearWorld, 0.0, new Random(42));
        
        var rainyFishing = rainyCandidates.First(c => c.Action.Id.Value == "fish_for_food");
        var clearFishing = clearCandidates.First(c => c.Action.Id.Value == "fish_for_food");
        
        var rainyDC = (int)rainyFishing.Action.Parameters["dc"];
        var clearDC = (int)clearFishing.Action.Parameters["dc"];
        
        Assert.True(rainyDC < clearDC, $"Rainy DC ({rainyDC}) should be lower than clear DC ({clearDC})");
    }

    [Fact]
    public void CoconutDC_WindyIsLowerThanClear()
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actorState = domain.CreateActorState(actorId, new Dictionary<string, object> { ["hunger"] = 60.0 });
        
        // Windy scenario
        var windyWorld = new IslandWorldState
        {
            Weather = Weather.Windy,
            CoconutsAvailable = 5
        };
        
        // Clear scenario
        var clearWorld = new IslandWorldState
        {
            Weather = Weather.Clear,
            CoconutsAvailable = 5
        };
        
        var windyCandidates = domain.GenerateCandidates(actorId, actorState, windyWorld, 0.0, new Random(42));
        var clearCandidates = domain.GenerateCandidates(actorId, actorState, clearWorld, 0.0, new Random(42));
        
        var windyCoconut = windyCandidates.First(c => c.Action.Id.Value == "shake_tree_coconut");
        var clearCoconut = clearCandidates.First(c => c.Action.Id.Value == "shake_tree_coconut");
        
        var windyDC = (int)windyCoconut.Action.Parameters["dc"];
        var clearDC = (int)clearCoconut.Action.Parameters["dc"];
        
        Assert.True(windyDC < clearDC, $"Windy DC ({windyDC}) should be lower than clear DC ({clearDC})");
    }

    [Fact]
    public void CoconutDC_ScarcityIncreasesWithFewerCoconuts()
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actorState = domain.CreateActorState(actorId, new Dictionary<string, object> { ["hunger"] = 60.0 });
        
        // Many coconuts scenario
        var manyCoconutsWorld = new IslandWorldState
        {
            Weather = Weather.Clear,
            CoconutsAvailable = 5
        };
        
        // Few coconuts scenario
        var fewCoconutsWorld = new IslandWorldState
        {
            Weather = Weather.Clear,
            CoconutsAvailable = 2
        };
        
        var manyCandidates = domain.GenerateCandidates(actorId, actorState, manyCoconutsWorld, 0.0, new Random(42));
        var fewCandidates = domain.GenerateCandidates(actorId, actorState, fewCoconutsWorld, 0.0, new Random(42));
        
        var manyCoconutAction = manyCandidates.First(c => c.Action.Id.Value == "shake_tree_coconut");
        var fewCoconutAction = fewCandidates.First(c => c.Action.Id.Value == "shake_tree_coconut");
        
        var manyDC = (int)manyCoconutAction.Action.Parameters["dc"];
        var fewDC = (int)fewCoconutAction.Action.Parameters["dc"];
        
        Assert.True(fewDC > manyDC, $"Few coconuts DC ({fewDC}) should be higher than many coconuts DC ({manyDC})");
    }
}

public class IslandCooldownSerializationTests
{
    [Fact]
    public void ActorState_SerializeDeserialize_PreservesCooldowns()
    {
        var originalState = new IslandActorState
        {
            Id = new ActorId("TestActor"),
            Status = ActorStatus.Ready,
            LastDecisionTime = 10.0,
            STR = 12,
            DEX = 14,
            CON = 10,
            INT = 10,
            WIS = 16,
            CHA = 8,
            Hunger = 50.0,
            Energy = 75.0,
            Morale = 60.0,
            Boredom = 20.0,
            LastPlaneSightingTime = 100.0,
            LastMermaidEncounterTime = 200.0
        };
        
        var serialized = originalState.Serialize();
        
        var deserializedState = new IslandActorState();
        deserializedState.Deserialize(serialized);
        
        Assert.Equal(originalState.LastPlaneSightingTime, deserializedState.LastPlaneSightingTime);
        Assert.Equal(originalState.LastMermaidEncounterTime, deserializedState.LastMermaidEncounterTime);
        Assert.Equal(originalState.STR, deserializedState.STR);
        Assert.Equal(originalState.Hunger, deserializedState.Hunger);
    }

    [Fact]
    public void ActorState_SerializeDeserialize_DefaultsToNegativeInfinity()
    {
        var originalState = new IslandActorState
        {
            Id = new ActorId("TestActor"),
            Status = ActorStatus.Ready,
            LastDecisionTime = 0.0,
            STR = 10,
            DEX = 10,
            CON = 10,
            INT = 10,
            WIS = 10,
            CHA = 10
        };
        
        // Don't set cooldowns - they should default to -infinity
        var serialized = originalState.Serialize();
        
        var deserializedState = new IslandActorState();
        deserializedState.Deserialize(serialized);
        
        Assert.True(double.IsNegativeInfinity(deserializedState.LastPlaneSightingTime));
        Assert.True(double.IsNegativeInfinity(deserializedState.LastMermaidEncounterTime));
    }

    [Fact]
    public void VignetteCooldown_PreventsDuplicateEvents()
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actorState = new IslandActorState
        {
            Id = actorId,
            LastPlaneSightingTime = 50.0
        };
        var worldState = new IslandWorldState();
        
        // At time 100, cooldown is not expired (only 50 seconds elapsed, need 600)
        var candidatesEarly = domain.GenerateCandidates(actorId, actorState, worldState, 100.0, new Random(42));
        var hasPlaneEarly = candidatesEarly.Any(c => c.Action.Id.Value == "plane_sighting");
        
        // At time 700, cooldown is expired (650 seconds elapsed, exceeds 600)
        var candidatesLate = domain.GenerateCandidates(actorId, actorState, worldState, 700.0, new Random(42));
        // Note: This might not always have plane_sighting due to random chance, but cooldown is no longer preventing it
        
        Assert.False(hasPlaneEarly, "Plane sighting should not appear when cooldown is active");
    }
}


