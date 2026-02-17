using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Engine;

namespace JohnnyLike.Engine.Tests;

public class ReservationTableTests
{
    [Fact]
    public void TryReserve_NewResource_ReturnsTrue()
    {
        var table = new ReservationTable();
        var result = table.TryReserveForScene(
            new ResourceId("printer"),
            new SceneId("scene1"),
            ReservationOwner.FromActor(new ActorId("Jim")),
            100.0
        );

        Assert.True(result);
        Assert.True(table.IsReserved(new ResourceId("printer")));
    }

    [Fact]
    public void TryReserve_AlreadyReserved_ReturnsFalse()
    {
        var table = new ReservationTable();
        var resourceId = new ResourceId("printer");
        
        table.TryReserveForScene(resourceId, new SceneId("scene1"), ReservationOwner.FromActor(new ActorId("Jim")), 100.0);
        var result = table.TryReserveForScene(resourceId, new SceneId("scene2"), ReservationOwner.FromActor(new ActorId("Pam")), 100.0);

        Assert.False(result);
    }

    [Fact]
    public void Release_ExistingReservation_RemovesReservation()
    {
        var table = new ReservationTable();
        var resourceId = new ResourceId("printer");
        
        table.TryReserveForScene(resourceId, new SceneId("scene1"), ReservationOwner.FromActor(new ActorId("Jim")), 100.0);
        table.Release(resourceId);

        Assert.False(table.IsReserved(resourceId));
    }

    [Fact]
    public void ReleaseByScene_RemovesAllSceneReservations()
    {
        var table = new ReservationTable();
        var sceneId = new SceneId("scene1");
        var res1 = new ResourceId("printer");
        var res2 = new ResourceId("desk");

        table.TryReserveForScene(res1, sceneId, ReservationOwner.FromActor(new ActorId("Jim")), 100.0);
        table.TryReserveForScene(res2, sceneId, ReservationOwner.FromActor(new ActorId("Jim")), 100.0);

        table.ReleaseByScene(sceneId);

        Assert.False(table.IsReserved(res1));
        Assert.False(table.IsReserved(res2));
    }

    [Fact]
    public void CleanupExpired_RemovesExpiredReservations()
    {
        var table = new ReservationTable();
        var resourceId = new ResourceId("printer");
        
        table.TryReserveForScene(resourceId, new SceneId("scene1"), ReservationOwner.FromActor(new ActorId("Jim")), 50.0);
        table.CleanupExpired(100.0);

        Assert.False(table.IsReserved(resourceId));
    }

    [Fact]
    public void ReleaseByScene_WithNullActorId_RemovesReservations()
    {
        var table = new ReservationTable();
        var sceneId = new SceneId("scene1");
        var res1 = new ResourceId("printer");
        var res2 = new ResourceId("desk");

        table.TryReserveForScene(res1, sceneId, ReservationOwner.FromWorldItem("item1"), 100.0);
        table.TryReserveForScene(res2, sceneId, ReservationOwner.FromWorldItem("item2"), 100.0);

        table.ReleaseByScene(sceneId);

        Assert.False(table.IsReserved(res1));
        Assert.False(table.IsReserved(res2));
    }

    [Fact]
    public void ReleaseByScene_OnlyReleasesResourcesForSpecificScene()
    {
        var table = new ReservationTable();
        var scene1 = new SceneId("scene1");
        var scene2 = new SceneId("scene2");
        var res1 = new ResourceId("printer");
        var res2 = new ResourceId("desk");
        var res3 = new ResourceId("phone");

        table.TryReserveForScene(res1, scene1, ReservationOwner.FromActor(new ActorId("Jim")), 100.0);
        table.TryReserveForScene(res2, scene1, ReservationOwner.FromActor(new ActorId("Pam")), 100.0);
        table.TryReserveForScene(res3, scene2, ReservationOwner.FromActor(new ActorId("Dwight")), 100.0);

        table.ReleaseByScene(scene1);

        Assert.False(table.IsReserved(res1));
        Assert.False(table.IsReserved(res2));
        Assert.True(table.IsReserved(res3)); // This should remain reserved
    }

