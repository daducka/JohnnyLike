namespace JohnnyLike.Domain.Island.Supply;

/// <summary>
/// Shells collected from the beach shoreline. Can be used for crafting decorations,
/// signaling, barter, or small tools in future recipes.
/// </summary>
public class ShellSupply : SupplyItem
{
    public ShellSupply(double quantity)
        : this("shells", quantity)
    {
    }

    public ShellSupply(string id = "shells", double quantity = 0.0)
        : base(id, "supply_shells", quantity)
    {
    }
}
