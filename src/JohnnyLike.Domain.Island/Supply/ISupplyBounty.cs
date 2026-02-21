namespace JohnnyLike.Domain.Island.Supply;

/// <summary>
/// Generalized supply management interface shared by SupplyPile, BeachItem, CoconutTreeItem, OceanItem, etc.
/// </summary>
public interface ISupplyBounty
{
    T? GetSupply<T>(string supplyId) where T : SupplyItem;
    T GetOrCreateSupply<T>(string supplyId, Func<string, T> factory) where T : SupplyItem;
    void AddSupply<T>(string supplyId, double quantity, Func<string, T> factory) where T : SupplyItem;
    bool TryConsumeSupply<T>(string supplyId, double quantity) where T : SupplyItem;
    double GetQuantity<T>(string supplyId) where T : SupplyItem;
}
