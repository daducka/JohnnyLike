namespace JohnnyLike.Domain.Island.Supply;

/// <summary>
/// Generalized supply management interface shared by SupplyPile, BeachItem, CoconutTreeItem, OceanItem, etc.
/// Implementors only need to expose <see cref="BountySupplies"/>; all method logic is provided as
/// default interface implementations so there is no duplication across classes.
/// </summary>
public interface ISupplyBounty
{
    /// <summary>The backing list of supply items managed by this bounty.</summary>
    List<SupplyItem> BountySupplies { get; }

    T? GetSupply<T>(string supplyId) where T : SupplyItem
        => BountySupplies.FirstOrDefault(s => s.Id == supplyId) as T;

    T GetOrCreateSupply<T>(string supplyId, Func<string, T> factory) where T : SupplyItem
    {
        var existing = GetSupply<T>(supplyId);
        if (existing != null) return existing;
        var newSupply = factory(supplyId);
        BountySupplies.Add(newSupply);
        return newSupply;
    }

    void AddSupply<T>(string supplyId, double quantity, Func<string, T> factory) where T : SupplyItem
        => GetOrCreateSupply(supplyId, factory).Quantity += quantity;

    bool TryConsumeSupply<T>(string supplyId, double quantity) where T : SupplyItem
    {
        var supply = GetSupply<T>(supplyId);
        if (supply == null || supply.Quantity < quantity) return false;
        supply.Quantity -= quantity;
        return true;
    }

    double GetQuantity<T>(string supplyId) where T : SupplyItem
        => GetSupply<T>(supplyId)?.Quantity ?? 0.0;
}