    [Fact]
    public void ReleaseByScene_WithMixedActorIds_RemovesAllSceneReservations()
    {
        var table = new ReservationTable();
        var sceneId = new SceneId("scene1");
        var res1 = new ResourceId("printer");
        var res2 = new ResourceId("desk");
        var res3 = new ResourceId("phone");

        table.TryReserveForScene(res1, sceneId, ReservationOwner.FromActor(new ActorId("Jim")), 100.0);
        table.TryReserveForScene(res2, sceneId, ReservationOwner.FromWorldItem("desk_item"), 100.0);
        table.TryReserveForScene(res3, sceneId, ReservationOwner.FromActor(new ActorId("Pam")), 100.0);

        table.ReleaseByScene(sceneId);

        Assert.False(table.IsReserved(res1));
        Assert.False(table.IsReserved(res2));
        Assert.False(table.IsReserved(res3));
    }
}

public class VarietyMemoryTests
{
    [Fact]
    public void GetRepetitionPenalty_NoHistory_ReturnsZero()
    {
        var memory = new VarietyMemory();
        var penalty = memory.GetRepetitionPenalty("Jim", "eat_snack", 10.0);

        Assert.Equal(0.0, penalty);
    }

    [Fact]
    public void GetRepetitionPenalty_AfterRecording_ReturnsPenalty()
    {
        var memory = new VarietyMemory();
        
        memory.RecordAction("Jim", "eat_snack", 10.0);
        memory.RecordAction("Jim", "eat_snack", 20.0);
        
        var penalty = memory.GetRepetitionPenalty("Jim", "eat_snack", 30.0);

        Assert.True(penalty > 0.0);
    }

    [Fact]
    public void Cleanup_RemovesOldEntries()
    {
        var memory = new VarietyMemory(memoryWindowSeconds: 60.0);
        
        memory.RecordAction("Jim", "eat_snack", 10.0);
        memory.Cleanup(200.0);
        
        var penalty = memory.GetRepetitionPenalty("Jim", "eat_snack", 200.0);

        Assert.Equal(0.0, penalty);
    }
}

public class DeterminismTests
{
    [Fact]
    public void SameSeed_ProducesSameTrace()
    {
        var hash1 = RunSimulation(42);
        var hash2 = RunSimulation(42);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void DifferentSeed_ProducesDifferentTrace()
    {
        var hash1 = RunSimulation(42);
        var hash2 = RunSimulation(43);

        Assert.NotEqual(hash1, hash2);
    }

    private string RunSimulation(int seed)
    {
        var domainPack = new Domain.Office.OfficeDomainPack();
        var traceSink = new InMemoryTraceSink();
        var engine = new JohnnyLike.Engine.Engine(domainPack, seed, traceSink);

        engine.AddActor(new ActorId("Jim"), new Dictionary<string, object>
        {
            ["hunger"] = 20.0,
            ["energy"] = 80.0
        });

        engine.AddActor(new ActorId("Pam"), new Dictionary<string, object>
        {
            ["hunger"] = 40.0,
            ["energy"] = 90.0
        });

        var executor = new SimRunner.FakeExecutor(engine);
        var timeStep = 0.5;
        var elapsed = 0.0;

        while (elapsed < 20.0)
        {
            executor.Update(timeStep);
            elapsed += timeStep;
        }

        return TraceHelper.ComputeTraceHash(traceSink.GetEvents());
    }
}

public class SignalHandlingTests
{
    [Fact]
    public void ProcessSignal_DoesNotUseReflection()
    {
        // This test verifies that Engine.ProcessSignal does not use reflection
        // by checking the Engine source code doesn't contain reflection APIs in ProcessSignal method.
        // 
        // Note: This is a simple string-based check suitable for this codebase.
        // For a production system, consider using Roslyn static analysis for more robust verification.
        var engineSourcePath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "..",
            "src", "JohnnyLike.Engine", "Engine.cs"
        );

        var engineSource = File.ReadAllText(engineSourcePath);
        
