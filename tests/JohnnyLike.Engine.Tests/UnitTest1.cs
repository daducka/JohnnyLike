using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Engine;

namespace JohnnyLike.Engine.Tests;

public class ReservationTableTests
{
    [Fact]
    public void TryReserve_NewResource_ReturnsTrue()
    {
        var table = new ReservationTable();
        var result = table.TryReserve(
            new ResourceId("printer"),
            new ActorId("Jim"),
            new SceneId("scene1"),
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
        
        table.TryReserve(resourceId, new ActorId("Jim"), new SceneId("scene1"), 100.0);
        var result = table.TryReserve(resourceId, new ActorId("Pam"), new SceneId("scene2"), 100.0);

        Assert.False(result);
    }

    [Fact]
    public void Release_ExistingReservation_RemovesReservation()
    {
        var table = new ReservationTable();
        var resourceId = new ResourceId("printer");
        
        table.TryReserve(resourceId, new ActorId("Jim"), new SceneId("scene1"), 100.0);
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

        table.TryReserve(res1, new ActorId("Jim"), sceneId, 100.0);
        table.TryReserve(res2, new ActorId("Jim"), sceneId, 100.0);

        table.ReleaseByScene(sceneId);

        Assert.False(table.IsReserved(res1));
        Assert.False(table.IsReserved(res2));
    }

    [Fact]
    public void CleanupExpired_RemovesExpiredReservations()
    {
        var table = new ReservationTable();
        var resourceId = new ResourceId("printer");
        
        table.TryReserve(resourceId, new ActorId("Jim"), new SceneId("scene1"), 50.0);
        table.CleanupExpired(100.0);

        Assert.False(table.IsReserved(resourceId));
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
        // by checking the Engine source code doesn't contain reflection APIs
        var engineSourcePath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "..",
            "src", "JohnnyLike.Engine", "Engine.cs"
        );

        var engineSource = File.ReadAllText(engineSourcePath);
        
        // Check that ProcessSignal method doesn't use reflection
        var processSignalStart = engineSource.IndexOf("private void ProcessSignal(Signal signal)");
        Assert.True(processSignalStart > 0, "ProcessSignal method should exist");
        
        var nextMethodStart = engineSource.IndexOf("}", processSignalStart);
        var processSignalCode = engineSource.Substring(processSignalStart, nextMethodStart - processSignalStart);
        
        // Verify no reflection APIs are used
        Assert.DoesNotContain("GetType()", processSignalCode);
        Assert.DoesNotContain("GetProperty(", processSignalCode);
        Assert.DoesNotContain("SetValue(", processSignalCode);
        Assert.DoesNotContain("GetMethod(", processSignalCode);
        Assert.DoesNotContain("Invoke(", processSignalCode);
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

        public List<ActionCandidate> GenerateCandidates(ActorId actorId, ActorState actorState, WorldState worldState, double currentTime, Random rng)
        {
            return new List<ActionCandidate>
            {
                new ActionCandidate(
                    new ActionSpec(new ActionId("idle"), ActionKind.Wait, new Dictionary<string, object>(), 1.0),
                    1.0
                )
            };
        }

        public void ApplyActionEffects(ActorId actorId, ActionOutcome outcome, ActorState actorState, WorldState worldState)
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
