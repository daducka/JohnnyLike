using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Tests;

public class CampfireMaintenanceCandidateProviderTests
{
    [Fact]
    public void CampfireItem_SuggestsAddFuel_WhenFuelIsLow()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        world.WorldItems.Add(new CampfireItem("main_campfire"));
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        world.MainCampfire!.FuelSeconds = 1200.0;
        
        var ctx = new IslandContext(actorId, actor, world, 0.0, new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        var candidates = new List<ActionCandidate>();
        
        world.MainCampfire.AddCandidates(ctx, candidates);
        
        Assert.Contains(candidates, c => c.Action.Id.Value == "add_fuel_campfire");
    }

    [Fact]
    public void CampfireItem_SuggestsRelight_WhenCampfireIsOut()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        world.WorldItems.Add(new CampfireItem("main_campfire"));
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        world.MainCampfire!.IsLit = false;
        world.MainCampfire.Quality = 50.0;
        
        var ctx = new IslandContext(actorId, actor, world, 0.0, new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        var candidates = new List<ActionCandidate>();
        
        world.MainCampfire.AddCandidates(ctx, candidates);
        
        Assert.Contains(candidates, c => c.Action.Id.Value == "relight_campfire");
    }

    [Fact]
    public void CampfireItem_SuggestsRepair_WhenQualityIsLow()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        world.WorldItems.Add(new CampfireItem("main_campfire"));
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        world.MainCampfire!.Quality = 50.0;
        
        var ctx = new IslandContext(actorId, actor, world, 0.0, new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        var candidates = new List<ActionCandidate>();
        
        world.MainCampfire.AddCandidates(ctx, candidates);
        
        Assert.Contains(candidates, c => c.Action.Id.Value == "repair_campfire");
    }

    [Fact]
    public void CampfireItem_SuggestsRebuild_WhenQualityIsCriticallyLow()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        world.WorldItems.Add(new CampfireItem("main_campfire"));
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        world.MainCampfire!.Quality = 5.0;
        
        var ctx = new IslandContext(actorId, actor, world, 0.0, new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        var candidates = new List<ActionCandidate>();
        
        world.MainCampfire.AddCandidates(ctx, candidates);
        
        Assert.Contains(candidates, c => c.Action.Id.Value == "rebuild_campfire");
    }

    [Fact]
    public void CampfireItem_HigherSurvivalSkill_IncreasesScore()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        world.WorldItems.Add(new CampfireItem("main_campfire"));
        world.MainCampfire!.FuelSeconds = 1200.0;
        
        var lowSkillActor = (IslandActorState)domain.CreateActorState(
            new ActorId("LowSkill"), 
            new Dictionary<string, object> { ["WIS"] = 8, ["STR"] = 8 }
        );
        
        var highSkillActor = (IslandActorState)domain.CreateActorState(
            new ActorId("HighSkill"), 
            new Dictionary<string, object> { ["WIS"] = 18, ["STR"] = 16 }
        );
        
