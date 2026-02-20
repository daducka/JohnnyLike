namespace JohnnyLike.Domain.Island.Supply;

/// <summary>
/// A crafted umbrella made from sticks and palm fronds. Provides shelter from rain.
/// </summary>
public class UmbrellaSupply : SupplyItem
{
    public UmbrellaSupply(string id = "umbrella", double quantity = 0.0)
        : base(id, "supply_umbrella", quantity)
    {
    }
}
