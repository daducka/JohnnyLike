namespace JohnnyLike.Domain.Island.Supply;

/// <summary>
/// Represents wood that can be used for fuel or construction
/// </summary>
public class WoodSupply : SupplyItem
{
    public WoodSupply(string id = "wood", double quantity = 0.0) 
        : base(id, "supply_wood", quantity)
    {
    }
}
