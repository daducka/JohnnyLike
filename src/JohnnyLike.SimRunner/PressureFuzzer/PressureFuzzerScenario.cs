using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Island.Supply;

namespace JohnnyLike.SimRunner.PressureFuzzer;

/// <summary>
/// Predefined world scenarios for the Pressure Fuzzer.
/// Each scenario represents a distinct combination of world infrastructure and
/// food availability, enabling targeted analysis of decision surface pathologies.
/// </summary>
public enum FuzzerScenarioKind
{
    /// <summary>No edible food in supply; food sources available (tree, ocean); no comfort infrastructure.</summary>
    NoFood_SourceAvailable,
    /// <summary>Edible food in supply pile; food sources also available; minimal infrastructure.</summary>
    FoodAvailableNow,
    /// <summary>Edible food; bed and campfire present; full comfort conditions.</summary>
    FoodAvailable_WithComfort,
    /// <summary>Edible food; bed present; campfire damaged and unlit — endgame distress.</summary>
    LateCollapse,
    /// <summary>Low food availability; high crafting material abundance — rich recipe discovery opportunity.</summary>
    HighRecipeOpportunity,
    /// <summary>Minimal world: no food sources, no infrastructure — survival floor.</summary>
    LowOpportunity
}

/// <summary>
/// Builds deterministic <see cref="IslandWorldState"/> instances for each scenario kind.
/// All world states are self-contained and do not require engine initialization.
/// </summary>
public static class PressureFuzzerScenarios
{
    private static readonly IslandDomainPack _domain = new();

    public static IslandWorldState Build(FuzzerScenarioKind scenario, ActorId actorId) =>
        scenario switch
        {
            FuzzerScenarioKind.NoFood_SourceAvailable    => BuildNoFoodSourceAvailable(actorId),
            FuzzerScenarioKind.FoodAvailableNow          => BuildFoodAvailableNow(actorId),
            FuzzerScenarioKind.FoodAvailable_WithComfort => BuildFoodAvailableWithComfort(actorId),
            FuzzerScenarioKind.LateCollapse              => BuildLateCollapse(actorId),
            FuzzerScenarioKind.HighRecipeOpportunity     => BuildHighRecipeOpportunity(actorId),
            FuzzerScenarioKind.LowOpportunity            => BuildLowOpportunity(actorId),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };

    /// <summary>
    /// No edible food in supply pile; food sources available (coconut tree + fishing pole);
    /// no bed, no campfire.
    /// Default world already has a coconut tree (5 coconuts in bounty) and ocean (100 fish).
    /// </summary>
    private static IslandWorldState BuildNoFoodSourceAvailable(ActorId actorId)
    {
        var world = (IslandWorldState)_domain.CreateInitialWorldState();
        _domain.InitializeActorItems(actorId, world);
        // Supply pile has only wood (20 units) — no food.
        return world;
    }

    /// <summary>
    /// Edible food present in supply pile (coconuts + fish); food sources also available;
    /// minimal infrastructure.
    /// </summary>
    private static IslandWorldState BuildFoodAvailableNow(ActorId actorId)
    {
        var world = BuildNoFoodSourceAvailable(actorId);
        var pile = world.SharedSupplyPile!;
        pile.AddSupply(5.0, () => new CoconutSupply());
        pile.AddSupply(3.0, () => new FishSupply());
        return world;
    }

    /// <summary>
    /// Edible food; bed in good condition; campfire lit and fully fuelled.
    /// </summary>
    private static IslandWorldState BuildFoodAvailableWithComfort(ActorId actorId)
    {
        var world = BuildFoodAvailableNow(actorId);
        var pile = world.SharedSupplyPile!;
        // Add crafting materials so bed repair stays available (avoids filtering candidates).
        pile.AddSupply(5.0, () => new PalmFrondSupply());
        pile.AddSupply(5.0, () => new StickSupply());
        pile.AddSupply(3.0, () => new RopeSupply());
        world.AddWorldItem(new PalmFrondBedItem("palm_frond_bed"), "beach");
        world.AddWorldItem(new CampfireItem("main_campfire"), "beach");
        return world;
    }

    /// <summary>
    /// Edible food; bed present; campfire unlit and heavily degraded — endgame distress.
    /// </summary>
    private static IslandWorldState BuildLateCollapse(ActorId actorId)
    {
        var world = BuildFoodAvailableWithComfort(actorId);
        var campfire = world.MainCampfire!;
        campfire.IsLit = false;
        campfire.FuelSeconds = 0.0;
        campfire.Quality = 15.0;
        return world;
    }

    /// <summary>
    /// Low food availability; rich crafting material abundance to surface recipe discovery pressure.
    /// </summary>
    private static IslandWorldState BuildHighRecipeOpportunity(ActorId actorId)
    {
        var world = (IslandWorldState)_domain.CreateInitialWorldState();
        _domain.InitializeActorItems(actorId, world);
        var pile = world.SharedSupplyPile!;
        pile.AddSupply(15.0, () => new StickSupply());
        pile.AddSupply(12.0, () => new PalmFrondSupply());
        pile.AddSupply(8.0,  () => new RopeSupply());
        pile.AddSupply(10.0, () => new RocksSupply());
        pile.AddSupply(20.0, () => new WoodSupply());
        return world;
    }

    /// <summary>
    /// Minimal world: beach, calendar, weather only — no food sources, no infrastructure.
    /// The <paramref name="actorId"/> parameter is accepted for API consistency but is not
    /// used here since this scenario deliberately omits all actor-specific items.
    /// </summary>
    private static IslandWorldState BuildLowOpportunity(ActorId actorId)
    {
        var world = new IslandWorldState();
        world.AddWorldItem(new CalendarItem("calendar"), "beach");
        world.AddWorldItem(new WeatherItem("weather"), "beach");
        world.AddWorldItem(new BeachItem("beach"), "beach");
        var pile = new SupplyPile("shared_supplies", "shared");
        pile.AddSupply(5.0, () => new WoodSupply());
        world.AddWorldItem(pile, "beach");
        return world;
    }
}
