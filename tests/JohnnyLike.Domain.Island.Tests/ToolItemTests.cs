using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Tests;

public class ToolItemTests
{
    [Fact]
    public void ToolItem_SharedOwnership_AllowsAnyActorToUse()
    {
        var campfire = new CampfireItem("test_campfire");
        var actor1 = new ActorId("Actor1");
        var actor2 = new ActorId("Actor2");
        
        Assert.True(campfire.CanActorUseTool(actor1));
        Assert.True(campfire.CanActorUseTool(actor2));
        Assert.Equal(OwnershipType.Shared, campfire.OwnershipType);
    }

    [Fact]
    public void ToolItem_ExclusiveOwnership_OnlyOwnerCanUse()
    {
        var actor1 = new ActorId("Actor1");
        var actor2 = new ActorId("Actor2");
        var fishingPole = new FishingPoleItem("pole1", actor1);
        
        Assert.True(fishingPole.CanActorUseTool(actor1));
        Assert.False(fishingPole.CanActorUseTool(actor2));
        Assert.Equal(OwnershipType.Exclusive, fishingPole.OwnershipType);
        Assert.Equal(actor1, fishingPole.OwnerActorId);
    }

    [Fact]
    public void ToolItem_Serialization_PreservesOwnership()
    {
        var actor1 = new ActorId("Actor1");
        var fishingPole = new FishingPoleItem("pole1", actor1);
        fishingPole.Quality = 75.5;
        fishingPole.IsBroken = false;
        
        var dict = fishingPole.SerializeToDict();
        
        Assert.Equal("pole1", dict["Id"]);
        Assert.Equal("fishing_pole", dict["Type"]);
        Assert.Equal("Exclusive", dict["OwnershipType"]);
        Assert.Equal("Actor1", dict["OwnerActorId"]);
        Assert.Equal(75.5, dict["Quality"]);
        Assert.Equal(false, dict["IsBroken"]);
    }

    [Fact]
    public void ToolItem_Deserialization_RestoresOwnership()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        
        var actor1 = new ActorId("Actor1");
        var fishingPole = new FishingPoleItem("pole1", actor1);
        fishingPole.Quality = 80.0;
        fishingPole.IsBroken = false;
        
        world.WorldItems.Add(fishingPole);
        
        var json = world.Serialize();
        var newWorld = new IslandWorldState();
        newWorld.Deserialize(json);
        
        var deserializedPole = newWorld.WorldItems.OfType<FishingPoleItem>().FirstOrDefault();
        
        Assert.NotNull(deserializedPole);
        Assert.Equal("pole1", deserializedPole.Id);
        Assert.Equal(OwnershipType.Exclusive, deserializedPole.OwnershipType);
        Assert.Equal(actor1, deserializedPole.OwnerActorId);
        Assert.Equal(80.0, deserializedPole.Quality);
        Assert.False(deserializedPole.IsBroken);
    }

    [Fact]
    public void CampfireItem_Serialization_PreservesSharedOwnership()
    {
        var campfire = new CampfireItem("main_campfire");
        campfire.IsLit = true;
        campfire.FuelSeconds = 1500.0;
        campfire.Quality = 90.0;
        
        var dict = campfire.SerializeToDict();
        
        Assert.Equal("main_campfire", dict["Id"]);
        Assert.Equal("campfire", dict["Type"]);
        Assert.Equal("Shared", dict["OwnershipType"]);
        Assert.Equal(true, dict["IsLit"]);
        Assert.Equal(1500.0, dict["FuelSeconds"]);
        Assert.Equal(90.0, dict["Quality"]);
    }

    [Fact]
    public void ShelterItem_Serialization_PreservesSharedOwnership()
    {
        var shelter = new ShelterItem("main_shelter");
        shelter.Quality = 65.0;
        
        var dict = shelter.SerializeToDict();
        
        Assert.Equal("main_shelter", dict["Id"]);
        Assert.Equal("shelter", dict["Type"]);
        Assert.Equal("Shared", dict["OwnershipType"]);
        Assert.Equal(65.0, dict["Quality"]);
    }
}

