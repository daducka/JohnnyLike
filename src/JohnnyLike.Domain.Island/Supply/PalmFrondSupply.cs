namespace JohnnyLike.Domain.Island.Supply;

/// <summary>
/// Palm fronds gathered from coconut trees. Used as crafting material.
/// </summary>
public class PalmFrondSupply : SupplyItem
{
    public PalmFrondSupply(double quantity)
        : this("palm_frond", quantity)
    {
    }

    public PalmFrondSupply(string id = "palm_frond", double quantity = 0.0)
        : base(id, "supply_palm_frond", quantity)
    {
    }
}
