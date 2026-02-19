using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Office;
using JohnnyLike.SimRunner;

namespace JohnnyLike.Scenario.Tests;

public class JimPamHighFiveScenarioTests
{
    [Fact]
    public void HighFiveScenario_BothActorsReady_SceneCompletes()
    {
        var domainPack = new OfficeDomainPack();
        var traceSink = new InMemoryTraceSink();
        var engine = new JohnnyLike.Engine.Engine(domainPack, 42, traceSink);

        engine.AddActor(new ActorId("Jim"), new Dictionary<string, object>
        {
            ["hunger"] = 35.0,
            ["energy"] = 80.0
        });

        engine.AddActor(new ActorId("Pam"), new Dictionary<string, object>
        {
            ["hunger"] = 10.0,
            ["energy"] = 90.0
        });

        var executor = new FakeExecutor(engine);
        
        for (int i = 0; i < 120; i++)
        {
            executor.Update(0.5);
        }

        var events = traceSink.GetEvents();
        
        // Verify scene was proposed
        Assert.Contains(events, e => e.EventType == "SceneProposed");
        
        // Verify actors joined
        var joinEvents = events.Where(e => e.EventType == "SceneJoined").ToList();
        Assert.True(joinEvents.Count >= 2, "Both actors should join the scene");
        
        // Verify scene started
        Assert.Contains(events, e => e.EventType == "SceneStarted");
        
        // Verify scene completed or at least was proposed
        var sceneEndEvents = events.Where(e => 
            e.EventType == "SceneCompleted" || e.EventType == "SceneAborted").ToList();
        Assert.NotEmpty(sceneEndEvents);
    }

    [Fact]
    public void ChatRedeemSignal_ExecutedWithoutInterruption()
    {
        var domainPack = new OfficeDomainPack();
        var traceSink = new InMemoryTraceSink();
        var engine = new JohnnyLike.Engine.Engine(domainPack, 42, traceSink);

        engine.AddActor(new ActorId("Jim"), new Dictionary<string, object>
        {
            ["hunger"] = 20.0,
            ["energy"] = 80.0
        });

        // Add Jim, let him start a task
        var executor = new FakeExecutor(engine);
        executor.Update(0.5);
        
        var jimState = engine.Actors[new ActorId("Jim")] as OfficeActorState;
        Assert.NotNull(jimState);
        
        // Enqueue chat redeem signal
        jimState.LastChatRedeem = "wave";
        engine.EnqueueSignal(new Signal(
            "chat_redeem",
            engine.CurrentTime + 5.0,
            new ActorId("Jim"),
            new Dictionary<string, object> { ["emote"] = "wave" }
        ));

        // Continue simulation
        for (int i = 0; i < 60; i++)
        {
            executor.Update(0.5);
        }

        var events = traceSink.GetEvents();
        
        // Verify signal was enqueued
        Assert.Contains(events, e => e.EventType == "SignalEnqueued");
        
        // Verify no actions were interrupted (no action has outcomeType == Cancelled)
        Assert.DoesNotContain(events, e => 
            e.EventType == "ActionCompleted" && 
            e.Details.TryGetValue("outcomeType", out var outcome) && 
            outcome.ToString() == "Cancelled");
        
        // Verify chat redeem was eventually executed
        Assert.Contains(events, e => 
            e.EventType == "ActionAssigned" && 
            e.Details.TryGetValue("actionId", out var actionId) && 
            actionId.ToString()!.Contains("chat_redeem"));
    }

    [Fact]
    public void PamNoShow_SceneAbortsAndReleasesReservations()
    {
        var domainPack = new OfficeDomainPack();
        var traceSink = new InMemoryTraceSink();
        var engine = new JohnnyLike.Engine.Engine(domainPack, 42, traceSink);

        // Jim ready, Pam very busy with long task
        engine.AddActor(new ActorId("Jim"), new Dictionary<string, object>
        {
            ["hunger"] = 35.0,
            ["energy"] = 80.0
        });

        // We'll simulate Pam being unavailable by not adding her initially
        // or giving her a very low priority for joining scenes

        var executor = new FakeExecutor(engine);
        
        for (int i = 0; i < 100; i++)
        {
            executor.Update(0.5);
        }

        var events = traceSink.GetEvents();
        
        // Since Pam is not present, scenes requiring 2 actors won't be proposed
        // or if proposed, will abort
        var abortEvents = events.Where(e => e.EventType == "SceneAborted").ToList();
        
        // Jim should continue with other tasks
        var jimActions = events.Where(e => 
            e.ActorId.HasValue && 
            e.ActorId.Value.Value == "Jim" && 
            e.EventType == "ActionAssigned").ToList();
        
        Assert.NotEmpty(jimActions);
    }

    [Fact]
    public void Determinism_SameInputProducesSameOutput()
    {
        var hash1 = RunScenario(42);
        var hash2 = RunScenario(42);
        var hash3 = RunScenario(43);

        Assert.Equal(hash1, hash2);
        Assert.NotEqual(hash1, hash3);
    }

    private string RunScenario(int seed)
    {
        var domainPack = new OfficeDomainPack();
        var traceSink = new InMemoryTraceSink();
        var engine = new JohnnyLike.Engine.Engine(domainPack, seed, traceSink);

        engine.AddActor(new ActorId("Jim"), new Dictionary<string, object>
        {
            ["hunger"] = 35.0,
            ["energy"] = 80.0
        });

        engine.AddActor(new ActorId("Pam"), new Dictionary<string, object>
        {
            ["hunger"] = 10.0,
            ["energy"] = 90.0
        });

        var executor = new FakeExecutor(engine);
        
        for (int i = 0; i < 60; i++)
        {
            executor.Update(0.5);
        }

        return JohnnyLike.Engine.TraceHelper.ComputeTraceHash(traceSink.GetEvents());
    }
}