        // Find ProcessSignal method
        var processSignalStart = engineSource.IndexOf("private void ProcessSignal(Signal signal)");
        Assert.True(processSignalStart > 0, "ProcessSignal method should exist");
        
        // Find the next method or end of class (simple approach for this codebase)
        // This assumes methods are separated by blank lines followed by access modifiers or closing braces
        var searchStart = processSignalStart + 100; // Skip method signature
        var nextMethod = engineSource.IndexOf("\n    public ", searchStart);
        var nextPrivate = engineSource.IndexOf("\n    private ", searchStart);
        var endOfMethod = Math.Min(
            nextMethod > 0 ? nextMethod : engineSource.Length,
            nextPrivate > 0 ? nextPrivate : engineSource.Length
        );
        
        var processSignalCode = engineSource.Substring(processSignalStart, endOfMethod - processSignalStart);
        
        // Verify no reflection APIs are used in ProcessSignal
        Assert.DoesNotContain("GetType()", processSignalCode);
        Assert.DoesNotContain("GetProperty", processSignalCode);
        Assert.DoesNotContain("SetValue", processSignalCode);
        Assert.DoesNotContain("GetMethod", processSignalCode);
        Assert.DoesNotContain(".Invoke(", processSignalCode);
        Assert.DoesNotContain("Reflection", processSignalCode);
    }

    [Fact]
    public void ProcessSignal_CallsDomainPackOnSignal()
    {
        var domainPack = new TestDomainPack();
        var engine = new JohnnyLike.Engine.Engine(domainPack, 42);
        
        engine.AddActor(new ActorId("TestActor"));
        
        var signal = new Signal(
            "test_signal",
            0.0,
            new ActorId("TestActor"),
            new Dictionary<string, object> { ["data"] = "test" }
        );
        
        engine.EnqueueSignal(signal);
        engine.AdvanceTime(1.0);
        
        Assert.True(domainPack.OnSignalCalled);
        Assert.Equal("test_signal", domainPack.LastSignalType);
        Assert.NotNull(domainPack.LastTargetActor);
    }

    private class TestDomainPack : IDomainPack
    {
        public string DomainName => "Test";
        public bool OnSignalCalled { get; private set; }
        public string? LastSignalType { get; private set; }
        public ActorState? LastTargetActor { get; private set; }

        public WorldState CreateInitialWorldState() => new TestWorldState();
        
        public ActorState CreateActorState(ActorId actorId, Dictionary<string, object>? initialData = null)
        {
            return new TestActorState { Id = actorId };
        }

        public List<ActionCandidate> GenerateCandidates(ActorId actorId, ActorState actorState, WorldState worldState, double currentTime, Random rng, IResourceAvailability resourceAvailability)
        {
            return new List<ActionCandidate>
            {
                new ActionCandidate(
                    new ActionSpec(new ActionId("idle"), ActionKind.Wait, EmptyActionParameters.Instance, 1.0),
                    1.0
                )
            };
        }

        public void ApplyActionEffects(ActorId actorId, ActionOutcome outcome, ActorState actorState, WorldState worldState, IRngStream rng)
        {
        }

        public void OnSignal(Signal signal, ActorState? targetActor, WorldState worldState, double currentTime)
        {
            OnSignalCalled = true;
            LastSignalType = signal.Type;
            LastTargetActor = targetActor;
        }

        public List<SceneTemplate> GetSceneTemplates() => new List<SceneTemplate>();
        
        public bool ValidateContent(out List<string> errors)
        {
            errors = new List<string>();
            return true;
        }

        public Dictionary<string, object> GetActorStateSnapshot(ActorState actorState)
        {
            return new Dictionary<string, object>();
        }
    }

    private class TestActorState : ActorState
    {
        public override string Serialize() => "{}";
        public override void Deserialize(string json) { }
    }

    private class TestWorldState : WorldState
    {
        public override string Serialize() => "{}";
        public override void Deserialize(string json) { }
    }
}

