namespace JohnnyLike.Domain.Island.Supply;

/// <summary>Metal scraps salvaged from wreckage. Used as a crafting material.</summary>
public class MetalScrapSupply : SupplyItem
{
    public MetalScrapSupply(double quantity) : this("metal_scrap", quantity) { }
    public MetalScrapSupply(string id = "metal_scrap", double quantity = 0.0)
        : base(id, "supply_metal_scrap", quantity) { }
}
