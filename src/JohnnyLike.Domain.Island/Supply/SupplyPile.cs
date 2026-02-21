using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using System.Text.Json;

namespace JohnnyLike.Domain.Island.Supply;

/// <summary>
/// Represents a pile of supplies with generic methods to manage different supply types
/// </summary>
public class SupplyPile : WorldItem, IIslandActionCandidate, ISupplyBounty
{
    public List<SupplyItem> Supplies { get; set; } = new();
    public string AccessControl { get; set; }

    // ISupplyBounty: route through the Supplies list so default interface methods work too
    List<SupplyItem> ISupplyBounty.BountySupplies => Supplies;

    public SupplyPile(string id, string accessControl = "shared") 
        : base(id, "supply_pile")
    {
        AccessControl = accessControl;
    }

    /// <summary>
    /// Gets a specific supply by ID, or null if not found
    /// </summary>
    public T? GetSupply<T>(string supplyId) where T : SupplyItem
    {
        return Supplies.FirstOrDefault(s => s.Id == supplyId) as T;
    }

    /// <summary>
    /// Gets a supply by ID, or creates it using the factory if it doesn't exist
    /// </summary>
    public T GetOrCreateSupply<T>(string supplyId, Func<string, T> factory) where T : SupplyItem
    {
        var existing = GetSupply<T>(supplyId);
        if (existing != null)
            return existing;

        var newSupply = factory(supplyId);
        Supplies.Add(newSupply);
        return newSupply;
    }

    /// <summary>
    /// Adds quantity to a supply, creating it if it doesn't exist
    /// </summary>
    public void AddSupply<T>(string supplyId, double quantity, Func<string, T> factory) where T : SupplyItem
    {
        var supply = GetOrCreateSupply(supplyId, factory);
        supply.Quantity += quantity;
    }

    /// <summary>
    /// Attempts to consume a specific quantity of a supply
    /// Returns true if successful, false if insufficient quantity
    /// </summary>
    public bool TryConsumeSupply<T>(string supplyId, double quantity) where T : SupplyItem
    {
        var supply = GetSupply<T>(supplyId);
        if (supply == null || supply.Quantity < quantity)
            return false;

        supply.Quantity -= quantity;
        return true;
    }

    /// <summary>
    /// Gets the quantity of a specific supply (returns 0 if not found)
    /// </summary>
    public double GetQuantity<T>(string supplyId) where T : SupplyItem
    {
        var supply = GetSupply<T>(supplyId);
        return supply?.Quantity ?? 0.0;
    }

    /// <summary>
    /// Checks if an actor can access this supply pile
    /// </summary>
    public bool CanAccess(ActorId actorId)
    {
        // For now, just return true for shared piles
        return AccessControl == "shared";
    }

    /// <summary>
    /// Provides action candidates from all supply items that implement ISupplyActionCandidate.
    /// </summary>
    public void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        if (!CanAccess(ctx.ActorId))
            return;

        foreach (var supply in Supplies)
        {
            if (supply is ISupplyActionCandidate candidate)
                candidate.AddCandidates(ctx, this, output);
        }
    }

    public override Dictionary<string, object> SerializeToDict()
    {
        var dict = base.SerializeToDict();
        dict["AccessControl"] = AccessControl;
        dict["Supplies"] = Supplies.Select(s => s.SerializeToDict()).ToList();
        return dict;
    }

    public override void DeserializeFromDict(Dictionary<string, JsonElement> data)
    {
        base.DeserializeFromDict(data);
        AccessControl = data["AccessControl"].GetString()!;

        Supplies.Clear();
        if (data.TryGetValue("Supplies", out var suppliesElement))
        {
            var suppliesList = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(suppliesElement.GetRawText());
            if (suppliesList != null)
            {
                foreach (var supplyData in suppliesList)
                {
                    var type = supplyData["Type"].GetString()!;
                    var id = supplyData["Id"].GetString()!;

                    SupplyItem? supply = type switch
                    {
                        "supply_wood"        => new WoodSupply(id),
                        "supply_fish"        => new FishSupply(id),
                        "supply_cooked_fish" => new CookedFishSupply(id),
                        "supply_coconut"     => new CoconutSupply(id),
                        "supply_stick"       => new StickSupply(id),
                        "supply_palm_frond"  => new PalmFrondSupply(id),
                        "supply_rocks"       => new RocksSupply(id),
                        _ => null
                    };

                    if (supply != null)
                    {
                        supply.DeserializeFromDict(supplyData);
                        Supplies.Add(supply);
                    }
                }
            }
        }
    }
}
