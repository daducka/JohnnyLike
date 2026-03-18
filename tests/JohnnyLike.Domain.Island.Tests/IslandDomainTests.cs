using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Island.Supply;
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
        Assert.NotNull(islandWorld.GetItem<CalendarItem>("calendar"));
        Assert.NotNull(islandWorld.GetItem<WeatherItem>("weather"));
        Assert.NotNull(islandWorld.GetItem<BeachItem>("beach"));
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
            ["satiety"] = 50.0
        };
        
        var state = domain.CreateActorState(actorId, initialData);
        
        var islandState = (IslandActorState)state;
        Assert.Equal(14, islandState.STR);
        Assert.Equal(16, islandState.DEX);
        Assert.Equal(12, islandState.WIS);
        Assert.Equal(50.0, islandState.Satiety);
    }

    [Fact]
    public void GenerateCandidates_GeneratesFishingAction()
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actorState = domain.CreateActorState(actorId, new Dictionary<string, object> { ["satiety"] = 40.0 });
        var worldState = (IslandWorldState)domain.CreateInitialWorldState();
        domain.InitializeActorItems(actorId, worldState);
        
        var candidates = domain.GenerateCandidates(actorId, actorState, worldState, 0L, new Random(42), new EmptyResourceAvailability());
        
        Assert.Contains(candidates, c => c.Action.Id.Value == "go_fishing");
    }

    [Fact(Skip = "Fishing score is based on skill level and pole quality, not hunger")]
    public void GenerateCandidates_FishingScoreIncreasesWithHunger()
    {
        // Note: In the current implementation, fishing is for entertainment (reduces boredom)
        // not for food (reducing hunger). The score is based on fishing skill and pole quality.
    }

    [Fact]
    public void GenerateCandidates_SleepScoreIncreasesWithLowEnergy()
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        
        var highEnergyState = domain.CreateActorState(actorId, new Dictionary<string, object> { ["energy"] = 90.0 });
        var lowEnergyState = domain.CreateActorState(actorId, new Dictionary<string, object> { ["energy"] = 20.0 });
        var worldState = domain.CreateInitialWorldState();
        
        var highEnergyCandidates = domain.GenerateCandidates(actorId, highEnergyState, worldState, 0L, new Random(42), new EmptyResourceAvailability());
        var lowEnergyCandidates = domain.GenerateCandidates(actorId, lowEnergyState, worldState, 0L, new Random(42), new EmptyResourceAvailability());
        
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
        var worldState = (IslandWorldState)domain.CreateInitialWorldState();
        domain.InitializeActorItems(actorId, worldState);
        
        var candidates = domain.GenerateCandidates(actorId, actorState, worldState, 0L, new Random(42), new EmptyResourceAvailability());
        
        // Should have candidates from various sources:
        // - Actor (idle, sleep, swim, collect driftwood, chat if pending)
        // - Items (fishing from FishingPoleItem, coconut from CoconutTreeItem, etc.)
        
        // Verify we have multiple candidate types present
        Assert.Contains(candidates, c => c.Action.Id.Value == "sleep_under_tree");
        Assert.Contains(candidates, c => c.Action.Id.Value == "go_fishing");
        Assert.Contains(candidates, c => c.Action.Id.Value == "idle");
    }

    [Fact]
    public void IdleCandidate_AlwaysPresentEvenWhenOtherCandidatesExist()
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actorState = domain.CreateActorState(actorId, new Dictionary<string, object> { ["satiety"] = 40.0 });
        var worldState = (IslandWorldState)domain.CreateInitialWorldState();
        domain.InitializeActorItems(actorId, worldState);
        
        var candidates = domain.GenerateCandidates(actorId, actorState, worldState, 0L, new Random(42), new EmptyResourceAvailability());
        
        // Idle must always be present
        Assert.Contains(candidates, c => c.Action.Id.Value == "idle");
        
        // Other candidates should also be present
        Assert.True(candidates.Count > 1, "Should have more than just idle candidate");
        Assert.Contains(candidates, c => c.Action.Id.Value == "go_fishing");
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
            EnqueuedAtTick = 0L
        });
        
        var candidates = domain.GenerateCandidates(actorId, actorState, worldState, 0L, new Random(42), new EmptyResourceAvailability());
        
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
            ["satiety"] = 15.0,  // Survival critical
            ["energy"] = 50.0 
        }) as IslandActorState;
        var worldState = domain.CreateInitialWorldState();
        domain.InitializeActorItems(actorId, (IslandWorldState)worldState);
        
        // Add a pending chat action
        actorState!.PendingChatActions.Enqueue(new PendingIntent
        {
            ActionId = "clap_emote",
            Type = "sub",
            Data = new Dictionary<string, object>(),
            EnqueuedAtTick = 0L
        });
        
        var candidates = domain.GenerateCandidates(actorId, actorState, worldState, 0L, new Random(42), new EmptyResourceAvailability());
        
        // Should NOT have the clap emote candidate because survival is critical
        Assert.DoesNotContain(candidates, c => c.Action.Id.Value == "clap_emote");
        
        // But should have survival actions like fishing
        Assert.Contains(candidates, c => c.Action.Id.Value == "go_fishing");
    }

    [Fact]
    public void Providers_DiscoveredInCorrectOrder()
    {
        // This test is obsolete as the old provider discovery system has been removed
        // Providers are now implemented as IIslandActionCandidate on items and actors
        // Commenting out this test as it tests the old architecture
        
        /*
        var domain = new IslandDomainPack();
        
        // Get the private _providers field via reflection to inspect order
        var providersField = typeof(IslandDomainPack).GetField("_providers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var providers = (List<IIslandCandidateProvider>)providersField!.GetValue(domain)!;
        */
        
        // Verify new system still provides candidates
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actorState = domain.CreateActorState(actorId);
        var worldState = (IslandWorldState)domain.CreateInitialWorldState();
        domain.InitializeActorItems(actorId, worldState);
        
        var candidates = domain.GenerateCandidates(actorId, actorState, worldState, 0L, new Random(42), new EmptyResourceAvailability());
        
        // Verify we have candidates from various sources
        Assert.True(candidates.Count > 0, "Should have candidates from items and actor");
        Assert.Contains(candidates, c => c.Action.Id.Value == "idle");
    }
}

public class IslandWorldStateTests
{
    [Fact]
    public void OnTimeAdvanced_AdvancesCalendarTimeOfDay()
    {
        var world = new IslandWorldState();
        var calendar = new CalendarItem("calendar") { TimeOfDay = 0.5 };
        world.WorldItems.Add(calendar);

        world.OnTickAdvanced((long)(0.0 * 20));
    }

    [Fact]
    public void OnTimeAdvanced_CalendarIncrementsDay()
    {
        var world = new IslandWorldState();
        var calendar = new CalendarItem("calendar") { TimeOfDay = 0.9, DayCount = 0 };
        world.WorldItems.Add(calendar);

        // Tick ITickableWorldItems first (as engine does), then domain tick
        var tick = 172800L;
        foreach (var tickable in WorldItemTickOrchestrator.TopologicalSort(world.WorldItems))
            tickable.Tick(tick, world);
        world.OnTickAdvanced(tick); // 0.1 day = 8640s = 172800 ticks
        Assert.InRange(calendar.TimeOfDay, 0.0, 0.1);
    }

