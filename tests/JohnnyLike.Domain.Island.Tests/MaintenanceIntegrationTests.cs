using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Tests;

public class MaintenanceIntegrationTests
{
    [Fact]
    public void Integration_DayNightCycle_ItemsDecayAndMaintenanceEmerges()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        world.WorldItems.Add(new CampfireItem("main_campfire"));
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId, new Dictionary<string, object>
        {
            ["WIS"] = 16,
            ["STR"] = 14
        });
        
        var campfire = world.MainCampfire!;
        var shelter = world.MainShelter!;
        
        var initialCampfireQuality = campfire.Quality;
        var initialShelterQuality = shelter.Quality;
        var initialFuel = campfire.FuelSeconds;
        
        var currentTime = 0.0;
        var oneDay = 86400.0;
        
        world.OnTickAdvanced((long)(currentTime + oneDay * 20));
        Assert.True(shelter.Quality < initialShelterQuality, "Shelter quality should decay");
        Assert.True(campfire.FuelSeconds < initialFuel, "Fuel should be consumed");
        
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(42), new EmptyResourceAvailability());
        
        var hasMaintenanceAction = candidates.Any(c => 
            c.Action.Id.Value.Contains("campfire") || c.Action.Id.Value.Contains("shelter"));
        
        Assert.True(hasMaintenanceAction, "Should suggest maintenance actions after decay");
    }

    [Fact]
    public void Integration_CampfireFuelDepletion_SuggestsRelighting()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        world.WorldItems.Add(new CampfireItem("main_campfire"));
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        var campfire = world.MainCampfire!;
        campfire.FuelSeconds = 10.0;
        
        world.OnTickAdvanced((long)(20.0 * 20));
        
        var candidates = domain.GenerateCandidates(actorId, actor, world, 400L, new Random(42), new EmptyResourceAvailability());
        
        Assert.Contains(candidates, c => c.Action.Id.Value == "relight_campfire");
    }

    [Fact]
    public void Integration_BadWeather_AcceleratesShelterDecay()
    {
        var domain = new IslandDomainPack();
        var worldClear = (IslandWorldState)domain.CreateInitialWorldState();
        var worldRainy = (IslandWorldState)domain.CreateInitialWorldState();
        
        // Set different weather conditions  
        worldClear.GetItem<WeatherItem>("weather")!.Precipitation = PrecipitationBand.Clear;
        worldRainy.GetItem<WeatherItem>("weather")!.Precipitation = PrecipitationBand.Rainy;
        
        // Tick both worlds with the same time advance
        var duration = 3600.0;
        worldClear.OnTickAdvanced((long)(duration * 20));
        worldRainy.OnTickAdvanced((long)(duration * 20));
        
        Assert.True(worldRainy.MainShelter!.Quality < worldClear.MainShelter!.Quality,
            "Shelter should decay faster in rainy weather");
    }

    [Fact]
    public void Integration_MaintenanceActions_RestoreItemQuality()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        world.WorldItems.Add(new CampfireItem("main_campfire"));
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        var campfire = world.MainCampfire!;
        campfire.Quality = 50.0;
        
        var rng = new RandomRngStream(new Random(42));
        
        actor.CurrentAction = new ActionSpec(
            new ActionId("repair_campfire"),
            ActionKind.Interact,
            new SkillCheckActionParameters(
                    new SkillCheckRequest(11, 2, AdvantageType.Normal, "Survival"),
                    new SkillCheckResult(10, 10 + 2, RollOutcomeTier.Success, true, 0.5)),
            500L
        );
        
        var outcome = new ActionOutcome(
            new ActionId("repair_campfire"),
            ActionOutcomeType.Success, 500L,
            new Dictionary<string, object> 
            { 
                ["tier"] = "Success"
            }
        );
        
        domain.ApplyActionEffects(actorId, outcome, actor, world, rng, new EmptyResourceAvailability(), new Action<EffectContext>(campfire.ApplyRepairEffect));
        
        Assert.True(campfire.Quality > 50.0, "Campfire quality should increase after repair");
    }

    [Fact]
    public void Integration_AddFuelAction_IncreasesFuelSeconds()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        world.WorldItems.Add(new CampfireItem("main_campfire"));
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        var campfire = world.MainCampfire!;
        campfire.FuelSeconds = 500.0;
        var initialFuel = campfire.FuelSeconds;
        
        var rng = new RandomRngStream(new Random(42));
        
        actor.CurrentAction = new ActionSpec(
            new ActionId("add_fuel_campfire"),
            ActionKind.Interact,
            new SkillCheckActionParameters(
                    new SkillCheckRequest(10, 2, AdvantageType.Normal, "Survival"),
                    new SkillCheckResult(10, 10 + 2, RollOutcomeTier.Success, true, 0.5)),
            400L
        );
        
        var outcome = new ActionOutcome(
            new ActionId("add_fuel_campfire"),
            ActionOutcomeType.Success, 400L,
            new Dictionary<string, object> 
            { 
                ["tier"] = "Success"
            }
        );
        
        domain.ApplyActionEffects(actorId, outcome, actor, world, rng, new EmptyResourceAvailability(), new Action<EffectContext>(campfire.ApplyAddFuelEffect));
        
        Assert.True(campfire.FuelSeconds > initialFuel, "Fuel should increase after adding fuel");
    }

    [Fact]
    public void Integration_HighSurvivalActor_EarlierMaintenanceDetection()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        world.WorldItems.Add(new CampfireItem("main_campfire"));
        
        world.MainCampfire!.FuelSeconds = 1500.0;
        
        var lowSkillActor = (IslandActorState)domain.CreateActorState(
            new ActorId("LowSkill"), 
            new Dictionary<string, object> { ["WIS"] = 8, ["STR"] = 8 }
        );
        
        var highSkillActor = (IslandActorState)domain.CreateActorState(
            new ActorId("HighSkill"), 
            new Dictionary<string, object> { ["WIS"] = 18, ["STR"] = 16 }
        );
        
        var candidatesLow = domain.GenerateCandidates(
            new ActorId("LowSkill"), lowSkillActor, world, 0L, new Random(42), new EmptyResourceAvailability());
        var candidatesHigh = domain.GenerateCandidates(
            new ActorId("HighSkill"), highSkillActor, world, 0L, new Random(42), new EmptyResourceAvailability());
        
        var lowFuelCandidate = candidatesLow.FirstOrDefault(c => c.Action.Id.Value == "add_fuel_campfire");
        var highFuelCandidate = candidatesHigh.FirstOrDefault(c => c.Action.Id.Value == "add_fuel_campfire");
        
        if (lowFuelCandidate != null && highFuelCandidate != null)
        {
            Assert.True(highFuelCandidate.Score > lowFuelCandidate.Score,
                "High survival actor should have higher maintenance awareness");
        }
    }

    [Fact]
    public void Integration_RebuildAction_RestoresItemCompletely()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        var shelter = world.MainShelter!;
        shelter.Quality = 5.0;
        
        var rng = new RandomRngStream(new Random(42));
        
        actor.CurrentAction = new ActionSpec(
            new ActionId("rebuild_shelter"),
            ActionKind.Interact,
            new SkillCheckActionParameters(
                    new SkillCheckRequest(14, 2, AdvantageType.Normal, "Survival"),
                    new SkillCheckResult(10, 10 + 2, RollOutcomeTier.Success, true, 0.5)),
            1800L
        );
        
        var outcome = new ActionOutcome(
            new ActionId("rebuild_shelter"),
            ActionOutcomeType.Success, 1800L,
            new Dictionary<string, object> 
            { 
                ["tier"] = "Success"
            }
        );
        
        domain.ApplyActionEffects(actorId, outcome, actor, world, rng, new EmptyResourceAvailability(), new Action<EffectContext>(shelter.ApplyRebuildShelterEffect));
        
        Assert.True(shelter.Quality >= 80.0, "Shelter quality should be significantly restored after rebuild");
    }

    [Fact]
    public void Integration_Serialization_PreservesMaintenanceState()
    {
        var domain = new IslandDomainPack();
        var world1 = (IslandWorldState)domain.CreateInitialWorldState();
        world1.WorldItems.Add(new CampfireItem("main_campfire"));
        
        world1.MainCampfire!.FuelSeconds = 1234.5;
        world1.MainCampfire.Quality = 67.8;
        world1.MainCampfire.IsLit = false;
        world1.MainShelter!.Quality = 45.3;
        
        var json = world1.Serialize();
        
        var world2 = new IslandWorldState();
        world2.Deserialize(json);
        
        Assert.Equal(1234.5, world2.MainCampfire!.FuelSeconds);
        Assert.Equal(67.8, world2.MainCampfire.Quality);
        Assert.False(world2.MainCampfire.IsLit);
        Assert.Equal(45.3, world2.MainShelter!.Quality);
    }
}
