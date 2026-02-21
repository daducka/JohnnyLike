namespace JohnnyLike.Domain.Island.Supply;

/// <summary>
/// Generalized supply management interface shared by SupplyPile, BeachItem, CoconutTreeItem, OceanItem, etc.
/// Implementors expose <see cref="BountySupplies"/> and <see cref="ActiveReservations"/>; all method
/// logic is provided as default interface implementations so there is no duplication across classes.
/// </summary>
public interface ISupplyBounty
{
    /// <summary>The backing list of supply items managed by this bounty.</summary>
    List<SupplyItem> BountySupplies { get; }

    /// <summary>
    /// Active supply reservations. Key = reservationKey, Value = (supplyId → reserved quantity).
    /// Reserved quantities are deducted from <see cref="BountySupplies"/> immediately so other
    /// actors see a reduced available pool, and are returned on release or discarded on commit.
    /// </summary>
    Dictionary<string, Dictionary<string, double>> ActiveReservations { get; }

    T? GetSupply<T>(string supplyId) where T : SupplyItem
        => (BountySupplies.FirstOrDefault(s => s.Id == supplyId && s is T) as T)
        ?? BountySupplies.OfType<T>().FirstOrDefault();

    T? GetSupply<T>() where T : SupplyItem
        => BountySupplies.OfType<T>().FirstOrDefault();

    T GetOrCreateSupply<T>(string supplyId, Func<string, T> factory) where T : SupplyItem
    {
        var existing = GetSupply<T>(supplyId);
        if (existing != null) return existing;
        var newSupply = factory(supplyId);
        BountySupplies.Add(newSupply);
        return newSupply;
    }

    T GetOrCreateSupply<T>(Func<T> factory) where T : SupplyItem
    {
        var existing = GetSupply<T>();
        if (existing != null) return existing;
        var newSupply = factory();
        BountySupplies.Add(newSupply);
        return newSupply;
    }

    void AddSupply<T>(string supplyId, double quantity, Func<string, T> factory) where T : SupplyItem
        => GetOrCreateSupply(supplyId, factory).Quantity += quantity;

    void AddSupply<T>(double quantity, Func<T> factory) where T : SupplyItem
        => GetOrCreateSupply(factory).Quantity += quantity;

    bool TryConsumeSupply<T>(string supplyId, double quantity) where T : SupplyItem
    {
        var supply = GetSupply<T>(supplyId);
        if (supply == null || supply.Quantity < quantity) return false;
        supply.Quantity -= quantity;
        return true;
    }

    bool TryConsumeSupply<T>(double quantity) where T : SupplyItem
    {
        var supply = GetSupply<T>();
        if (supply == null || supply.Quantity < quantity) return false;
        supply.Quantity -= quantity;
        return true;
    }

    double GetQuantity<T>(string supplyId) where T : SupplyItem
        => GetSupply<T>(supplyId)?.Quantity ?? 0.0;

    double GetQuantity<T>() where T : SupplyItem
        => GetSupply<T>()?.Quantity ?? 0.0;

    // ── Reservation API ────────────────────────────────────────────────────────

    /// <summary>
    /// Reserves <paramref name="quantity"/> of the specified supply under <paramref name="reservationKey"/>,
    /// deducting it from the available pool immediately so other actors cannot claim it.
    /// Returns false if the supply is unavailable in the required quantity.
    /// Multiple supplies can be reserved under the same key by calling this method repeatedly.
    /// </summary>
    bool ReserveSupply<T>(string reservationKey, string supplyId, double quantity) where T : SupplyItem
    {
        var supply = GetSupply<T>(supplyId);
        if (supply == null || supply.Quantity < quantity) return false;
        supply.Quantity -= quantity;
        if (!ActiveReservations.TryGetValue(reservationKey, out var entries))
            ActiveReservations[reservationKey] = entries = new Dictionary<string, double>();
        entries[supplyId] = entries.GetValueOrDefault(supplyId) + quantity;
        return true;
    }

    bool ReserveSupply<T>(string reservationKey, double quantity) where T : SupplyItem
    {
        var supply = GetSupply<T>();
        if (supply == null || supply.Quantity < quantity) return false;
        supply.Quantity -= quantity;
        if (!ActiveReservations.TryGetValue(reservationKey, out var entries))
            ActiveReservations[reservationKey] = entries = new Dictionary<string, double>();
        entries[supply.Id] = entries.GetValueOrDefault(supply.Id) + quantity;
        return true;
    }

    /// <summary>
    /// Transfers up to <paramref name="actualQuantity"/> of <paramref name="supplyId"/> from the
    /// reservation to <paramref name="destination"/>, returning any remainder back to this bounty.
    /// Removes the supply entry from the reservation and cleans up the key when empty.
    /// Safe to call with more than was reserved — commits only what is available.
    /// </summary>
    void CommitReservation<T>(
        string reservationKey,
        string supplyId,
        double actualQuantity,
        ISupplyBounty destination,
        Func<string, T> factory) where T : SupplyItem
    {
        if (!ActiveReservations.TryGetValue(reservationKey, out var entries)) return;
        var reserved = entries.GetValueOrDefault(supplyId, 0.0);
        var committed = Math.Min(actualQuantity, reserved);
        var remainder = reserved - committed;
        if (remainder > 0)
            GetOrCreateSupply(supplyId, factory).Quantity += remainder;
        if (committed > 0)
            destination.AddSupply(supplyId, committed, factory);
        entries.Remove(supplyId);
        if (entries.Count == 0)
            ActiveReservations.Remove(reservationKey);
    }

    void CommitReservation<T>(
        string reservationKey,
        double actualQuantity,
        ISupplyBounty destination,
        Func<T> factory) where T : SupplyItem
    {
        if (!ActiveReservations.TryGetValue(reservationKey, out var entries)) return;

        var supplyId = entries.Keys
            .FirstOrDefault(id => BountySupplies.FirstOrDefault(s => s.Id == id) is T);

        if (supplyId == null)
            return;

        CommitReservation(
            reservationKey,
            supplyId,
            actualQuantity,
            destination,
            _ => factory());
    }

    /// <summary>
    /// Releases all supplies under <paramref name="reservationKey"/> back to this bounty without
    /// transferring to any destination (rollback/cancel path).
    /// </summary>
    void ReleaseReservation(string reservationKey)
    {
        if (!ActiveReservations.TryGetValue(reservationKey, out var entries)) return;
        foreach (var (supplyId, qty) in entries)
        {
            var supply = BountySupplies.FirstOrDefault(s => s.Id == supplyId);
            if (supply != null) supply.Quantity += qty;
        }
        ActiveReservations.Remove(reservationKey);
    }
}