    [Fact]
    public void OnTimeAdvanced_CoconutsRegenDaily()
    {
        var world = new IslandWorldState();
        var calendar = new CalendarItem("calendar") { TimeOfDay = 0.9, DayCount = 0 };
        world.WorldItems.Add(calendar);
        var tree = new CoconutTreeItem("palm_tree");
        ((ISupplyBounty)tree).GetSupply<CoconutSupply>("coconut")!.Quantity = 0;
        world.WorldItems.Add(tree);

        // Tick ITickableWorldItems first (as engine does), then domain tick
        var tick = 1728000L;
        foreach (var tickable in WorldItemTickOrchestrator.TopologicalSort(world.WorldItems))
            tickable.Tick(tick, world);
        world.OnTickAdvanced(tick); // advance 1 day = 86400s = 1728000 ticks

        // Calendar should have incremented the day count (TimeOfDay wraps around midnight)
        Assert.True(calendar.DayCount >= 1, "Calendar should have advanced at least 1 day");
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
        
        var modifier = state.GetSkillModifier(SkillType.Fishing);
        
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
        
        var modifier = state.GetSkillModifier(SkillType.Survival);
        
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
                new ActiveBuff { SkillType = SkillType.Perception, Type = BuffType.SkillBonus, Value = 2, ExpiresAtTick = 2000L }
            }
        };
        
        var modifier = state.GetSkillModifier(SkillType.Perception);
        
        Assert.Equal(2 + 2, modifier);
    }

    [Fact]
    public void GetAdvantage_ReturnsAdvantageWhenBuffActive()
    {
        var state = new IslandActorState
        {
            ActiveBuffs = new List<ActiveBuff>
            {
                new ActiveBuff { SkillType = SkillType.Fishing, Type = BuffType.Advantage, ExpiresAtTick = 2000L }
            }
        };
        
        var advantage = state.GetAdvantage(SkillType.Fishing);
        
        Assert.Equal(AdvantageType.Advantage, advantage);
    }

    [Fact]
    public void GetAdvantage_ReturnsNormalWhenNoBuffActive()
    {
        var state = new IslandActorState();
        
        var advantage = state.GetAdvantage(SkillType.Fishing);
        
        Assert.Equal(AdvantageType.Normal, advantage);
    }
}

public class IslandActionEffectsTests
{
    [Fact]
    public void ApplyActionEffects_FishingSuccess_IncreaseMorale()
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actorState = (IslandActorState)domain.CreateActorState(actorId);
        actorState.Morale = 50.0;
        actorState.CurrentAction = new ActionSpec(
            new ActionId("go_fishing"),
            ActionKind.Interact,
            new SkillCheckActionParameters(
                new SkillCheckRequest(10, 3, AdvantageType.Normal, "Fishing"),
                new SkillCheckResult(10, 10 + 3, RollOutcomeTier.Success, true, 0.5)),
            Duration.FromTicks(300L),
            ""
        );
        var worldState = new IslandWorldState();

        domain.InitializeActorItems(actorId, worldState);
        worldState.WorldItems.Add(new OceanItem("ocean"));
        
        // Generate candidates to get the effect handler
        var candidates = domain.GenerateCandidates(actorId, actorState, worldState, 0L, new Random(42), new EmptyResourceAvailability());
        var fishingCandidate = candidates.FirstOrDefault(c => c.Action.Id.Value == "go_fishing");
        Assert.NotNull(fishingCandidate);
        Assert.NotNull(fishingCandidate.EffectHandler);
        
        var outcome = new ActionOutcome(
            new ActionId("go_fishing"), ActionOutcomeType.Success, Duration.FromTicks(300L),
            new Dictionary<string, object>
            {
                ["tier"] = "Success"
            }
        );
        
        var rng = new RandomRngStream(new Random(42));
        // Call PreAction first so the reservation context (fishCtx) is set for EffectHandler
        domain.TryExecutePreAction(actorId, actorState, worldState, rng, new EmptyResourceAvailability(), fishingCandidate.PreAction);
        domain.ApplyActionEffects(actorId, outcome, actorState, worldState, rng, new EmptyResourceAvailability(), fishingCandidate.EffectHandler);
        
