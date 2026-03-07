using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Domain.Island.Supply;

/// <summary>
/// Leftover fish scraps from eating raw or cooked fish, or occasionally found on the beach.
/// Can be crafted into fishing bait.
/// </summary>
public class CarcassScrapsSupply : SupplyItem
{
    public CarcassScrapsSupply(double quantity)
        : this("carcass_scraps", quantity)
    {
    }

    public CarcassScrapsSupply(string id = "carcass_scraps", double quantity = 0.0)
        : base(id, "supply_carcass_scraps", quantity)
    {
    }
}
