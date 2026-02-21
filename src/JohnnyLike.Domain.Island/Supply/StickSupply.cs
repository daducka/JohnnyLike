namespace JohnnyLike.Domain.Island.Supply;

/// <summary>
/// Sticks gathered from the island. Used as crafting material.
/// </summary>
public class StickSupply : SupplyItem
{
    public StickSupply(double quantity)
        : this("stick", quantity)
    {
    }

    public StickSupply(string id = "stick", double quantity = 0.0)
        : base(id, "supply_stick", quantity)
    {
    }
}