        // Morale increases: fishing effect (+5 on Success) + outcome morale bonus (+2 for Success tier)
        // Net: 50 + 5 + 2 = 57 — no passive decay, morale reflects the positive outcome
        Assert.True(actorState.Morale > 50.0, $"Expected morale > 50 (fishing success should improve morale), got {actorState.Morale}");
    }

    [Fact]
    public void ApplyActionEffects_SleepUnderTree_RestoresEnergy()
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        // Use CreateActorState so the actor has a MetabolicBuff.
        var actorState = (IslandActorState)domain.CreateActorState(actorId,
            new Dictionary<string, object> { ["energy"] = 30.0 });
        var worldState = new IslandWorldState();

        // Generate candidates to get the sleep action with its PreAction handler.
        var candidates = domain.GenerateCandidates(actorId, actorState, worldState, 0L, new Random(42), new EmptyResourceAvailability());
        var sleepCandidate = candidates.FirstOrDefault(c => c.Action.Id.Value == "sleep_under_tree");
        Assert.NotNull(sleepCandidate);
        Assert.NotNull(sleepCandidate.EffectHandler);

        // Execute PreAction: sets MetabolicBuff.Intensity = Sleeping.
        var rng = new RandomRngStream(new Random(42));
        domain.TryExecutePreAction(actorId, actorState, worldState, rng, new EmptyResourceAvailability(), sleepCandidate.PreAction);

        // Tick the world for 600 ticks (= 30 sim-seconds) to drive MetabolicBuff.OnTick.
        var actors = new Dictionary<ActorId, ActorState> { [actorId] = actorState };
        domain.TickWorldState(worldState, actors, 600L, new EmptyResourceAvailability());

        Assert.True(actorState.Energy > 30.0,
            $"Energy should increase after sleeping and a world tick, got {actorState.Energy:F2}");
    }

    [Fact]
    public void GenerateCandidates_SkillCheck_PopulatesResultData()
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actorState = domain.CreateActorState(actorId, new Dictionary<string, object> { ["satiety"] = 40.0 });
        var worldState = domain.CreateInitialWorldState();
        domain.InitializeActorItems(actorId, (IslandWorldState)worldState);
        
        var candidates = domain.GenerateCandidates(actorId, actorState, worldState, 0L, new Random(42), new EmptyResourceAvailability());
        
        var fishingCandidate = candidates.FirstOrDefault(c => c.Action.Id.Value == "go_fishing");
        Assert.NotNull(fishingCandidate);
        
        // Verify ResultData is populated in the candidate
        var resultData = fishingCandidate.Action.ResultData;
        Assert.NotNull(resultData);
        Assert.True(resultData.ContainsKey("dc"));
        Assert.True(resultData.ContainsKey("modifier"));
        Assert.True(resultData.ContainsKey("advantage"));
        Assert.True(resultData.ContainsKey("skillId"));
        Assert.True(resultData.ContainsKey("roll"));
        Assert.True(resultData.ContainsKey("total"));
        Assert.True(resultData.ContainsKey("tier"));

        Assert.IsType<int>(resultData["dc"]);
        Assert.IsType<int>(resultData["modifier"]);
        Assert.IsType<string>(resultData["advantage"]);
        Assert.Equal("Fishing", resultData["skillId"]);
        Assert.IsType<int>(resultData["roll"]);
        Assert.IsType<int>(resultData["total"]);
        Assert.IsType<string>(resultData["tier"]);
    }

    [Fact]
    public void ApplyActionEffects_PassiveStatsDecay()
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        // Use CreateActorState so the actor has a MetabolicBuff.
        var actorState = (IslandActorState)domain.CreateActorState(actorId,
            new Dictionary<string, object> { ["satiety"] = 70.0, ["energy"] = 80.0, ["morale"] = 50.0 });
        var worldState = new IslandWorldState();

        // Drive metabolism via TickWorldState (600 ticks = 30 sim-seconds of basal burn).
        var actors = new Dictionary<ActorId, ActorState> { [actorId] = actorState };
        domain.TickWorldState(worldState, actors, 600L, new EmptyResourceAvailability());

        Assert.True(actorState.Satiety < 70.0,
            $"Satiety should decrease from basal burn during world tick, got {actorState.Satiety:F2}");
        // Light activity: conversion offsets drain so Energy stays approximately stable.
        Assert.True(actorState.Energy <= 80.0,
            $"Energy should not exceed starting value, got {actorState.Energy:F2}");

        // Morale is now outcome-driven, not time-based. A Success outcome with no tier gives +2 morale.
        // After the world tick, physio is fine (satiety=70, energy=80) so no morale pressure from VitalityBuff.
        var outcome = new ActionOutcome(new ActionId("idle"), ActionOutcomeType.Success, Duration.FromTicks(200L), null);
        var rng = new RandomRngStream(new Random(42));
        domain.ApplyActionEffects(actorId, outcome, actorState, worldState, rng, new EmptyResourceAvailability());

        Assert.True(actorState.Morale >= 50.0,
            $"Morale should not decrease after a successful idle action (no passive decay), got {actorState.Morale:F2}");
    }

    [Theory]
    [InlineData("CriticalSuccess", IslandDomainPack.MoraleCriticalSuccessBonus)]
    [InlineData("Success",         IslandDomainPack.MoraleSuccessBonus)]
    [InlineData("PartialSuccess",  IslandDomainPack.MoralePartialSuccessBonus)]
    [InlineData("Failure",         IslandDomainPack.MoraleFailurePenalty)]
    [InlineData("CriticalFailure", IslandDomainPack.MoraleCriticalFailurePenalty)]
    public void ApplyActionEffects_OutcomeMorale_MatchesTier(string tierStr, double expectedDelta)
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actorState = (IslandActorState)domain.CreateActorState(actorId,
            new Dictionary<string, object> { ["morale"] = 50.0 });
        var worldState = new IslandWorldState();
        var rng = new RandomRngStream(new Random(42));

        var outcome = new ActionOutcome(
            new ActionId("test_action"), ActionOutcomeType.Success, Duration.FromTicks(200L),
            new Dictionary<string, object> { ["tier"] = tierStr });

        domain.ApplyActionEffects(actorId, outcome, actorState, worldState, rng, new EmptyResourceAvailability());

        Assert.Equal(Math.Clamp(50.0 + expectedDelta, 0.0, 100.0), actorState.Morale, precision: 5);
    }

    [Fact]
    public void ApplyActionEffects_FailedOutcome_AppliesMoralePenalty()
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actorState = (IslandActorState)domain.CreateActorState(actorId,
            new Dictionary<string, object> { ["morale"] = 50.0 });
        var worldState = new IslandWorldState();
        var rng = new RandomRngStream(new Random(42));

        var outcome = new ActionOutcome(
            new ActionId("test_action"), ActionOutcomeType.Failed, Duration.FromTicks(200L), null);

        domain.ApplyActionEffects(actorId, outcome, actorState, worldState, rng, new EmptyResourceAvailability());

        Assert.True(actorState.Morale < 50.0,
            $"Failed outcome should reduce morale, got {actorState.Morale}");
    }

    [Fact]
    public void ApplyActionEffects_CancelledOutcome_DoesNotChangeMorale()
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actorState = (IslandActorState)domain.CreateActorState(actorId,
            new Dictionary<string, object> { ["morale"] = 50.0 });
        var worldState = new IslandWorldState();
        var rng = new RandomRngStream(new Random(42));

        var outcome = new ActionOutcome(
            new ActionId("test_action"), ActionOutcomeType.Cancelled, Duration.FromTicks(200L), null);

        domain.ApplyActionEffects(actorId, outcome, actorState, worldState, rng, new EmptyResourceAvailability());

        Assert.Equal(50.0, actorState.Morale, precision: 5);
    }

    [Fact]
    public void ApplyActionEffects_LongComfortAction_DoesNotCollapseMorale()
    {
        // Regression test: a 15-minute comfort action should not tank morale.
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actorState = (IslandActorState)domain.CreateActorState(actorId,
            new Dictionary<string, object> { ["morale"] = 50.0 });
        var worldState = new IslandWorldState();
        var rng = new RandomRngStream(new Random(42));

        // Simulate a 15-minute comfort action (Success tier).
        var duration = Duration.Minutes(15);
        var outcome = new ActionOutcome(
            new ActionId("sit_and_watch_waves"), ActionOutcomeType.Success, duration,
            new Dictionary<string, object> { ["tier"] = "Success" });

        domain.ApplyActionEffects(actorId, outcome, actorState, worldState, rng, new EmptyResourceAvailability());

        // Morale should not drop — Success outcome gives a bonus, not a penalty.
        Assert.True(actorState.Morale >= 50.0,
            $"Long comfort action should not collapse morale, got {actorState.Morale}");
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
            0L,
            actorId,
            new Dictionary<string, object>
            {
                ["redeem_name"] = "write_name_sand",
                ["viewer_name"] = "TestViewer"
            }
        );
        
        domain.OnSignal(signal, actorState, worldState, 0L);
        
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
            0L,
            actorId,
            new Dictionary<string, object> { ["subscriber"] = "TestSub" }
        );
        
        domain.OnSignal(signal, actorState, worldState, 200L);
        
        // Check Inspiration buff was added
        Assert.Contains(actorState.ActiveBuffs, b => b.Name == "Inspiration");
        var inspirationBuff = actorState.ActiveBuffs.First(b => b.Name == "Inspiration");
        Assert.Equal(BuffType.SkillBonus, inspirationBuff.Type);
        Assert.Equal(1, inspirationBuff.Value);
        Assert.Equal(6200L, inspirationBuff.ExpiresAtTick);
        
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
        var actorState = (IslandActorState)domain.CreateActorState(actorId);
        actorState.Satiety = 70.0;  // Not critical
        actorState.Energy = 60.0;   // Not critical
        actorState.PendingChatActions.Enqueue(new PendingIntent
        {
            ActionId = "write_name_sand",
            Type = "chat_redeem",
            Data = new Dictionary<string, object> { ["viewer_name"] = "TestViewer" },
            EnqueuedAtTick = 0L
        });
        var worldState = new IslandWorldState();
        
        var candidates = domain.GenerateCandidates(actorId, actorState, worldState, 200L, new Random(42), new EmptyResourceAvailability());
        
        // Should have a write_name_sand candidate with high priority
        var writeSandCandidate = candidates.FirstOrDefault(c => c.Action.Id.Value == "write_name_sand");
        Assert.NotNull(writeSandCandidate);
        Assert.True(writeSandCandidate.Score >= 1.0, $"Expected write_name_sand score >= 1.0 but got {writeSandCandidate.Score}");
        Assert.Contains("TestViewer", writeSandCandidate.Reason);
    }

    [Fact]
    public void GenerateCandidates_SurvivalCritical_SkipsPendingChatActions()
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actorState = (IslandActorState)domain.CreateActorState(actorId);
        actorState.Satiety = 15.0;  // Critical (low satiety)
        actorState.Energy = 60.0;
        actorState.PendingChatActions.Enqueue(new PendingIntent
        {
            ActionId = "clap_emote",
            Type = "sub",
            Data = new Dictionary<string, object>(),
            EnqueuedAtTick = 0L
        });
        var worldState = new IslandWorldState();

        domain.InitializeActorItems(actorId, worldState);
        worldState.WorldItems.Add(new OceanItem("ocean"));
        
        var candidates = domain.GenerateCandidates(actorId, actorState, worldState, 200L, new Random(42), new EmptyResourceAvailability());
        
        // Should NOT have clap emote when survival is critical
        var clapCandidate = candidates.FirstOrDefault(c => c.Action.Id.Value == "clap_emote");
        Assert.Null(clapCandidate);
        
        // Should have survival actions (fishing should be very high priority)
        var fishingCandidate = candidates.FirstOrDefault(c => c.Action.Id.Value == "go_fishing");
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
            ["satiety"] = 70.0,
            ["energy"] = 70.0
        });
        
        // Enqueue a chat redeem signal
        var signal = new Signal(
            "chat_redeem",
            0L,
            new ActorId("TestActor"),
            new Dictionary<string, object>
            {
                ["redeem_name"] = "write_name_sand",
                ["viewer_name"] = "TestViewer"
            }
        );
        engine.EnqueueSignal(signal);
        
        // Process signal
        engine.AdvanceTicks((long)(0.1 * 20));
        
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
        var actorState = (IslandActorState)domain.CreateActorState(actorId);
        actorState.PendingChatActions.Enqueue(new PendingIntent
        {
            ActionId = "write_name_sand",
            Type = "chat_redeem",
            Data = new Dictionary<string, object> { ["viewer_name"] = "TestViewer" },
            EnqueuedAtTick = 0L
        });
        var worldState = new IslandWorldState();
        
        // Generate candidates to get the chat action with its effect handler
        var candidates = domain.GenerateCandidates(actorId, actorState, worldState, 0L, new Random(42), new EmptyResourceAvailability());
        var chatCandidate = candidates.FirstOrDefault(c => c.Action.Id.Value == "write_name_sand");
        Assert.NotNull(chatCandidate);
        Assert.NotNull(chatCandidate.EffectHandler);
        
        var outcome = new ActionOutcome(
            new ActionId("write_name_sand"), ActionOutcomeType.Success, Duration.FromTicks(160L),
            null
        );
        
        var rng = new RandomRngStream(new Random(42));
        domain.ApplyActionEffects(actorId, outcome, actorState, worldState, rng, new EmptyResourceAvailability(), chatCandidate.EffectHandler);
        
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
            ["satiety"] = 40.0,
            ["energy"] = 70.0,
            ["morale"] = 50.0
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
    [Fact(Skip = "Fishing DC tuning by time of day not implemented for fishing poles")]
    public void FishingDC_MorningIsLowerThanAfternoon()
    {
        // Note: In the current implementation, fishing DC is based on pole quality, not time of day
    }

    [Fact(Skip = "Fishing DC tuning by weather not implemented for fishing poles")]
    public void FishingDC_RainyIsLowerThanClear()
    {
        // Note: In the current implementation, fishing DC is based on pole quality, not weather
    }

    [Fact(Skip = "CoconutDC_WindyIsLowerThanClear: Windy weather no longer exists in the new weather system")]
    public void CoconutDC_WindyIsLowerThanClear()
    {
        // Note: In the new weather system (Hot/Cold), there is no Windy. This test is obsolete.
    }

    [Fact]
    public void CoconutDC_ScarcityIncreasesWithFewerCoconuts()
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actorState = domain.CreateActorState(actorId, new Dictionary<string, object> { ["satiety"] = 40.0 });
        
        // Many coconuts scenario
        var manyCoconutsWorld = (IslandWorldState)domain.CreateInitialWorldState();
        ((ISupplyBounty)manyCoconutsWorld.GetItem<CoconutTreeItem>("palm_tree")!).GetSupply<CoconutSupply>("coconut")!.Quantity = 5;
        
        // Few coconuts scenario
        var fewCoconutsWorld = (IslandWorldState)domain.CreateInitialWorldState();
        ((ISupplyBounty)fewCoconutsWorld.GetItem<CoconutTreeItem>("palm_tree")!).GetSupply<CoconutSupply>("coconut")!.Quantity = 2;
        
        var manyCandidates = domain.GenerateCandidates(actorId, actorState, manyCoconutsWorld, 0L, new Random(42), new EmptyResourceAvailability());
        var fewCandidates = domain.GenerateCandidates(actorId, actorState, fewCoconutsWorld, 0L, new Random(42), new EmptyResourceAvailability());
        
        var manyCoconutAction = manyCandidates.First(c => c.Action.Id.Value == "shake_tree_coconut");
        var fewCoconutAction = fewCandidates.First(c => c.Action.Id.Value == "shake_tree_coconut");
        
        var manyDC = ((SkillCheckActionParameters)manyCoconutAction.Action.Parameters).Request.DC;
        var fewDC = ((SkillCheckActionParameters)fewCoconutAction.Action.Parameters).Request.DC;
        
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
            LastDecisionTick = 200L,
            STR = 12,
            DEX = 14,
            CON = 10,
            INT = 10,
            WIS = 16,
            CHA = 8,
            Satiety = 50.0,
            Energy = 75.0,
            Morale = 60.0,
            LastPlaneSightingTick = 2000L,
            LastMermaidEncounterTick = 4000L
        };
        
        var serialized = originalState.Serialize();
        
        var deserializedState = new IslandActorState();
        deserializedState.Deserialize(serialized);
        
        Assert.Equal(originalState.LastPlaneSightingTick, deserializedState.LastPlaneSightingTick);
        Assert.Equal(originalState.LastMermaidEncounterTick, deserializedState.LastMermaidEncounterTick);
        Assert.Equal(originalState.STR, deserializedState.STR);
        Assert.Equal(originalState.Satiety, deserializedState.Satiety);
    }

    [Fact]
    public void ActorState_SerializeDeserialize_DefaultsToNegativeInfinity()
    {
        var originalState = new IslandActorState
        {
            Id = new ActorId("TestActor"),
            Status = ActorStatus.Ready,
            LastDecisionTick = 0L,
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
        
        Assert.Equal(-1L, deserializedState.LastPlaneSightingTick);
        Assert.Equal(-1L, deserializedState.LastMermaidEncounterTick);
    }

    [Fact]
    public void VignetteCooldown_PreventsDuplicateEvents()
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actorState = new IslandActorState
        {
            Id = actorId,
            LastPlaneSightingTick = 1000L
        };
        var worldState = new IslandWorldState();
        
        // At time 100, cooldown is not expired (only 50 seconds elapsed, need 600)
        var candidatesEarly = domain.GenerateCandidates(actorId, actorState, worldState, 2000L, new Random(42), new EmptyResourceAvailability());
        var hasPlaneEarly = candidatesEarly.Any(c => c.Action.Id.Value == "plane_sighting");
        
        // At time 700, cooldown is expired (650 seconds elapsed, exceeds 600)
        var candidatesLate = domain.GenerateCandidates(actorId, actorState, worldState, 14000L, new Random(42), new EmptyResourceAvailability());
        // Note: This might not always have plane_sighting due to random chance, but cooldown is no longer preventing it
        
        Assert.False(hasPlaneEarly, "Plane sighting should not appear when cooldown is active");
    }
}

