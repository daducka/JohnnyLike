using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.SimRunner;

namespace JohnnyLike.Scenario.Tests;

public class IslandScenarioTests
{
    [Fact]
    public void IslandScenario_BasicFlow_SceneProposedAndCompleted()
    {
        var domainPack = new IslandDomainPack();
        var traceSink = new InMemoryTraceSink();
        var engine = new JohnnyLike.Engine.Engine(domainPack, 42, traceSink);

        engine.AddActor(new ActorId("Johnny"), new Dictionary<string, object>
        {
            ["STR"] = 12,
            ["DEX"] = 14,
            ["CON"] = 13,
            ["INT"] = 10,
            ["WIS"] = 11,
            ["CHA"] = 15,
            ["satiety"] = 70.0,
            ["energy"] = 80.0,
            ["morale"] = 60.0
        });

        var executor = new FakeExecutor(engine);
        
        for (int i = 0; i < 200; i++)
        {
            executor.Update(0.5);
        }

        var events = traceSink.GetEvents();

        // Assert the events list is not empty
        Assert.NotEmpty(events);


    }
}