public class ActorStateSnapshotTests
{
    [Fact]
    public void ActionCompleted_IncludesActorStateSnapshot()
    {
        // Arrange
        var domainPack = new TestDomainPackWithSnapshot();
        var traceSink = new InMemoryTraceSink();
        var engine = new JohnnyLike.Engine.Engine(domainPack, 42, traceSink);
        
        engine.AddActor(new ActorId("TestActor"));
        
        // Act - Get and complete an action
        var success = engine.TryGetNextAction(new ActorId("TestActor"), out var action);
        Assert.True(success);
        Assert.NotNull(action);
        
        engine.ReportActionComplete(
            new ActorId("TestActor"),
            new ActionOutcome(action!.Id, ActionOutcomeType.Success, 1.0, null)
        );
        
        // Assert - Check that the trace event contains actor state snapshot
        var events = traceSink.GetEvents();
        var actionCompletedEvent = events.FirstOrDefault(e => e.EventType == "ActionCompleted");
        
        Assert.NotNull(actionCompletedEvent);
        Assert.True(actionCompletedEvent.Details.ContainsKey("actor_testValue"));
        Assert.Equal(42, actionCompletedEvent.Details["actor_testValue"]);
    }

    private class TestDomainPackWithSnapshot : IDomainPack
    {
        public string DomainName => "Test";

        public WorldState CreateInitialWorldState() => new TestWorldState();
        
        public ActorState CreateActorState(ActorId actorId, Dictionary<string, object>? initialData = null)
        {
            return new TestActorState { Id = actorId };
        }

        public List<ActionCandidate> GenerateCandidates(ActorId actorId, ActorState actorState, WorldState worldState, double currentTime, Random rng, IResourceAvailability resourceAvailability)
        {
            return new List<ActionCandidate>
            {
                new ActionCandidate(
                    new ActionSpec(new ActionId("test_action"), ActionKind.Wait, EmptyActionParameters.Instance, 1.0),
                    1.0
                )
            };
        }

        public void ApplyActionEffects(ActorId actorId, ActionOutcome outcome, ActorState actorState, WorldState worldState, IRngStream rng)
        {
        }

        public void OnSignal(Signal signal, ActorState? targetActor, WorldState worldState, double currentTime)
        {
        }

        public List<SceneTemplate> GetSceneTemplates() => new List<SceneTemplate>();
        
        public bool ValidateContent(out List<string> errors)
        {
            errors = new List<string>();
            return true;
        }

        public Dictionary<string, object> GetActorStateSnapshot(ActorState actorState)
        {
            return new Dictionary<string, object>
            {
                ["testValue"] = 42
            };
        }
    }

    private class TestActorState : ActorState
    {
        public override string Serialize() => "{}";
        public override void Deserialize(string json) { }
    }

    private class TestWorldState : WorldState
    {
        public override string Serialize() => "{}";
        public override void Deserialize(string json) { }
    }
}

public class EventMemoryTests
{
    // Basic functionality tests
    
    [Fact]
    public void RecordEvent_SingleEvent_StoresCorrectly()
    {
        var memory = new EventMemory();
        
        memory.RecordEvent("planeSighting", 100.0);
        
        Assert.Equal(100.0, memory.GetLastEventTime("planeSighting"));
    }

    [Fact]
    public void GetLastEventTime_ExistingEvent_ReturnsCorrectTime()
    {
        var memory = new EventMemory();
        memory.RecordEvent("mermaidEncounter", 42.5);
        
        var result = memory.GetLastEventTime("mermaidEncounter");
        
        Assert.Equal(42.5, result);
    }

    [Fact]
    public void GetLastEventTime_NonExistentEvent_ReturnsNegativeInfinity()
    {
        var memory = new EventMemory();
        
        var result = memory.GetLastEventTime("neverHappened");
        
        Assert.True(double.IsNegativeInfinity(result));
    }

    [Fact]
    public void RecordEvent_UpdatesExistingEvent_OverwritesPreviousTime()
    {
        var memory = new EventMemory();
        memory.RecordEvent("event1", 100.0);
        
        memory.RecordEvent("event1", 200.0);
        
        Assert.Equal(200.0, memory.GetLastEventTime("event1"));
    }

