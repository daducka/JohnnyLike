using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Domain.Island.Supply;

/// <summary>
/// Fishing bait crafted from carcass scraps. Consuming one unit when fishing improves
/// the chance of catching a fish.
/// </summary>
public class BaitSupply : SupplyItem
{
    public BaitSupply(double quantity)
        : this("bait", quantity)
    {
    }

    public BaitSupply(string id = "bait", double quantity = 0.0)
        : base(id, "supply_bait", quantity)
    {
    }
}