        var ctxLow = new IslandContext(new ActorId("LowSkill"), lowSkillActor, world, 0.0, 
            new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        var ctxHigh = new IslandContext(new ActorId("HighSkill"), highSkillActor, world, 0.0, 
            new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        
        var candidatesLow = new List<ActionCandidate>();
        var candidatesHigh = new List<ActionCandidate>();
        
        world.MainCampfire.AddCandidates(ctxLow, candidatesLow);
        world.MainCampfire.AddCandidates(ctxHigh, candidatesHigh);
        
        var scoreLow = candidatesLow.First(c => c.Action.Id.Value == "add_fuel_campfire").IntrinsicScore;
        var scoreHigh = candidatesHigh.First(c => c.Action.Id.Value == "add_fuel_campfire").IntrinsicScore;
        
        Assert.True(scoreHigh > scoreLow, $"High skill score ({scoreHigh}) should be greater than low skill score ({scoreLow})");
    }

    [Fact]
    public void CampfireItem_DoesNotSuggestActions_WhenCampfireMissing()
    {
        var world = new IslandWorldState();
        var actorId = new ActorId("TestActor");
        var domain = new IslandDomainPack();
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        var ctx = new IslandContext(actorId, actor, world, 0.0, new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        var candidates = new List<ActionCandidate>();
        
        if (world.MainCampfire != null)
        {
            world.MainCampfire.AddCandidates(ctx, candidates);
        }
        
        Assert.Empty(candidates);
    }
}

public class ShelterMaintenanceCandidateProviderTests
{
    [Fact]
    public void ShelterItem_SuggestsRepair_WhenQualityIsLow()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        world.WorldItems.Add(new CampfireItem("main_campfire"));
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        world.MainShelter!.Quality = 50.0;
        
        var ctx = new IslandContext(actorId, actor, world, 0.0, new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        var candidates = new List<ActionCandidate>();
        
        world.MainShelter.AddCandidates(ctx, candidates);
        
        Assert.Contains(candidates, c => c.Action.Id.Value == "repair_shelter");
    }

    [Fact]
    public void ShelterItem_SuggestsReinforce_WhenQualityIsVeryLow()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        world.WorldItems.Add(new CampfireItem("main_campfire"));
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        world.MainShelter!.Quality = 40.0;
        
        var ctx = new IslandContext(actorId, actor, world, 0.0, new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        var candidates = new List<ActionCandidate>();
        
        world.MainShelter.AddCandidates(ctx, candidates);
        
        Assert.Contains(candidates, c => c.Action.Id.Value == "reinforce_shelter");
    }

    [Fact]
    public void ShelterItem_SuggestsRebuild_WhenQualityIsCriticallyLow()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        world.WorldItems.Add(new CampfireItem("main_campfire"));
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        world.MainShelter!.Quality = 10.0;
        
        var ctx = new IslandContext(actorId, actor, world, 0.0, new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        var candidates = new List<ActionCandidate>();
        
        world.MainShelter.AddCandidates(ctx, candidates);
        
        Assert.Contains(candidates, c => c.Action.Id.Value == "rebuild_shelter");
    }

    [Fact]
    public void ShelterItem_RainyWeather_IncreasesRepairScore()
    {
        var domain = new IslandDomainPack();
        var worldClear = (IslandWorldState)domain.CreateInitialWorldState();
        var worldRainy = (IslandWorldState)domain.CreateInitialWorldState();
        
        worldClear.GetItem<WeatherItem>("weather")!.Temperature = TemperatureBand.Hot;
        worldClear.MainShelter!.Quality = 50.0;
        
        worldRainy.GetItem<WeatherItem>("weather")!.Temperature = TemperatureBand.Cold;
        worldRainy.MainShelter!.Quality = 50.0;
        
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        var ctxClear = new IslandContext(actorId, actor, worldClear, 0.0, 
            new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        var ctxRainy = new IslandContext(actorId, actor, worldRainy, 0.0, 
            new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        
        var candidatesClear = new List<ActionCandidate>();
        var candidatesRainy = new List<ActionCandidate>();
        
        worldClear.MainShelter.AddCandidates(ctxClear, candidatesClear);
        worldRainy.MainShelter.AddCandidates(ctxRainy, candidatesRainy);
        
        var scoreClear = candidatesClear.First(c => c.Action.Id.Value == "repair_shelter").IntrinsicScore;
        var scoreRainy = candidatesRainy.First(c => c.Action.Id.Value == "repair_shelter").IntrinsicScore;
        
        Assert.True(scoreRainy > scoreClear);
    }

    [Fact]
    public void ShelterItem_HigherSurvivalAndWisdom_IncreasesScore()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        world.WorldItems.Add(new CampfireItem("main_campfire"));
        world.MainShelter!.Quality = 50.0;
        
        var lowSkillActor = (IslandActorState)domain.CreateActorState(
            new ActorId("LowSkill"), 
            new Dictionary<string, object> { ["WIS"] = 8, ["STR"] = 8 }
        );
        
        var highSkillActor = (IslandActorState)domain.CreateActorState(
            new ActorId("HighSkill"), 
            new Dictionary<string, object> { ["WIS"] = 18, ["STR"] = 16 }
        );
        
        var ctxLow = new IslandContext(new ActorId("LowSkill"), lowSkillActor, world, 0.0, 
            new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        var ctxHigh = new IslandContext(new ActorId("HighSkill"), highSkillActor, world, 0.0, 
            new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        
        var candidatesLow = new List<ActionCandidate>();
        var candidatesHigh = new List<ActionCandidate>();
        
        world.MainShelter.AddCandidates(ctxLow, candidatesLow);
        world.MainShelter.AddCandidates(ctxHigh, candidatesHigh);
        
        var scoreLow = candidatesLow.First(c => c.Action.Id.Value == "repair_shelter").IntrinsicScore;
        var scoreHigh = candidatesHigh.First(c => c.Action.Id.Value == "repair_shelter").IntrinsicScore;
        
        Assert.True(scoreHigh > scoreLow, $"High skill score ({scoreHigh}) should be greater than low skill score ({scoreLow})");
    }

    [Fact]
    public void ShelterItem_DoesNotSuggestActions_WhenShelterMissing()
    {
        var world = new IslandWorldState();
        var actorId = new ActorId("TestActor");
        var domain = new IslandDomainPack();
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        var ctx = new IslandContext(actorId, actor, world, 0.0, new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        var candidates = new List<ActionCandidate>();
        
        if (world.MainShelter != null)
        {
            world.MainShelter.AddCandidates(ctx, candidates);
        }
        
        Assert.Empty(candidates);
    }
}