    // Time calculation tests
    
    [Fact]
    public void GetTimeSince_ExistingEvent_ReturnsCorrectElapsedTime()
    {
        var memory = new EventMemory();
        memory.RecordEvent("event1", 50.0);
        
        var timeSince = memory.GetTimeSince("event1", 150.0);
        
        Assert.Equal(100.0, timeSince);
    }

    [Fact]
    public void GetTimeSince_NonExistentEvent_ReturnsPositiveInfinity()
    {
        var memory = new EventMemory();
        
        var timeSince = memory.GetTimeSince("neverHappened", 100.0);
        
        Assert.True(double.IsPositiveInfinity(timeSince));
    }

    // Query tests
    
    [Fact]
    public void HasEventOccurred_ExistingEvent_ReturnsTrue()
    {
        var memory = new EventMemory();
        memory.RecordEvent("event1", 100.0);
        
        var result = memory.HasEventOccurred("event1");
        
        Assert.True(result);
    }

    [Fact]
    public void HasEventOccurred_NonExistentEvent_ReturnsFalse()
    {
        var memory = new EventMemory();
        
        var result = memory.HasEventOccurred("neverHappened");
        
        Assert.False(result);
    }

    // Serialization tests
    
    [Fact]
    public void GetAllEvents_MultipleEvents_ReturnsAllStoredEvents()
    {
        var memory = new EventMemory();
        memory.RecordEvent("event1", 100.0);
        memory.RecordEvent("event2", 200.0);
        memory.RecordEvent("event3", 300.0);
        
        var allEvents = memory.GetAllEvents();
        
        Assert.Equal(3, allEvents.Count);
        Assert.Equal(100.0, allEvents["event1"]);
        Assert.Equal(200.0, allEvents["event2"]);
        Assert.Equal(300.0, allEvents["event3"]);
    }

    [Fact]
    public void RestoreEvents_FromDictionary_RestoresAllEvents()
    {
        var memory = new EventMemory();
        var events = new Dictionary<string, double>
        {
            ["event1"] = 10.0,
            ["event2"] = 20.0,
            ["event3"] = 30.0
        };
        
        memory.RestoreEvents(events);
        
        Assert.Equal(10.0, memory.GetLastEventTime("event1"));
        Assert.Equal(20.0, memory.GetLastEventTime("event2"));
        Assert.Equal(30.0, memory.GetLastEventTime("event3"));
    }

    [Fact]
    public void RestoreEvents_ClearsPreviousEvents_BeforeRestoring()
    {
        var memory = new EventMemory();
        memory.RecordEvent("oldEvent", 999.0);
        
        var newEvents = new Dictionary<string, double>
        {
            ["newEvent"] = 100.0
        };
        memory.RestoreEvents(newEvents);
        
        Assert.False(memory.HasEventOccurred("oldEvent"));
        Assert.True(memory.HasEventOccurred("newEvent"));
        Assert.Equal(100.0, memory.GetLastEventTime("newEvent"));
    }

    // Edge case tests
    
    [Fact]
    public void RecordEvent_MultipleEvents_AllStoreCorrectly()
    {
        var memory = new EventMemory();
        
        memory.RecordEvent("event1", 10.0);
        memory.RecordEvent("event2", 20.0);
        memory.RecordEvent("event3", 30.0);
        
        Assert.Equal(10.0, memory.GetLastEventTime("event1"));
        Assert.Equal(20.0, memory.GetLastEventTime("event2"));
        Assert.Equal(30.0, memory.GetLastEventTime("event3"));
    }

    [Fact]
    public void GetAllEvents_ReturnsCopy_DoesNotExposeInternalState()
    {
        var memory = new EventMemory();
        memory.RecordEvent("event1", 100.0);
        
        var allEvents1 = memory.GetAllEvents();
        allEvents1["event2"] = 200.0;  // Modify the returned dictionary
        
        var allEvents2 = memory.GetAllEvents();
        
        Assert.Single(allEvents2);  // Should still have only one event
        Assert.False(allEvents2.ContainsKey("event2"));  // Should not contain the added event
    }
}
