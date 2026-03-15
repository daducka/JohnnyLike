using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Island.Supply;

namespace JohnnyLike.Domain.Island.Tests;

/// <summary>
/// Tests for <see cref="IslandDomainPack.BuildPeriodicSnapshot"/>.
/// </summary>
public class PeriodicSnapshotTests
{
    private static IslandDomainPack CreateDomain() => new();

    private static IslandWorldState CreateWorld(IslandDomainPack domain)
        => (IslandWorldState)domain.CreateInitialWorldState();

    [Fact]
    public void BuildPeriodicSnapshot_EmptyActors_ReturnsWorldAndSupplyAndItemEvents()
    {
        var domain = CreateDomain();
        var world = CreateWorld(domain);
        var actors = new Dictionary<ActorId, ActorState>();

        var events = domain.BuildPeriodicSnapshot(world, actors, 1200L);

        Assert.Contains(events, e => e.EventType == "PeriodicWorldSnapshot");
        Assert.Contains(events, e => e.EventType == "PeriodicSupplySnapshot");
        // Each non-supply world item should produce a PeriodicWorldItemSnapshot
        Assert.Contains(events, e => e.EventType == "PeriodicWorldItemSnapshot");
    }

    [Fact]
    public void BuildPeriodicSnapshot_WorldSnapshot_ContainsExpectedFields()
    {
        var domain = CreateDomain();
        var world = CreateWorld(domain);
        var actors = new Dictionary<ActorId, ActorState>();

        var events = domain.BuildPeriodicSnapshot(world, actors, 1200L);

        var worldEvt = events.Single(e => e.EventType == "PeriodicWorldSnapshot");
        Assert.True(worldEvt.Details.ContainsKey("day"));
        Assert.True(worldEvt.Details.ContainsKey("hour"));
        Assert.True(worldEvt.Details.ContainsKey("dayPhase"));
        Assert.True(worldEvt.Details.ContainsKey("temperature"));
        Assert.True(worldEvt.Details.ContainsKey("precipitation"));
        Assert.True(worldEvt.Details.ContainsKey("tide"));
        Assert.True(worldEvt.Details.ContainsKey("fishAvailable"));
        Assert.Null(worldEvt.ActorId);
    }

    [Fact]
    public void BuildPeriodicSnapshot_SupplySnapshot_ContainsExpectedFields()
    {
        var domain = CreateDomain();
        var world = CreateWorld(domain);
        var actors = new Dictionary<ActorId, ActorState>();

        var events = domain.BuildPeriodicSnapshot(world, actors, 1200L);

        var supplyEvt = events.Single(e => e.EventType == "PeriodicSupplySnapshot");
        Assert.True(supplyEvt.Details.ContainsKey("pileId"));
        Assert.True(supplyEvt.Details.ContainsKey("access"));
        Assert.True(supplyEvt.Details.ContainsKey("supplies"));
        Assert.Equal("shared_supplies", supplyEvt.Details["pileId"]);
        Assert.Null(supplyEvt.ActorId);
    }

    [Fact]
    public void BuildPeriodicSnapshot_WorldItemSnapshot_ContainsExpectedFields()
    {
        var domain = CreateDomain();
        var world = CreateWorld(domain);
        var actors = new Dictionary<ActorId, ActorState>();

        var events = domain.BuildPeriodicSnapshot(world, actors, 1200L);

        var itemEvts = events.Where(e => e.EventType == "PeriodicWorldItemSnapshot").ToList();
        Assert.NotEmpty(itemEvts);
        foreach (var evt in itemEvts)
        {
            Assert.True(evt.Details.ContainsKey("itemId"));
            Assert.True(evt.Details.ContainsKey("itemType"));
            Assert.True(evt.Details.ContainsKey("room"));
            Assert.Null(evt.ActorId);
        }
    }

    [Fact]
    public void BuildPeriodicSnapshot_WithActor_EmitsActorAndRecipeEvents()
    {
        var domain = CreateDomain();
        var world = CreateWorld(domain);
        var actorId = new ActorId("Alice");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        actor.Satiety = 75.0;
        actor.Energy = 88.5;
        actor.Morale = 40.0;
        actor.Health = 95.0;
        var actors = new Dictionary<ActorId, ActorState> { [actorId] = actor };

        var events = domain.BuildPeriodicSnapshot(world, actors, 600L);

        var actorEvt = events.Single(e => e.EventType == "PeriodicActorSnapshot");
        Assert.Equal(actorId, actorEvt.ActorId);
        Assert.Equal(75.0, actorEvt.Details["satiety"]);
        Assert.Equal(88.5, actorEvt.Details["energy"]);
        Assert.Equal(40.0, actorEvt.Details["morale"]);
        Assert.Equal(95.0, actorEvt.Details["health"]);
        Assert.True(actorEvt.Details.ContainsKey("status"));
        Assert.True(actorEvt.Details.ContainsKey("currentAction"));
        Assert.True(actorEvt.Details.ContainsKey("room"));
        Assert.True(actorEvt.Details.ContainsKey("decisionPragmatism"));

        var recipeEvt = events.Single(e => e.EventType == "PeriodicRecipeSnapshot");
        Assert.Equal(actorId, recipeEvt.ActorId);
        Assert.True(recipeEvt.Details.ContainsKey("knownRecipes"));
    }