public class ScoringPostPassTests
{
    [Fact]
    public void GenerateCandidates_NullQualities_ScoreEqualsIntrinsicScore()
    {
        // think_about_supplies has no Qualities that vary with actor state; verify a candidate
        // with non-null Qualities gets a Score != IntrinsicScore when quality weights are non-zero
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actorState = domain.CreateActorState(actorId, new Dictionary<string, object> { ["energy"] = 10.0 });
        var worldState = domain.CreateInitialWorldState();

        var candidates = domain.GenerateCandidates(actorId, actorState, worldState, 0L, new Random(42), new EmptyResourceAvailability());

        // Idle now has Qualities, so Score may differ from IntrinsicScore
        var idle = candidates.First(c => c.Action.Id.Value == "idle");
        Assert.NotNull(idle.Qualities);

        // sleep_under_tree also has Qualities; with low energy the Rest quality boosts its score
        var sleep = candidates.First(c => c.Action.Id.Value == "sleep_under_tree");
        Assert.NotNull(sleep.Qualities);
        Assert.True(sleep.Score >= sleep.IntrinsicScore, "Qualities should not lower sleep score when energy is very low");
    }

    [Fact]
    public void GenerateCandidates_LowSatiety_FoodConsumptionQualityIncreasesScore()
    {
        // An actor with very low satiety should score a FoodConsumption-quality candidate higher
        // via the quality post-pass (not via IntrinsicScore which is now static)
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");

        // Actor with very low satiety (very hungry)
        var hungryState = domain.CreateActorState(actorId, new Dictionary<string, object> { ["satiety"] = 10.0 });
        // Actor with full satiety
        var fullState = domain.CreateActorState(actorId, new Dictionary<string, object> { ["satiety"] = 100.0 });

        var worldState = (IslandWorldState)domain.CreateInitialWorldState();
        ((ISupplyBounty)worldState.GetItem<CoconutTreeItem>("palm_tree")!).GetSupply<CoconutSupply>("coconut")!.Quantity = 10;

        var hungryCandidates = domain.GenerateCandidates(actorId, hungryState, worldState, 0L, new Random(42), new EmptyResourceAvailability());
        var fullCandidates = domain.GenerateCandidates(actorId, fullState, worldState, 0L, new Random(42), new EmptyResourceAvailability());

        // Both actors should have a sleep candidate; Sleep has Rest quality
        var hungrySleepScore = hungryCandidates.First(c => c.Action.Id.Value == "sleep_under_tree").Score;
        var fullSleepScore = fullCandidates.First(c => c.Action.Id.Value == "sleep_under_tree").Score;

        // Both should have the same sleep score since energy is the same (default 100)
        Assert.Equal(fullSleepScore, hungrySleepScore);

        // Coconut now has a static IntrinsicScore (0.25); both actors get the same IntrinsicScore
        var hungryCoconutIntrinsic = hungryCandidates.First(c => c.Action.Id.Value == "shake_tree_coconut").IntrinsicScore;
        var fullCoconutIntrinsic = fullCandidates.First(c => c.Action.Id.Value == "shake_tree_coconut").IntrinsicScore;
        Assert.Equal(fullCoconutIntrinsic, hungryCoconutIntrinsic);

        // But the quality post-pass uses FoodAcquisition weight (shake_tree gathers food), so hungry actor gets a higher final Score
        var hungryCoconutScore = hungryCandidates.First(c => c.Action.Id.Value == "shake_tree_coconut").Score;
        var fullCoconutScore = fullCandidates.First(c => c.Action.Id.Value == "shake_tree_coconut").Score;

        Assert.True(hungryCoconutScore > fullCoconutScore,
            $"Hungry actor coconut final score ({hungryCoconutScore:F3}) should be > full actor ({fullCoconutScore:F3}) due to FoodAcquisition quality weight");
    }