public class FishingPoleItemTests
{
    [Fact]
    public void FishingPoleItem_OwnerGetsGoFishingCandidate()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        var fishingPole = new FishingPoleItem("pole1", actorId);
        fishingPole.Quality = 80.0;
        world.WorldItems.Add(fishingPole);
        
        var ctx = new IslandContext(actorId, actor, world, 0.0, new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        var candidates = new List<ActionCandidate>();
        
        fishingPole.AddCandidates(ctx, candidates);
        
        Assert.Contains(candidates, c => c.Action.Id.Value == "go_fishing");
    }

    [Fact]
    public void FishingPoleItem_NonOwnerGetsNoCandidates()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var owner = new ActorId("Owner");
        var nonOwner = new ActorId("NonOwner");
        var actor = (IslandActorState)domain.CreateActorState(nonOwner);
        
        var fishingPole = new FishingPoleItem("pole1", owner);
        fishingPole.Quality = 80.0;
        world.WorldItems.Add(fishingPole);
        
        var ctx = new IslandContext(nonOwner, actor, world, 0.0, new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        var candidates = new List<ActionCandidate>();
        
        fishingPole.AddCandidates(ctx, candidates);
        
        Assert.Empty(candidates);
    }

    [Fact]
    public void FishingPoleItem_SuggestsMaintainRod_WhenQualityBelowThreshold()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        var fishingPole = new FishingPoleItem("pole1", actorId);
        fishingPole.Quality = 70.0;
        fishingPole.IsBroken = false;
        world.WorldItems.Add(fishingPole);
        
        var ctx = new IslandContext(actorId, actor, world, 0.0, new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        var candidates = new List<ActionCandidate>();
        
        fishingPole.AddCandidates(ctx, candidates);
        
        Assert.Contains(candidates, c => c.Action.Id.Value == "maintain_rod");
    }

    [Fact]
    public void FishingPoleItem_SuggestsRepairRod_WhenBroken()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        var fishingPole = new FishingPoleItem("pole1", actorId);
        fishingPole.Quality = 15.0;
        fishingPole.IsBroken = true;
        world.WorldItems.Add(fishingPole);
        
        var ctx = new IslandContext(actorId, actor, world, 0.0, new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        var candidates = new List<ActionCandidate>();
        
        fishingPole.AddCandidates(ctx, candidates);
        
        Assert.Contains(candidates, c => c.Action.Id.Value == "repair_rod");
    }

    [Fact]
    public void FishingPoleItem_BrokenPole_DoesNotOfferGoFishing()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        var fishingPole = new FishingPoleItem("pole1", actorId);
        fishingPole.Quality = 15.0;
        fishingPole.IsBroken = true;
        world.WorldItems.Add(fishingPole);
        
        var ctx = new IslandContext(actorId, actor, world, 0.0, new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        var candidates = new List<ActionCandidate>();
        
        fishingPole.AddCandidates(ctx, candidates);
        
        Assert.DoesNotContain(candidates, c => c.Action.Id.Value == "go_fishing");
    }

    [Fact]
    public void FishingPoleItem_TickDecay_BreaksAtLowQuality()
    {
        var fishingPole = new FishingPoleItem("pole1", new ActorId("Owner"));
        var world = new IslandWorldState();
        
        fishingPole.Quality = 22.0;
        fishingPole.IsBroken = false;
        
        // Tick enough to drop quality below 20
        fishingPole.Tick(500.0, world);
        
        Assert.True(fishingPole.IsBroken);
        Assert.True(fishingPole.Quality < 20.0);
    }

    [Fact]
    public void InitializeActorItems_CreatesExclusiveFishingPole()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        
        domain.InitializeActorItems(actorId, world);
        
        var fishingPole = world.WorldItems.OfType<FishingPoleItem>().FirstOrDefault();
        
        Assert.NotNull(fishingPole);
        Assert.Equal(OwnershipType.Exclusive, fishingPole.OwnershipType);
        Assert.Equal(actorId, fishingPole.OwnerActorId);
        Assert.Equal($"fishing_pole_{actorId.Value}", fishingPole.Id);
    }
}
