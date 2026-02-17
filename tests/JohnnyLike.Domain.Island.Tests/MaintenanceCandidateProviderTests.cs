using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Tests;

public class CampfireMaintenanceCandidateProviderTests
{
    [Fact]
    public void Provider_SuggestsAddFuel_WhenFuelIsLow()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        world.MainCampfire!.FuelSeconds = 1200.0;
        
        var ctx = new IslandContext(actorId, actor, world, 0.0, new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        var provider = new CampfireMaintenanceCandidateProvider();
        var candidates = new List<ActionCandidate>();
        
        provider.AddCandidates(ctx, candidates);
        
        Assert.Contains(candidates, c => c.Action.Id.Value == "add_fuel_campfire");
    }

    [Fact]
    public void Provider_SuggestsRelight_WhenCampfireIsOut()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        world.MainCampfire!.IsLit = false;
        world.MainCampfire.Quality = 50.0;
        
        var ctx = new IslandContext(actorId, actor, world, 0.0, new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        var provider = new CampfireMaintenanceCandidateProvider();
        var candidates = new List<ActionCandidate>();
        
        provider.AddCandidates(ctx, candidates);
        
        Assert.Contains(candidates, c => c.Action.Id.Value == "relight_campfire");
    }

    [Fact]
    public void Provider_SuggestsRepair_WhenQualityIsLow()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        world.MainCampfire!.Quality = 50.0;
        
        var ctx = new IslandContext(actorId, actor, world, 0.0, new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        var provider = new CampfireMaintenanceCandidateProvider();
        var candidates = new List<ActionCandidate>();
        
        provider.AddCandidates(ctx, candidates);
        
        Assert.Contains(candidates, c => c.Action.Id.Value == "repair_campfire");
    }

    [Fact]
    public void Provider_SuggestsRebuild_WhenQualityIsCriticallyLow()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        world.MainCampfire!.Quality = 5.0;
        
        var ctx = new IslandContext(actorId, actor, world, 0.0, new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        var provider = new CampfireMaintenanceCandidateProvider();
        var candidates = new List<ActionCandidate>();
        
        provider.AddCandidates(ctx, candidates);
        
        Assert.Contains(candidates, c => c.Action.Id.Value == "rebuild_campfire");
    }

    [Fact]
    public void Provider_HigherSurvivalSkill_IncreasesScore()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
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
        
        var provider = new CampfireMaintenanceCandidateProvider();
        var candidatesLow = new List<ActionCandidate>();
        var candidatesHigh = new List<ActionCandidate>();
        
        provider.AddCandidates(ctxLow, candidatesLow);
        provider.AddCandidates(ctxHigh, candidatesHigh);
        
        var scoreLow = candidatesLow.First(c => c.Action.Id.Value == "add_fuel_campfire").Score;
        var scoreHigh = candidatesHigh.First(c => c.Action.Id.Value == "add_fuel_campfire").Score;
        
        Assert.True(scoreHigh > scoreLow, $"High skill score ({scoreHigh}) should be greater than low skill score ({scoreLow})");
    }

    [Fact]
    public void Provider_DoesNotSuggestActions_WhenCampfireMissing()
    {
        var world = new IslandWorldState();
        var actorId = new ActorId("TestActor");
        var domain = new IslandDomainPack();
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        var ctx = new IslandContext(actorId, actor, world, 0.0, new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        var provider = new CampfireMaintenanceCandidateProvider();
        var candidates = new List<ActionCandidate>();
        
        provider.AddCandidates(ctx, candidates);
        
        Assert.Empty(candidates);
    }
}

public class ShelterMaintenanceCandidateProviderTests
{
    [Fact]
    public void Provider_SuggestsRepair_WhenQualityIsLow()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        world.MainShelter!.Quality = 50.0;
        
