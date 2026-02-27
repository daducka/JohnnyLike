using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Island.Supply;

namespace JohnnyLike.Domain.Island;

/// <summary>
/// Registry that maps world item type strings to factory functions for polymorphic
/// deserialization. Domains register their item types here instead of adding cases
/// to a switch statement; the engine uses this to reconstruct world state from saved data.
/// <para>
/// This registry is designed for single-threaded, deterministic simulation use.
/// All registrations must happen before the simulation starts (i.e., during domain
/// initialization), not during concurrent ticking.
/// </para>
/// </summary>
public static class WorldItemTypeRegistry
{
    // Not thread-safe by design: registrations are expected to occur during
    // single-threaded initialization before any simulation ticking begins.
    private static readonly Dictionary<string, Func<string, WorldItem>> _factories =
        new(StringComparer.Ordinal)
        {
            ["campfire"]       = id => new CampfireItem(id),
            ["shelter"]        = id => new ShelterItem(id),
            ["fishing_pole"]   = id => new FishingPoleItem(id),
            ["treasure_chest"] = id => new TreasureChestItem(id),
            ["shark"]          = id => new SharkItem(id),
            ["supply_pile"]    = id => new SupplyPile(id),
            ["umbrella_tool"]  = id => new UmbrellaItem(id),
            ["calendar"]       = id => new CalendarItem(id),
            ["weather"]        = id => new WeatherItem(id),
            ["beach"]          = id => new BeachItem(id),
            ["palm_tree"]      = id => new CoconutTreeItem(id),
            ["ocean"]          = id => new OceanItem(id),
            ["stalactite"]     = id => new StalactiteItem(id),
        };

    /// <summary>
    /// Registers or replaces a world item type factory.
    /// Overwrites any existing registration for <paramref name="typeKey"/>; this is
    /// intentional so test setups and derived domains can substitute factories without
    /// requiring a separate unregister step.
    /// </summary>
    public static void Register(string typeKey, Func<string, WorldItem> factory)
        => _factories[typeKey] = factory;

    /// <summary>
    /// Creates a world item by type string and id. Returns null if the type is unknown.
    /// </summary>
    public static WorldItem? Create(string typeKey, string id)
        => _factories.TryGetValue(typeKey, out var factory) ? factory(id) : null;

    /// <summary>Returns all registered type keys (for diagnostics).</summary>
    public static IReadOnlyCollection<string> RegisteredTypes => _factories.Keys;
}