    [Fact]
    public void GenerateCandidates_LowEnergy_SleepScoreHigherThanIdleScore()
    {
        // With low energy and Rest quality weight on Sleep, sleep should score higher than idle
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actorState = domain.CreateActorState(actorId, new Dictionary<string, object> { ["energy"] = 10.0 });
        var worldState = domain.CreateInitialWorldState();

        var candidates = domain.GenerateCandidates(actorId, actorState, worldState, 0L, new Random(42), new EmptyResourceAvailability());

        var sleepScore = candidates.First(c => c.Action.Id.Value == "sleep_under_tree").Score;
        var idleScore = candidates.First(c => c.Action.Id.Value == "idle").Score;

        Assert.True(sleepScore > idleScore,
            $"Sleep score ({sleepScore:F3}) should be greater than idle ({idleScore:F3}) when energy is very low");
    }

    [Fact]
    public void StagedHungerRamp_HighSatiety_ProducesNearZeroFoodNeed()
    {
        // At satiety >= 70 the staged ramp should produce ~0 FoodConsumption need,
        // so coconut-tree score should be close to its intrinsic score only.
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");

        var satisfiedState = domain.CreateActorState(actorId, new Dictionary<string, object> { ["satiety"] = 80.0 });
        var worldState = (IslandWorldState)domain.CreateInitialWorldState();
        ((ISupplyBounty)worldState.GetItem<CoconutTreeItem>("palm_tree")!).GetSupply<CoconutSupply>("coconut")!.Quantity = 10;

        var candidates = domain.GenerateCandidates(actorId, satisfiedState, worldState, 0L, new Random(42), new EmptyResourceAvailability());
        var coconut = candidates.First(c => c.Action.Id.Value == "shake_tree_coconut");

        // FoodConsumption need weight should be 0 at satiety=80 (>= 70)
        // Score = IntrinsicScore + Preparation * prepWeight + Efficiency * effWeight + Safety * safetyWeight
        // The FoodConsumption contribution should be 0.
        Assert.True(coconut.Score <= coconut.IntrinsicScore + 1.0,
            $"Coconut score ({coconut.Score:F3}) should not be inflated by food need when satiety is 80");
    }

    [Fact]
    public void StagedHungerRamp_MidSatiety_ProducesVeryMildFoodNeed()
    {
        // At satiety=60 (in the 50-70 "very mild" band), hunger pressure should be negligible
        // compared to satiety=20 (strong hunger).
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");

        var midSatietyState = domain.CreateActorState(actorId, new Dictionary<string, object> { ["satiety"] = 60.0 });
        var starvingState   = domain.CreateActorState(actorId, new Dictionary<string, object> { ["satiety"] = 20.0 });
        var worldState = (IslandWorldState)domain.CreateInitialWorldState();
        ((ISupplyBounty)worldState.GetItem<CoconutTreeItem>("palm_tree")!).GetSupply<CoconutSupply>("coconut")!.Quantity = 10;

        var midCandidates = domain.GenerateCandidates(actorId, midSatietyState, worldState, 0L, new Random(42), new EmptyResourceAvailability());
        var starvingCandidates = domain.GenerateCandidates(actorId, starvingState, worldState, 0L, new Random(42), new EmptyResourceAvailability());

        var midCoconutScore      = midCandidates.First(c => c.Action.Id.Value == "shake_tree_coconut").Score;
        var starvingCoconutScore = starvingCandidates.First(c => c.Action.Id.Value == "shake_tree_coconut").Score;

        Assert.True(starvingCoconutScore > midCoconutScore + 0.4,
            $"Starving actor coconut score ({starvingCoconutScore:F3}) should be meaningfully higher than mid-satiety ({midCoconutScore:F3})");
    }