        var ctx = new IslandContext(actorId, actor, world, 0.0, new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        var provider = new ShelterMaintenanceCandidateProvider();
        var candidates = new List<ActionCandidate>();
        
        provider.AddCandidates(ctx, candidates);
        
        Assert.Contains(candidates, c => c.Action.Id.Value == "repair_shelter");
    }

    [Fact]
    public void Provider_SuggestsReinforce_WhenQualityIsVeryLow()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        world.MainShelter!.Quality = 40.0;
        
        var ctx = new IslandContext(actorId, actor, world, 0.0, new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        var provider = new ShelterMaintenanceCandidateProvider();
        var candidates = new List<ActionCandidate>();
        
        provider.AddCandidates(ctx, candidates);
        
        Assert.Contains(candidates, c => c.Action.Id.Value == "reinforce_shelter");
    }

    [Fact]
    public void Provider_SuggestsRebuild_WhenQualityIsCriticallyLow()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        world.MainShelter!.Quality = 10.0;
        
        var ctx = new IslandContext(actorId, actor, world, 0.0, new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        var provider = new ShelterMaintenanceCandidateProvider();
        var candidates = new List<ActionCandidate>();
        
        provider.AddCandidates(ctx, candidates);
        
        Assert.Contains(candidates, c => c.Action.Id.Value == "rebuild_shelter");
    }

    [Fact]
    public void Provider_RainyWeather_IncreasesRepairScore()
    {
        var domain = new IslandDomainPack();
        var worldClear = (IslandWorldState)domain.CreateInitialWorldState();
        var worldRainy = (IslandWorldState)domain.CreateInitialWorldState();
        
        worldClear.Weather = Weather.Clear;
        worldClear.MainShelter!.Quality = 50.0;
        
        worldRainy.Weather = Weather.Rainy;
        worldRainy.MainShelter!.Quality = 50.0;
        
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        var ctxClear = new IslandContext(actorId, actor, worldClear, 0.0, 
            new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        var ctxRainy = new IslandContext(actorId, actor, worldRainy, 0.0, 
            new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        
        var provider = new ShelterMaintenanceCandidateProvider();
        var candidatesClear = new List<ActionCandidate>();
        var candidatesRainy = new List<ActionCandidate>();
        
        provider.AddCandidates(ctxClear, candidatesClear);
        provider.AddCandidates(ctxRainy, candidatesRainy);
        
        var scoreClear = candidatesClear.First(c => c.Action.Id.Value == "repair_shelter").Score;
        var scoreRainy = candidatesRainy.First(c => c.Action.Id.Value == "repair_shelter").Score;
        
        Assert.True(scoreRainy > scoreClear);
    }

    [Fact]
    public void Provider_HigherSurvivalAndWisdom_IncreasesScore()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
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
        
        var provider = new ShelterMaintenanceCandidateProvider();
        var candidatesLow = new List<ActionCandidate>();
        var candidatesHigh = new List<ActionCandidate>();
        
        provider.AddCandidates(ctxLow, candidatesLow);
        provider.AddCandidates(ctxHigh, candidatesHigh);
        
        var scoreLow = candidatesLow.First(c => c.Action.Id.Value == "repair_shelter").Score;
        var scoreHigh = candidatesHigh.First(c => c.Action.Id.Value == "repair_shelter").Score;
        
        Assert.True(scoreHigh > scoreLow, $"High skill score ({scoreHigh}) should be greater than low skill score ({scoreLow})");
    }

    [Fact]
    public void Provider_DoesNotSuggestActions_WhenShelterMissing()
    {
        var world = new IslandWorldState();
        var actorId = new ActorId("TestActor");
        var domain = new IslandDomainPack();
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        var ctx = new IslandContext(actorId, actor, world, 0.0, new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        var provider = new ShelterMaintenanceCandidateProvider();
        var candidates = new List<ActionCandidate>();
        
        provider.AddCandidates(ctx, candidates);
        
        Assert.Empty(candidates);
    }
}
