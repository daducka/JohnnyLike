using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Island.Recipes;
using JohnnyLike.Domain.Island.Supply;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Tests;

public class WorldItemEnvironmentTests
{
    private static IslandWorldState MakeWorld()
    {
        var world = new IslandWorldState();
        world.WorldItems.Add(new CalendarItem("calendar"));
        world.WorldItems.Add(new WeatherItem("weather"));
        world.WorldItems.Add(new BeachItem("beach"));
        var pile = new SupplyPile("shared_supplies", "shared");
        world.WorldItems.Add(pile);
        return world;
    }

    // ── CalendarItem ──────────────────────────────────────────────────────────

    [Fact]
    public void CalendarItem_AdvancesTimeOfDay()
    {
        var calendar = new CalendarItem("calendar");
        calendar.TimeOfDay = 0.5;
        var world = new IslandWorldState();

        calendar.Tick(21600.0, world, 0.0); // 6 hours

        Assert.InRange(calendar.TimeOfDay, 0.74, 0.76);
    }

    [Fact]
    public void CalendarItem_IncrementsDay()
    {
        var calendar = new CalendarItem("calendar") { TimeOfDay = 0.9, DayCount = 0 };
        var world = new IslandWorldState();

        calendar.Tick(8640.0, world, 0.0); // 2.4 hours

        Assert.Equal(1, calendar.DayCount);
        Assert.InRange(calendar.TimeOfDay, 0.0, 0.1);
    }

    // ── WeatherItem ───────────────────────────────────────────────────────────

    [Fact]
    public void WeatherItem_IsCold_AtNight()
    {
        var world = new IslandWorldState();
        var calendar = new CalendarItem("calendar") { TimeOfDay = 0.083 }; // 2am
        world.WorldItems.Add(calendar);
        var weather = new WeatherItem("weather");

        weather.Tick(0.0, world, 0.0);

        Assert.Equal(TemperatureBand.Cold, weather.Temperature);
    }

    [Fact]
    public void WeatherItem_IsHot_AtNoon()
    {
        var world = new IslandWorldState();
        var calendar = new CalendarItem("calendar") { TimeOfDay = 0.5 }; // noon
        world.WorldItems.Add(calendar);
        var weather = new WeatherItem("weather");

        weather.Tick(0.0, world, 0.0);

        Assert.Equal(TemperatureBand.Hot, weather.Temperature);
    }

    [Fact]
    public void WeatherItem_IsCold_AtEarlyMorning()
    {
        var world = new IslandWorldState();
        var calendar = new CalendarItem("calendar") { TimeOfDay = 0.25 }; // 6am = hour 6, boundary
        world.WorldItems.Add(calendar);
        var weather = new WeatherItem("weather");

        weather.Tick(0.0, world, 0.0);

        // hour 6.0 < 8.0, therefore Cold
        Assert.Equal(TemperatureBand.Cold, weather.Temperature);
    }

    // ── BeachItem ─────────────────────────────────────────────────────────────

    [Fact]
    public void BeachItem_ExploreBeach_YieldsSticks()
    {
        var world = MakeWorld();
        var beach = world.GetItem<BeachItem>("beach")!;
        beach.GetSupply<StickSupply>("sticks")!.Quantity = 20;

        var actor = new IslandActorState { Id = new ActorId("test_actor") };
        var rng = new Random(0);
        var ctx = new Candidates.IslandContext(
            actor.Id, actor, world, 0.0,
            new RandomRngStream(rng), rng,
            new EmptyResourceAvailability());

        var candidates = new List<ActionCandidate>();
        beach.AddCandidates(ctx, candidates);

        Assert.Contains(candidates, c => c.Action.Id.Value == "explore_beach");

        // Execute the effect
        var explore = candidates.Single(c => c.Action.Id.Value == "explore_beach");
        var effectCtx = new EffectContext
        {
            ActorId = actor.Id,
            Outcome = new ActionOutcome(new ActionId("explore_beach"), ActionOutcomeType.Success, 0.0),
            Actor = actor,
            World = world,
            Tier = RollOutcomeTier.Success,
            Rng = new RandomRngStream(new Random(0)),
            Reservations = new EmptyResourceAvailability()
        };

        ((Action<EffectContext>)explore.EffectHandler!)(effectCtx);

        var pile = world.SharedSupplyPile!;
        Assert.True(pile.GetQuantity<StickSupply>("sticks") > 0, "Should have sticks after exploring beach");
    }

    [Fact]
    public void BeachItem_Tide_IsHighAfterSixHours()
    {
        var world = new IslandWorldState();
        var calendar = new CalendarItem("calendar") { TimeOfDay = 0.25 }; // 6am = tidePhase=6 → high
        world.WorldItems.Add(calendar);
        var beach = new BeachItem("beach");
        world.WorldItems.Add(beach);

        beach.Tick(0.0, world, 0.0);

        Assert.Equal(TideLevel.High, beach.Tide);
    }

    // ── Campfire Recipe ───────────────────────────────────────────────────────

    [Fact]
    public void CampfireRecipe_CannotBeDiscovered_WhenHot()
    {
        var world = MakeWorld();
        world.GetItem<WeatherItem>("weather")!.Temperature = TemperatureBand.Hot;

        var actor = new IslandActorState { Id = new ActorId("test_actor") };

        var recipe = IslandRecipeRegistry.Get("campfire");
        var canDiscover = recipe.Discovery!.CanDiscover(actor, world);

        Assert.False(canDiscover);
    }

    [Fact]
    public void CampfireRecipe_CanBeDiscovered_WhenCold()
    {
        var world = MakeWorld();
        world.GetItem<WeatherItem>("weather")!.Temperature = TemperatureBand.Cold;

        var actor = new IslandActorState { Id = new ActorId("test_actor") };

        var recipe = IslandRecipeRegistry.Get("campfire");
        var canDiscover = recipe.Discovery!.CanDiscover(actor, world);

        Assert.True(canDiscover);
    }

