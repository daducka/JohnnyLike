using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Supply;
using System.Text.Json;

namespace JohnnyLike.Domain.Island.Items;

public class OceanItem : WorldItem, ITickableWorldItem, ISupplyBounty
{
    public List<SupplyItem> BountySupplies { get; set; } = new() { new FishSupply(100) };
    public Dictionary<string, Dictionary<string, double>> ActiveReservations { get; } = new();
    private ISupplyBounty Bounty => this;
    public double FishRegenRatePerMinute { get; set; } = 5.0;
    private long _lastTick = 0;

    public OceanItem(string id = "ocean") : base(id, "ocean") { }

    public IEnumerable<string> GetDependencies() => new[] { "calendar" };

    public List<TraceEvent> Tick(long currentTick, WorldState worldState)
    {
        var dtTicks = currentTick - _lastTick;
        _lastTick = currentTick;
        var dtSeconds = (double)dtTicks / 20.0;

        var fish = Bounty.GetSupply<FishSupply>();
        if (fish != null)
        {
            var oldAmount = fish.Quantity;
            fish.Quantity = Math.Min(100.0, fish.Quantity + FishRegenRatePerMinute * (dtSeconds / 60.0));

            if (fish.Quantity - oldAmount >= 1.0)
            {
                return new List<TraceEvent>
                {
                    new TraceEvent(currentTick, null, "FishRegenerated", new Dictionary<string, object>
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
        dict["LastTick"] = _lastTick;
        return dict;
    }

    public override void DeserializeFromDict(Dictionary<string, JsonElement> data)
    {
        base.DeserializeFromDict(data);
        if (data.TryGetValue("FishRegenRatePerMinute", out var rate))
            FishRegenRatePerMinute = rate.GetDouble();
        if (data.TryGetValue("LastTick", out var lt)) _lastTick = lt.GetInt64();
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