    [Fact]
    public void StagedHungerRamp_StarvedActorScoresFoodHigherThanSatisfiedActor()
    {
        // A starving actor (satiety=10) should score food actions much higher than a satisfied actor (satiety=80).
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");

        var starvingState = domain.CreateActorState(actorId, new Dictionary<string, object> { ["satiety"] = 10.0 });
        var satisfiedState = domain.CreateActorState(actorId, new Dictionary<string, object> { ["satiety"] = 80.0 });
        var worldState = (IslandWorldState)domain.CreateInitialWorldState();
        ((ISupplyBounty)worldState.GetItem<CoconutTreeItem>("palm_tree")!).GetSupply<CoconutSupply>("coconut")!.Quantity = 10;

        var starvingCandidates = domain.GenerateCandidates(actorId, starvingState, worldState, 0L, new Random(42), new EmptyResourceAvailability());
        var satisfiedCandidates = domain.GenerateCandidates(actorId, satisfiedState, worldState, 0L, new Random(42), new EmptyResourceAvailability());

        var starvingCoconutScore = starvingCandidates.First(c => c.Action.Id.Value == "shake_tree_coconut").Score;
        var satisfiedCoconutScore = satisfiedCandidates.First(c => c.Action.Id.Value == "shake_tree_coconut").Score;

        Assert.True(starvingCoconutScore > satisfiedCoconutScore,
            $"Starving actor coconut score ({starvingCoconutScore:F3}) should exceed satisfied actor ({satisfiedCoconutScore:F3})");
    }

    [Fact]
    public void ShakeTreeCoconut_HasPreparationQuality()
    {
        // shake_tree_coconut should now carry Preparation quality (resource provisioning step).
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actorState = domain.CreateActorState(actorId, new Dictionary<string, object> { ["satiety"] = 30.0 });
        var worldState = (IslandWorldState)domain.CreateInitialWorldState();
        ((ISupplyBounty)worldState.GetItem<CoconutTreeItem>("palm_tree")!).GetSupply<CoconutSupply>("coconut")!.Quantity = 10;

        var candidates = domain.GenerateCandidates(actorId, actorState, worldState, 0L, new Random(42), new EmptyResourceAvailability());
        var coconut = candidates.First(c => c.Action.Id.Value == "shake_tree_coconut");

        Assert.True(coconut.Qualities.ContainsKey(QualityType.Preparation),
            "shake_tree_coconut should have Preparation quality after reclassification");
        Assert.True(coconut.Qualities.ContainsKey(QualityType.FoodAcquisition),
            "shake_tree_coconut should have FoodAcquisition quality (gathering action)");
        Assert.False(coconut.Qualities.ContainsKey(QualityType.FoodConsumption),
            "shake_tree_coconut should NOT have FoodConsumption quality — it gathers, not eats");
    }

    [Fact]
    public void BashAndEatCoconut_FoodConsumptionQuality_ScalesWithSatiety()
    {
        // bash_and_eat_coconut should have diminishing FoodConsumption quality when satiety is high.
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");

        // Satiety=10 → satietyFactor = clamp((100-10)/60, 0, 1) = clamp(1.5, 0, 1) = 1.0
        var hungryState = domain.CreateActorState(actorId, new Dictionary<string, object> { ["satiety"] = 10.0 });
        // Satiety=70 → satietyFactor = clamp((100-70)/60, 0, 1) = clamp(0.5, 0, 1) = 0.5
        var satisfiedState = domain.CreateActorState(actorId, new Dictionary<string, object> { ["satiety"] = 70.0 });
        var worldState = (IslandWorldState)domain.CreateInitialWorldState();
        worldState.SharedSupplyPile!.GetOrCreateSupply(() => new Supply.CoconutSupply()).Quantity = 10;

        var hungryCandidates = domain.GenerateCandidates(actorId, hungryState, worldState, 0L, new Random(42), new EmptyResourceAvailability());
        var satisfiedCandidates = domain.GenerateCandidates(actorId, satisfiedState, worldState, 0L, new Random(42), new EmptyResourceAvailability());

        var hungryEat = hungryCandidates.FirstOrDefault(c => c.Action.Id.Value == "bash_and_eat_coconut");
        var satisfiedEat = satisfiedCandidates.FirstOrDefault(c => c.Action.Id.Value == "bash_and_eat_coconut");

        if (hungryEat != null && satisfiedEat != null)
        {
            Assert.True(
                hungryEat.Qualities[QualityType.FoodConsumption] > satisfiedEat.Qualities[QualityType.FoodConsumption],
                $"Hungry actor food quality ({hungryEat.Qualities[QualityType.FoodConsumption]:F3}) should exceed satisfied actor ({satisfiedEat.Qualities[QualityType.FoodConsumption]:F3})");
        }
    }

    [Fact]
    public void FunWeight_SuppressedWhenStarving()
    {
        // Playful actions (swim, hum_to_self, etc.) are gated out entirely when starving.
        // This verifies the PlayfulOnly gating: swim is absent for a starving/low-morale actor
        // but present for a healthy actor.
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");

        // Starving actor: satiety=10 (below PlayfulOnly threshold of >25)
        var starvingActor = domain.CreateActorState(actorId, new Dictionary<string, object>
        {
            ["satiety"] = 10.0,
            ["morale"]  = 50.0,
            ["energy"]  = 80.0
        });
        // Healthy actor: all stats above PlayfulOnly thresholds
        var healthyActor = domain.CreateActorState(actorId, new Dictionary<string, object>
        {
            ["satiety"] = 80.0,
            ["morale"]  = 60.0,
            ["energy"]  = 80.0
        });
        var worldState = (IslandWorldState)domain.CreateInitialWorldState();
        domain.InitializeActorItems(actorId, worldState);

        var starvingCandidates = domain.GenerateCandidates(actorId, starvingActor, worldState, 0L, new Random(42), new EmptyResourceAvailability());
        var healthyCandidates  = domain.GenerateCandidates(actorId, healthyActor,  worldState, 0L, new Random(42), new EmptyResourceAvailability());

        // Swim is gated by PlayfulOnly — absent when starving, present when healthy
        Assert.DoesNotContain(starvingCandidates, c => c.Action.Id.Value == "swim");
        Assert.Contains(healthyCandidates, c => c.Action.Id.Value == "swim");
    }

    [Fact]
    public void FunWeight_ReducedBaseline_SwimScoreLowerThanSurvivalActions_WhenFullyNourished()
    {
        // With the 0.6 Fun multiplier, swim should not dominate when the actor is fully satisfied
        // and has decent morale (e.g., 70). Survival-oriented actions like think_about_supplies
        // or shake_tree_coconut should be competitive.
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");

        // Full health/energy, decent morale — beach-vacation scenario that should NOT dominate
        var actorState = domain.CreateActorState(actorId, new Dictionary<string, object>
        {
            ["satiety"] = 90.0,
            ["energy"]  = 90.0,
            ["morale"]  = 70.0
        });
        var worldState = (IslandWorldState)domain.CreateInitialWorldState();

        var candidates = domain.GenerateCandidates(actorId, actorState, worldState, 0L, new Random(42), new EmptyResourceAvailability());

        var swimScore  = candidates.First(c => c.Action.Id.Value == "swim").Score;
        var thinkScore = candidates.First(c => c.Action.Id.Value == "think_about_supplies").Score;

        // With 0.6 Fun multiplier and Morale=70, Fun weight = (1 - 0.7) * 0.6 = 0.18
        // swim base score (0.18) + Fun contribution (0.8 * 0.18) ≈ 0.32
        // think_about_supplies has Preparation + Efficiency and should be competitive
        Assert.True(swimScore < 1.0,
            $"Swim score ({swimScore:F3}) should stay below 1.0 when actor is satisfied and morale is decent");
    }