    [Fact]
    public void BuildPeriodicSnapshot_WithKnownRecipes_ListsThemSorted()
    {
        var domain = CreateDomain();
        var world = CreateWorld(domain);
        var actorId = new ActorId("Bob");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        actor.KnownRecipeIds.Add("rope");
        actor.KnownRecipeIds.Add("cook_fish");
        actor.KnownRecipeIds.Add("umbrella");
        var actors = new Dictionary<ActorId, ActorState> { [actorId] = actor };

        var events = domain.BuildPeriodicSnapshot(world, actors, 600L);

        var recipeEvt = events.Single(e => e.EventType == "PeriodicRecipeSnapshot");
        var knownRecipes = (string)recipeEvt.Details["knownRecipes"];
        Assert.Equal("cook_fish, rope, umbrella", knownRecipes);
    }

    [Fact]
    public void BuildPeriodicSnapshot_ActorNoRecipes_ShowsNone()
    {
        var domain = CreateDomain();
        var world = CreateWorld(domain);
        var actorId = new ActorId("Charlie");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        var actors = new Dictionary<ActorId, ActorState> { [actorId] = actor };

        var events = domain.BuildPeriodicSnapshot(world, actors, 600L);

        var recipeEvt = events.Single(e => e.EventType == "PeriodicRecipeSnapshot");
        Assert.Equal("(none)", recipeEvt.Details["knownRecipes"]);
    }

    [Fact]
    public void BuildPeriodicSnapshot_MaintainableItem_IncludesQuality()
    {
        var domain = CreateDomain();
        var world = CreateWorld(domain);
        var campfire = new CampfireItem("test_campfire");
        campfire.Quality = 65.0;
        world.AddWorldItem(campfire, "beach");
        var actors = new Dictionary<ActorId, ActorState>();

        var events = domain.BuildPeriodicSnapshot(world, actors, 600L);

        var campfireEvt = events.SingleOrDefault(e =>
            e.EventType == "PeriodicWorldItemSnapshot" &&
            (string)e.Details["itemId"] == "test_campfire");
        Assert.NotNull(campfireEvt);
        Assert.True(campfireEvt.Details.ContainsKey("quality"));
        Assert.Equal(65.0, campfireEvt.Details["quality"]);
    }

    [Fact]
    public void BuildPeriodicSnapshot_ToolItem_IncludesToolFields()
    {
        var domain = CreateDomain();
        var world = CreateWorld(domain);
        var pole = new FishingPoleItem("test_pole", new ActorId("Dave"));
        world.AddWorldItem(pole, "beach");
        var actors = new Dictionary<ActorId, ActorState>();

        var events = domain.BuildPeriodicSnapshot(world, actors, 600L);

        var poleEvt = events.SingleOrDefault(e =>
            e.EventType == "PeriodicWorldItemSnapshot" &&
            (string)e.Details["itemId"] == "test_pole");
        Assert.NotNull(poleEvt);
        Assert.True(poleEvt.Details.ContainsKey("isBroken"));
        Assert.True(poleEvt.Details.ContainsKey("ownershipType"));
        Assert.True(poleEvt.Details.ContainsKey("owner"));
        Assert.Equal("Dave", poleEvt.Details["owner"]);
    }

    [Fact]
    public void BuildPeriodicSnapshot_TickIsPreservedOnEvents()
    {
        var domain = CreateDomain();
        var world = CreateWorld(domain);
        var actors = new Dictionary<ActorId, ActorState>();
        const long tick = 9999L;

        var events = domain.BuildPeriodicSnapshot(world, actors, tick);

        Assert.All(events, e => Assert.Equal(tick, e.Tick));
    }

    [Fact]
    public void BuildPeriodicSnapshot_SupplyPileNotIncludedInWorldItemSnapshots()
    {
        var domain = CreateDomain();
        var world = CreateWorld(domain);
        var actors = new Dictionary<ActorId, ActorState>();

        var events = domain.BuildPeriodicSnapshot(world, actors, 600L);

        // Supply piles should only appear in PeriodicSupplySnapshot, not PeriodicWorldItemSnapshot
        var itemEvts = events.Where(e => e.EventType == "PeriodicWorldItemSnapshot").ToList();
        Assert.DoesNotContain(itemEvts, e => (string)e.Details["itemType"] == "supply_pile");
    }
}
