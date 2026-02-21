namespace JohnnyLike.Domain.Island.Supply;

/// <summary>
/// Rocks gathered from the beach. Used as crafting material.
/// </summary>
public class RocksSupply : SupplyItem
{
    public RocksSupply(string id = "rocks", double quantity = 0.0)
        : base(id, "supply_rocks", quantity)
    {
    }
}
