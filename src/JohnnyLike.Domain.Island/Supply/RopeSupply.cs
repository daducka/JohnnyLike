namespace JohnnyLike.Domain.Island.Supply;

/// <summary>
/// Rope crafted from palm fronds. Used as a crafting material.
/// </summary>
public class RopeSupply : SupplyItem
{
    public RopeSupply(double quantity)
        : this("rope", quantity)
    {
    }

    public RopeSupply(string id = "rope", double quantity = 0.0)
        : base(id, "supply_rope", quantity)
    {
    }
}
