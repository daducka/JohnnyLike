using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Supply;
using System.Text.Json;

namespace JohnnyLike.Domain.Island.Items;

/// <summary>
/// Represents the ocean surrounding the island.
/// Maintains a FishSupply bounty that regenerates over time.
/// The fishing action consumes fish from this bounty.
/// </summary>
public class OceanItem : WorldItem, ITickableWorldItem, ISupplyBounty
{
    // ISupplyBounty — all method logic comes from the interface's default implementations
    public List<SupplyItem> BountySupplies { get; set; } = new()
    {
        new FishSupply("fish", 100)
    };

    // Shorthand so internal methods can call ISupplyBounty defaults without explicit casts
    private ISupplyBounty Bounty => this;

    public double FishRegenRatePerMinute { get; set; } = 5.0;

    public OceanItem(string id = "ocean") : base(id, "ocean") { }

    // ITickableWorldItem
    public IEnumerable<string> GetDependencies() => new[] { "calendar" };

    public List<TraceEvent> Tick(double dtSeconds, IslandWorldState world, double currentTime)
    {
        var fish = Bounty.GetSupply<FishSupply>("fish");
        if (fish != null)
        {
            var oldAmount = fish.Quantity;
            fish.Quantity = Math.Min(100.0, fish.Quantity + FishRegenRatePerMinute * (dtSeconds / 60.0));

            if (fish.Quantity - oldAmount >= 1.0)
            {
                return new List<TraceEvent>
                {
                    new TraceEvent(currentTime, null, "FishRegenerated", new Dictionary<string, object>
                    {
                        ["oldAvailable"] = Math.Round(oldAmount, 2),
                        ["newAvailable"] = Math.Round(fish.Quantity, 2),
                        ["regenerated"]  = Math.Round(fish.Quantity - oldAmount, 2)
                    })
                };
            }
        }
        return new List<TraceEvent>();
    }

    public override Dictionary<string, object> SerializeToDict()
    {
        var dict = base.SerializeToDict();
        dict["FishRegenRatePerMinute"] = FishRegenRatePerMinute;
        dict["BountySupplies"] = BountySupplies.Select(s => s.SerializeToDict()).ToList();
        return dict;
    }

    public override void DeserializeFromDict(Dictionary<string, JsonElement> data)
    {
        base.DeserializeFromDict(data);
        if (data.TryGetValue("FishRegenRatePerMinute", out var rate))
            FishRegenRatePerMinute = rate.GetDouble();
        if (data.TryGetValue("BountySupplies", out var bountyEl))
        {
            var list = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(bountyEl.GetRawText());
            if (list != null)
            {
                BountySupplies.Clear();
                foreach (var sd in list)
                {
                    var type = sd["Type"].GetString()!;
                    var id = sd["Id"].GetString()!;
                    SupplyItem? supply = type switch
                    {
                        "supply_fish" => new FishSupply(id),
                        _ => null
                    };
                    if (supply != null)
                    {
                        supply.DeserializeFromDict(sd);
                        BountySupplies.Add(supply);
                    }
                }
            }
        }
    }
}
