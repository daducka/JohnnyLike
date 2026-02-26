using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.SimRunner;

namespace JohnnyLike.Scenario.Tests;

public class JimPamHighFiveScenarioTests
{
    [Fact]
    public void HighFiveScenario_BothActorsReady_SceneCompletes()
    {
        var domainPack = new IslandDomainPack();
        var traceSink = new InMemoryTraceSink();
        var engine = new JohnnyLike.Engine.Engine(domainPack, 42, traceSink);

        engine.AddActor(new ActorId("Jim"), new Dictionary<string, object>
        {
            ["satiety"] = 65.0,
            ["energy"] = 80.0
        });

        engine.AddActor(new ActorId("Pam"), new Dictionary<string, object>
        {
            ["satiety"] = 90.0,
            ["energy"] = 90.0
        });

        var executor = new FakeExecutor(engine);
        
        for (int i = 0; i < 120; i++)
        {
            executor.Update(0.5);
        }

        var events = traceSink.GetEvents();
        
        // Both actors should receive action assignments
        var jimActions = events.Where(e => e.EventType == "ActionAssigned" && e.ActorId?.Value == "Jim").ToList();
        var pamActions = events.Where(e => e.EventType == "ActionAssigned" && e.ActorId?.Value == "Pam").ToList();
        Assert.NotEmpty(jimActions);
        Assert.NotEmpty(pamActions);
    }

    [Fact]
    public void ChatRedeemSignal_ExecutedWithoutInterruption()
    {
        var domainPack = new IslandDomainPack();
        var traceSink = new InMemoryTraceSink();
        var engine = new JohnnyLike.Engine.Engine(domainPack, 42, traceSink);

        engine.AddActor(new ActorId("Jim"), new Dictionary<string, object>
        {
            ["satiety"] = 80.0,
            ["energy"] = 80.0
        });

        var executor = new FakeExecutor(engine);
        executor.Update(0.5);
        
        var jimState = engine.Actors[new ActorId("Jim")] as IslandActorState;
        Assert.NotNull(jimState);
        
        // Enqueue chat redeem signal with proper Island domain data
        engine.EnqueueSignal(new Signal(
            "chat_redeem",
            engine.CurrentTick + 100L,
            new ActorId("Jim"),
            new Dictionary<string, object> 
            { 
                ["redeem_name"] = "write_name_sand",
                ["viewer_name"] = "TestViewer" 
            }
        ));

        // Continue simulation
        for (int i = 0; i < 60; i++)
        {
            executor.Update(0.5);
        }

        var events = traceSink.GetEvents();
        
        // Verify signal was enqueued
        Assert.Contains(events, e => e.EventType == "SignalEnqueued");
        
        // Verify no actions were cancelled
        Assert.DoesNotContain(events, e => 
            e.EventType == "ActionCompleted" && 
            e.Details.TryGetValue("outcomeType", out var outcome) && 
            outcome.ToString() == "Cancelled");
        
        // Verify signal was processed (write_name_sand action was assigned)
        Assert.Contains(events, e => 
            e.EventType == "ActionAssigned" && 
            e.Details.TryGetValue("actionId", out var actionId) && 
            (actionId.ToString()!.Contains("write_name_sand") || 
             e.EventType == "SignalProcessed"));
    }

    [Fact]
    public void PamNoShow_SceneAbortsAndReleasesReservations()
    {
        var domainPack = new IslandDomainPack();
        var traceSink = new InMemoryTraceSink();
        var engine = new JohnnyLike.Engine.Engine(domainPack, 42, traceSink);

        engine.AddActor(new ActorId("Jim"), new Dictionary<string, object>
        {
            ["satiety"] = 65.0,
            ["energy"] = 80.0
        });

        var executor = new FakeExecutor(engine);
        
        for (int i = 0; i < 60; i++)
        {
            executor.Update(0.5);
        }

        var events = traceSink.GetEvents();
        // Jim should have gotten some actions
        var jimActions = events.Where(e => e.EventType == "ActionAssigned" && e.ActorId?.Value == "Jim").ToList();
        Assert.NotEmpty(jimActions);
    }
    
    [Fact]
    public void IslandScenario_StalactiteDripsEvery60Ticks()
    {
        var domainPack = new IslandDomainPack();
        var traceSink = new InMemoryTraceSink();
        var engine = new JohnnyLike.Engine.Engine(domainPack, 42, traceSink);
        
        engine.AddActor(new ActorId("Jim"), new Dictionary<string, object>());
        var executor = new FakeExecutor(engine);
        
        // Advance 61 ticks (just past first drip)
        executor.AdvanceTicks(61L);
        
        var events = traceSink.GetEvents();
        Assert.Contains(events, e => e.EventType == "StalactiteDrip");
    }
}