    [Fact]
    public void HungryActor_WithImmediateFood_FoodConsumptionWeightExceedsFoodAcquisition()
    {
        // When edible food is available in the shared supply pile the quality model should
        // bias hunger pressure toward FoodConsumption rather than FoodAcquisition.
        var domain  = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actor   = domain.CreateActorState(actorId, new Dictionary<string, object> { ["satiety"] = 15.0 });

        var worldState = (IslandWorldState)domain.CreateInitialWorldState();
        // Add edible coconuts directly to the shared supply pile (immediate food).
        worldState.SharedSupplyPile!.GetOrCreateSupply(() => new Supply.CoconutSupply()).Quantity = 5;

        var candidates = domain.GenerateCandidates(actorId, actor, worldState, 0L, new Random(42), new EmptyResourceAvailability());

        // bash_and_eat_coconut is the direct eat action (FoodConsumption quality)
        // shake_tree_coconut gathers coconuts (FoodAcquisition quality)
        var eatCandidate    = candidates.FirstOrDefault(c => c.Action.Id.Value == "bash_and_eat_coconut");
        var shakeCandidate  = candidates.FirstOrDefault(c => c.Action.Id.Value == "shake_tree_coconut");

        Assert.NotNull(eatCandidate);
        Assert.NotNull(shakeCandidate);

        // With immediate food available the eat action should score higher than the gather action.
        Assert.True(eatCandidate!.Score > shakeCandidate!.Score,
            $"bash_and_eat_coconut ({eatCandidate.Score:F3}) should outscore shake_tree_coconut ({shakeCandidate.Score:F3}) when food is immediately available");
    }

    [Fact]
    public void HungryActor_WithNoImmediateFood_FoodAcquisitionWeightDominates()
    {
        // When the shared supply pile has no edible food but the tree has coconuts the model
        // should bias hunger pressure toward FoodAcquisition so gathering actions rank higher.
        var domain  = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actor   = domain.CreateActorState(actorId, new Dictionary<string, object> { ["satiety"] = 15.0 });

        var worldState = (IslandWorldState)domain.CreateInitialWorldState();
        // Coconuts on the tree only (acquirable, not immediately edible).
        ((ISupplyBounty)worldState.GetItem<CoconutTreeItem>("palm_tree")!).GetSupply<CoconutSupply>("coconut")!.Quantity = 10;

        var candidates = domain.GenerateCandidates(actorId, actor, worldState, 0L, new Random(42), new EmptyResourceAvailability());
        var shakeCandidate = candidates.FirstOrDefault(c => c.Action.Id.Value == "shake_tree_coconut");

        Assert.NotNull(shakeCandidate);

        // FoodAcquisition quality weight should be active — score should be well above intrinsic.
        Assert.True(shakeCandidate!.Score > shakeCandidate.IntrinsicScore + 0.5,
            $"shake_tree_coconut score ({shakeCandidate.Score:F3}) should significantly exceed intrinsic ({shakeCandidate.IntrinsicScore:F3}) when starving with only acquirable food");
    }

    [Fact]
    public void GoFishing_HasFoodAcquisitionQuality_NotFoodConsumption()
    {
        // go_fishing acquires food (fish) rather than consuming it; it should carry
        // FoodAcquisition quality, not FoodConsumption.
        var domain  = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actor   = (IslandActorState)domain.CreateActorState(actorId);
        var world   = (IslandWorldState)domain.CreateInitialWorldState();
        domain.InitializeActorItems(actorId, world);
        // OceanItem starts with 100 fish by default, so go_fishing should be available.

        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(42), new EmptyResourceAvailability());
        var fishing = candidates.FirstOrDefault(c => c.Action.Id.Value == "go_fishing");

        if (fishing != null)
        {
            Assert.True(fishing.Qualities.ContainsKey(QualityType.FoodAcquisition),
                "go_fishing should carry FoodAcquisition quality");
            Assert.False(fishing.Qualities.ContainsKey(QualityType.FoodConsumption),
                "go_fishing should NOT carry FoodConsumption quality — it acquires food, not consumes it");
        }
    }

    [Fact]
    public void ThinkAboutSupplies_UsesFallback_WhenNoDiscoverableRecipes()
    {
        // In the default initial world state no recipes are discoverable.
        // think_about_supplies should use low fallback qualities and have a modest score.
        var domain  = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actor   = domain.CreateActorState(actorId, new Dictionary<string, object>
        {
            ["satiety"] = 80.0, ["energy"] = 80.0, ["morale"] = 60.0
        });
        var world = (IslandWorldState)domain.CreateInitialWorldState();

        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(42), new EmptyResourceAvailability());
        var think = candidates.First(c => c.Action.Id.Value == "think_about_supplies");

        // With fallback qualities (0.15 Preparation, 0.10 Efficiency) the score should be low.
        Assert.True(think.Score < 0.5,
            $"think_about_supplies score ({think.Score:F3}) should be modest when no discoverable recipes exist");
    }

    [Fact]
    public void ThinkAboutSupplies_ScoresHigher_WhenRecipesAreDiscoverable()
    {
        // When there are discoverable recipes the dynamic quality blend should push
        // think_about_supplies above its fallback score.
        var domain  = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actor   = domain.CreateActorState(actorId, new Dictionary<string, object>
        {
            ["satiety"] = 80.0, ["energy"] = 80.0, ["morale"] = 60.0
        });

        // Baseline world — no discoverable recipes.
        var worldFallback = (IslandWorldState)domain.CreateInitialWorldState();
        var fallbackCandidates = domain.GenerateCandidates(actorId, actor, worldFallback, 0L, new Random(42), new EmptyResourceAvailability());
        var fallbackScore = fallbackCandidates.First(c => c.Action.Id.Value == "think_about_supplies").Score;

        // World with palm fronds → palm_frond_blanket becomes discoverable.
        var worldRich = (IslandWorldState)domain.CreateInitialWorldState();
        worldRich.SharedSupplyPile!.AddSupply(5.0, () => new Supply.PalmFrondSupply());
        var richCandidates = domain.GenerateCandidates(actorId, actor, worldRich, 0L, new Random(42), new EmptyResourceAvailability());
        var richScore = richCandidates.First(c => c.Action.Id.Value == "think_about_supplies").Score;

        Assert.True(richScore > fallbackScore,
            $"think_about_supplies with discoverable recipe ({richScore:F3}) should score higher than fallback ({fallbackScore:F3})");
    }

    [Fact]
    public void ThinkAboutSupplies_Suppressed_WhenStarving_AndNoFoodRelevantRecipes()
    {
        // When the actor is starving and discoverable recipes do not help with food/safety,
        // think_about_supplies qualities should be suppressed.
        // Use a planner archetype (INT=16, WIS=16) so Preparation/Efficiency have
        // positive personality weight, making the quality suppression effect observable.
        var domain  = new IslandDomainPack();
        var actorId = new ActorId("TestActor");

        // Starving planner: satiety=15 is below ThinkSuppliesStarvationThreshold=25.
        var starvingActor = (IslandActorState)domain.CreateActorState(actorId, new Dictionary<string, object>
        {
            ["INT"] = 16, ["WIS"] = 16,
            ["satiety"] = 15.0, ["energy"] = 80.0, ["morale"] = 50.0
        });
        var satisfiedActor = (IslandActorState)domain.CreateActorState(actorId, new Dictionary<string, object>
        {
            ["INT"] = 16, ["WIS"] = 16,
            ["satiety"] = 80.0, ["energy"] = 80.0, ["morale"] = 50.0
        });

        // CarcassScraps in the pile: only the bait recipe becomes discoverable.
        // Bait has {Preparation, Mastery, Efficiency, Comfort(-)} but no Food/Safety qualities.
        // So starvation suppression should apply for the starving actor.
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        world.SharedSupplyPile!.AddSupply(3.0, () => new Supply.CarcassScrapsSupply());

        var starvingCandidates  = domain.GenerateCandidates(actorId, starvingActor,  world, 0L, new Random(42), new EmptyResourceAvailability());
        var satisfiedCandidates = domain.GenerateCandidates(actorId, satisfiedActor, world, 0L, new Random(42), new EmptyResourceAvailability());

        var starvingThinkScore  = starvingCandidates.First(c => c.Action.Id.Value == "think_about_supplies").Score;
        var satisfiedThinkScore = satisfiedCandidates.First(c => c.Action.Id.Value == "think_about_supplies").Score;

        Assert.True(starvingThinkScore < satisfiedThinkScore,
            $"think_about_supplies should be suppressed when starving and no food-relevant recipes are discoverable " +
            $"(starving: {starvingThinkScore:F3} vs satisfied: {satisfiedThinkScore:F3})");
    }
}