    [Fact]
    public void CampfireRecipe_IsInRegistry()
    {
        Assert.True(IslandRecipeRegistry.All.ContainsKey("campfire"));
    }

    // ── OceanItem ─────────────────────────────────────────────────────────────

    [Fact]
    public void OceanItem_RegeneratesFish_OverTime()
    {
        var world = new IslandWorldState();
        var calendar = new CalendarItem("calendar");
        world.WorldItems.Add(calendar);
        var ocean = new OceanItem("ocean") { FishRegenRatePerMinute = 5.0 };
        ocean.GetSupply<FishSupply>("fish")!.Quantity = 50.0;
        world.WorldItems.Add(ocean);

        ocean.Tick(60.0, world, 0.0); // 1 minute

        Assert.Equal(55.0, ocean.GetQuantity<FishSupply>("fish"), 1);
    }

    [Fact]
    public void OceanItem_FishCappedAt100()
    {
        var world = new IslandWorldState();
        var calendar = new CalendarItem("calendar");
        world.WorldItems.Add(calendar);
        var ocean = new OceanItem("ocean") { FishRegenRatePerMinute = 10.0 };
        ocean.GetSupply<FishSupply>("fish")!.Quantity = 95.0;

        ocean.Tick(60.0, world, 0.0);

        Assert.Equal(100.0, ocean.GetQuantity<FishSupply>("fish"));
    }

    [Fact]
    public void OceanItem_IsInInitialWorldState()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();

        Assert.NotNull(world.GetItem<OceanItem>("ocean"));
    }

    // ── UmbrellaItem (Precipitation-based) ────────────────────────────────────

    [Fact]
    public void UmbrellaItem_DeployedWhenRaining_UsingPrecipitationBand()
    {
        var world = MakeWorld();
        world.GetItem<WeatherItem>("weather")!.Precipitation = PrecipitationBand.Rainy;

        var actor = new IslandActorState { Id = new ActorId("test_actor") };
        var umbrella = new UmbrellaItem($"umbrella_{actor.Id.Value}", actor.Id);
        world.WorldItems.Add(umbrella);

        var rng = new Random(42);
        var ctx = new Candidates.IslandContext(
            actor.Id, actor, world, 0.0,
            new RandomRngStream(rng), rng,
            new EmptyResourceAvailability());

        var candidates = new List<ActionCandidate>();
        umbrella.AddCandidates(ctx, candidates);

        Assert.Contains(candidates, c => c.Action.Id.Value == "deploy_umbrella");
    }

    [Fact]
    public void UmbrellaItem_NotDeployedWhenClear_UsingPrecipitationBand()
    {
        var world = MakeWorld();
        world.GetItem<WeatherItem>("weather")!.Precipitation = PrecipitationBand.Clear;

        var actor = new IslandActorState { Id = new ActorId("test_actor") };
        var umbrella = new UmbrellaItem($"umbrella_{actor.Id.Value}", actor.Id);
        world.WorldItems.Add(umbrella);

        var rng = new Random(42);
        var ctx = new Candidates.IslandContext(
            actor.Id, actor, world, 0.0,
            new RandomRngStream(rng), rng,
            new EmptyResourceAvailability());

        var candidates = new List<ActionCandidate>();
        umbrella.AddCandidates(ctx, candidates);

        Assert.DoesNotContain(candidates, c => c.Action.Id.Value == "deploy_umbrella");
    }

    // ── BeachItem bounty-gated candidate ─────────────────────────────────────

    [Fact]
    public void BeachItem_ExploreCandidate_NotOffered_WhenInsufficientBounty()
    {
        var world = MakeWorld();
        var beach = world.GetItem<BeachItem>("beach")!;

        // Set bounty below minimum
        beach.GetSupply<StickSupply>("sticks")!.Quantity = 1.0;
        beach.GetSupply<WoodSupply>("driftwood")!.Quantity = 1.0;

        var actor = new IslandActorState { Id = new ActorId("test_actor") };
        var rng = new Random(0);
        var ctx = new Candidates.IslandContext(
            actor.Id, actor, world, 0.0,
            new RandomRngStream(rng), rng,
            new EmptyResourceAvailability());

        var candidates = new List<ActionCandidate>();
        beach.AddCandidates(ctx, candidates);

        Assert.DoesNotContain(candidates, c => c.Action.Id.Value == "explore_beach");
    }

    // ── CoconutTreeItem bounty ────────────────────────────────────────────────

    [Fact]
    public void CoconutTreeItem_HasPalmFrondBounty()
    {
        var tree = new CoconutTreeItem("palm_tree");
        Assert.True(tree.GetQuantity<PalmFrondSupply>("palm_frond") > 0);
    }

    [Fact]
    public void CoconutTreeItem_ExploreCandidate_NotOffered_WhenNoFronds()
    {
        var world = MakeWorld();
        var tree = new CoconutTreeItem("palm_tree");
        // Remove fronds from bounty
        tree.GetSupply<PalmFrondSupply>("palm_frond")!.Quantity = 0;
        world.WorldItems.Add(tree);

        var actor = new IslandActorState { Id = new ActorId("test_actor") };
        var rng = new Random(0);
        var ctx = new Candidates.IslandContext(
            actor.Id, actor, world, 0.0,
            new RandomRngStream(rng), rng,
            new EmptyResourceAvailability());

        var candidates = new List<ActionCandidate>();
        tree.AddCandidates(ctx, candidates);

        Assert.DoesNotContain(candidates, c => c.Action.Id.Value == "shake_tree_coconut");
    }
}
