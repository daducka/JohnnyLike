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