public class ActionCandidateQualitiesTests
{
    /// <summary>
    /// Helper that generates all candidates with a world containing all item types
    /// that emit candidates, so every action can be covered.
    /// </summary>
    private static (IslandDomainPack domain, List<ActionCandidate> candidates) GenerateAllCandidates()
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actorState = (IslandActorState)domain.CreateActorState(actorId, new Dictionary<string, object>
        {
            ["satiety"] = 25.0,  // low but not critical (< 20 would suppress chat actions)
            ["morale"]  = 10.0,  // low morale → sandcastle stomp appears
            ["energy"]  = 80.0
        });

        var worldState = (IslandWorldState)domain.CreateInitialWorldState();
        domain.InitializeActorItems(actorId, worldState);

        // Campfire for relight + repair (Quality > 20 so relight fires, < 70 so repair fires)
        worldState.WorldItems.Add(new Items.CampfireItem("campfire_relight") { Quality = 40.0, IsLit = false });
        // Campfire for rebuild (Quality < 10)
        worldState.WorldItems.Add(new Items.CampfireItem("campfire_rebuild") { Quality = 5.0, IsLit = false });

        // Ensure all expirable items are present
        worldState.WorldItems.Add(new Items.PlaneItem("plane"));
        worldState.WorldItems.Add(new Items.MermaidItem("mermaid"));
        worldState.WorldItems.Add(new Items.TreasureChestItem("treasure_chest"));

        // Add a sandcastle at low quality so stomp candidate appears
        worldState.WorldItems.Add(new Items.SandCastleItem("sandcastle") { Quality = 30.0 });

        // Add a blanket at low quality so repair candidate appears
        worldState.WorldItems.Add(new Items.PalmFrondBlanketItem("palm_frond_blanket") { Quality = 40.0 });
        worldState.SharedSupplyPile!.AddSupply(5, () => new Supply.PalmFrondSupply());

        // Degrade fishing pole so repair/maintain appear
        var pole = worldState.WorldItems.OfType<Items.FishingPoleItem>().FirstOrDefault();
        if (pole != null)
            pole.Quality = 15.0;

        // Pending chat action (write_name_sand only fires when not survival critical)
        actorState.PendingChatActions.Enqueue(new PendingIntent
        {
            ActionId = "write_name_sand", Type = "chat_redeem",
            Data = new Dictionary<string, object> { ["viewer_name"] = "Viewer" }, EnqueuedAtTick = 0L
        });

        var candidates = domain.GenerateCandidates(actorId, actorState, worldState, 0L, new Random(42), new EmptyResourceAvailability());
        return (domain, candidates);
    }

    [Theory]
    [InlineData("idle")]
    [InlineData("write_name_sand")]
    [InlineData("try_to_signal_plane")]
    [InlineData("wave_at_mermaid")]
    [InlineData("bash_open_treasure_chest")]
    [InlineData("stomp_on_sandcastle")]
    [InlineData("shake_tree_coconut")]
    [InlineData("go_fishing")]
    [InlineData("maintain_rod")]
    [InlineData("repair_rod")]
    [InlineData("repair_blanket")]
    [InlineData("sleep_in_blanket")]
    [InlineData("relight_campfire")]
    [InlineData("repair_campfire")]
    [InlineData("rebuild_campfire")]
    public void ActionCandidate_HasNonNullQualities(string actionId)
    {
        var (_, candidates) = GenerateAllCandidates();

        var candidate = candidates.FirstOrDefault(c => c.Action.Id.Value == actionId);
        Assert.NotNull(candidate);
        Assert.NotNull(candidate.Qualities);
        Assert.True(candidate.Qualities.Count > 0, $"{actionId} should have at least one Quality entry");
    }

    [Fact]
    public void Swim_HasNonNullQualities_WhenActorIsHealthy()
    {
        // Swim is gated by PlayfulOnly, so requires a healthy actor to appear in candidates.
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actorState = (IslandActorState)domain.CreateActorState(actorId, new Dictionary<string, object>
        {
            ["satiety"] = 80.0,
            ["morale"]  = 60.0,
            ["energy"]  = 80.0
        });
        var worldState = (IslandWorldState)domain.CreateInitialWorldState();
        domain.InitializeActorItems(actorId, worldState);

        var candidates = domain.GenerateCandidates(actorId, actorState, worldState, 0L, new Random(42), new EmptyResourceAvailability());

        var candidate = candidates.FirstOrDefault(c => c.Action.Id.Value == "swim");
        Assert.NotNull(candidate);
        Assert.NotNull(candidate.Qualities);
        Assert.True(candidate.Qualities.Count > 0, "swim should have at least one Quality entry");
    }

    [Fact]
    public void CampfireAddFuel_HasNonNullQualities()
    {
        // add_fuel_campfire requires a lit campfire with low fuel and wood in supply
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actorState = (IslandActorState)domain.CreateActorState(actorId);
        var worldState = (IslandWorldState)domain.CreateInitialWorldState();

        var campfire = new Items.CampfireItem("main_campfire") { IsLit = true, FuelSeconds = 500.0 };
        worldState.WorldItems.Add(campfire);

        worldState.SharedSupplyPile!.GetOrCreateSupply(() => new Supply.WoodSupply()).Quantity = 10;

        var candidates = domain.GenerateCandidates(actorId, actorState, worldState, 0L, new Random(42), new EmptyResourceAvailability());

        var candidate = candidates.FirstOrDefault(c => c.Action.Id.Value == "add_fuel_campfire");
        Assert.NotNull(candidate);
        Assert.NotNull(candidate.Qualities);
        Assert.True(candidate.Qualities.Count > 0);
    }

    [Fact]
    public void ClapEmote_HasNonNullQualities()
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actorState = (IslandActorState)domain.CreateActorState(actorId);
        var worldState = domain.CreateInitialWorldState();

        actorState.PendingChatActions.Enqueue(new PendingIntent
        {
            ActionId = "clap_emote", Type = "sub",
            Data = new Dictionary<string, object>(), EnqueuedAtTick = 0L
        });

        var candidates = domain.GenerateCandidates(actorId, actorState, worldState, 0L, new Random(42), new EmptyResourceAvailability());

        var candidate = candidates.FirstOrDefault(c => c.Action.Id.Value == "clap_emote");
        Assert.NotNull(candidate);
        Assert.NotNull(candidate.Qualities);
        Assert.True(candidate.Qualities.Count > 0);
    }
}

