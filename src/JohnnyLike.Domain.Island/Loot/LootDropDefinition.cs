using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Domain.Island.Loot;

/// <summary>Defines a single drop that occurs when a <see cref="LootItem"/> is consumed.</summary>
public abstract class LootDropDefinition { }

/// <summary>A drop that adds a quantity of a supply to the shared supply pile.</summary>
public sealed class SupplyLootDrop : LootDropDefinition
{
    public required SupplyKind SupplyKind { get; init; }
    public double Quantity { get; init; }
}

/// <summary>A drop that spawns a world item into the same room as the loot.</summary>
public sealed class WorldItemLootDrop : LootDropDefinition
{
    /// <summary>Factory that creates the world item to spawn.</summary>
    public required Func<string, WorldItem> Factory { get; init; }

    /// <summary>Unique ID for the spawned item (will be passed to Factory).</summary>
    public required string ItemId { get; init; }
}
