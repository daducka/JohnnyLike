using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Island.Supply;
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
        var blanket = new PalmFrondBlanketItem("palm_frond_blanket");
        world.WorldItems.Add(blanket);
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId, new Dictionary<string, object>
        {
            ["WIS"] = 16,
            ["STR"] = 14
        });

        var campfire = world.MainCampfire!;

        var initialCampfireQuality = campfire.Quality;
        var initialBlanketQuality = blanket.Quality;
        var initialFuel = campfire.FuelSeconds;

        var currentTime = 0.0;
        var oneDay = 86400.0;

        world.OnTickAdvanced((long)(currentTime + oneDay * 20));
        Assert.True(blanket.Quality < initialBlanketQuality, "Blanket quality should decay");
        Assert.True(campfire.FuelSeconds < initialFuel, "Fuel should be consumed");

        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(42), new EmptyResourceAvailability());

        var hasMaintenanceAction = candidates.Any(c =>
            c.Action.Id.Value.Contains("campfire") || c.Action.Id.Value.Contains("blanket"));

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
    public void Integration_BlanketDecay_SuggestsRepair()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var blanket = new PalmFrondBlanketItem("palm_frond_blanket");
        blanket.Quality = 40.0;
        world.WorldItems.Add(blanket);
        world.SharedSupplyPile!.AddSupply(5, () => new PalmFrondSupply());

        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);

        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(42), new EmptyResourceAvailability());

        Assert.Contains(candidates, c => c.Action.Id.Value == "repair_blanket");
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
            Duration.FromTicks(500L),
            ""
        );

        var outcome = new ActionOutcome(
            new ActionId("repair_campfire"), ActionOutcomeType.Success, Duration.FromTicks(500L),
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
            Duration.FromTicks(400L),
            ""
        );

        var outcome = new ActionOutcome(
            new ActionId("add_fuel_campfire"), ActionOutcomeType.Success, Duration.FromTicks(400L),
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
    public void Integration_RepairBlanketAction_RestoresBlanketQuality()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);

        var blanket = new PalmFrondBlanketItem("palm_frond_blanket");
        blanket.Quality = 40.0;
        world.WorldItems.Add(blanket);
        world.SharedSupplyPile!.AddSupply(5, () => new PalmFrondSupply());

        var rng = new RandomRngStream(new Random(42));

        actor.CurrentAction = new ActionSpec(
            new ActionId("repair_blanket"),
            ActionKind.Interact,
            new SkillCheckActionParameters(
                    new SkillCheckRequest(11, 2, AdvantageType.Normal, "Survival"),
                    new SkillCheckResult(10, 10 + 2, RollOutcomeTier.Success, true, 0.5)),
            Duration.FromTicks(500L),
            ""
        );

        var outcome = new ActionOutcome(
            new ActionId("repair_blanket"), ActionOutcomeType.Success, Duration.FromTicks(500L),
            new Dictionary<string, object>
            {
                ["tier"] = "Success"
            }
        );

        domain.ApplyActionEffects(actorId, outcome, actor, world, rng, new EmptyResourceAvailability(), new Action<EffectContext>(blanket.ApplyRepairBlanketEffect));

        Assert.True(blanket.Quality > 40.0, "Blanket quality should increase after repair");
    }

    [Fact]
    public void Integration_Serialization_PreservesMaintenanceState()
    {
        var domain = new IslandDomainPack();
        var world1 = (IslandWorldState)domain.CreateInitialWorldState();
        world1.WorldItems.Add(new CampfireItem("main_campfire"));
        var blanket = new PalmFrondBlanketItem("palm_frond_blanket");
        blanket.Quality = 45.3;
        world1.WorldItems.Add(blanket);

        world1.MainCampfire!.FuelSeconds = 1234.5;
        world1.MainCampfire.Quality = 67.8;
        world1.MainCampfire.IsLit = false;

        var json = world1.Serialize();

        var world2 = new IslandWorldState();
        world2.Deserialize(json);

        Assert.Equal(1234.5, world2.MainCampfire!.FuelSeconds);
        Assert.Equal(67.8, world2.MainCampfire.Quality);
        Assert.False(world2.MainCampfire.IsLit);
        var blanket2 = world2.WorldItems.OfType<PalmFrondBlanketItem>().FirstOrDefault();
        Assert.NotNull(blanket2);
        Assert.Equal(45.3, blanket2!.Quality);
    }
}
