using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Supply;
using JohnnyLike.Domain.Kit.Dice;
using System.Text.Json;

namespace JohnnyLike.Domain.Island.Items;

/// <summary>
/// Represents a coconut palm tree. Owns its own CoconutSupply and PalmFrondSupply
/// bounty that regenerates daily via CalendarItem.
/// </summary>
public class CoconutTreeItem : WorldItem, IIslandActionCandidate, ITickableWorldItem, ISupplyBounty
{
    private static readonly ResourceId PalmTreeResource = new("island:resource:palm_tree");

    public List<SupplyItem> BountySupplies { get; set; } = new()
    {
        new CoconutSupply("coconut", 5),
        new PalmFrondSupply("palm_frond", 8)
    };

    private int _lastDayCount = 0;

    public CoconutTreeItem(string id = "palm_tree")
        : base(id, "palm_tree")
    {
    }

    // ISupplyBounty
    public T? GetSupply<T>(string supplyId) where T : SupplyItem
        => BountySupplies.FirstOrDefault(s => s.Id == supplyId) as T;

    public T GetOrCreateSupply<T>(string supplyId, Func<string, T> factory) where T : SupplyItem
    {
        var existing = GetSupply<T>(supplyId);
        if (existing != null) return existing;
        var newSupply = factory(supplyId);
        BountySupplies.Add(newSupply);
        return newSupply;
    }

    public void AddSupply<T>(string supplyId, double quantity, Func<string, T> factory) where T : SupplyItem
    {
        var supply = GetOrCreateSupply(supplyId, factory);
        supply.Quantity += quantity;
    }

    public bool TryConsumeSupply<T>(string supplyId, double quantity) where T : SupplyItem
    {
        var supply = GetSupply<T>(supplyId);
        if (supply == null || supply.Quantity < quantity) return false;
        supply.Quantity -= quantity;
        return true;
    }

    public double GetQuantity<T>(string supplyId) where T : SupplyItem
        => GetSupply<T>(supplyId)?.Quantity ?? 0.0;

    // ITickableWorldItem
    public IEnumerable<string> GetDependencies() => new[] { "calendar" };

    public List<TraceEvent> Tick(double dtSeconds, IslandWorldState world, double currentTime)
    {
        var calendar = world.GetItem<CalendarItem>("calendar");
        if (calendar != null && calendar.DayCount > _lastDayCount)
        {
            // Regenerate coconuts and fronds daily
            var coconuts = GetSupply<CoconutSupply>("coconut");
            if (coconuts != null)
                coconuts.Quantity = Math.Min(10, coconuts.Quantity + 3);
            else
                BountySupplies.Add(new CoconutSupply("coconut", 3));

            var fronds = GetSupply<PalmFrondSupply>("palm_frond");
            if (fronds != null)
                fronds.Quantity = Math.Min(12, fronds.Quantity + 4);
            else
                BountySupplies.Add(new PalmFrondSupply("palm_frond", 4));

            _lastDayCount = calendar.DayCount;
        }
        return new List<TraceEvent>();
    }

    // IIslandActionCandidate
    public void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        var coconutsAvailable = GetQuantity<CoconutSupply>("coconut");
        var frondsAvailable = GetQuantity<PalmFrondSupply>("palm_frond");

        if (coconutsAvailable < 1.0 || frondsAvailable < 1.0)
            return;

        var baseDC = 12;

        if (coconutsAvailable >= 5)
            baseDC -= 2;
        else if (coconutsAvailable <= 2)
            baseDC += 2;

        var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC);

        var baseScore = 0.4 + ((100.0 - ctx.Actor.Satiety) / 150.0);
        if (ctx.Actor.Satiety < 30.0)
            baseScore = 0.9;

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("shake_tree_coconut"),
                ActionKind.Interact,
                parameters,
                10.0 + ctx.Random.NextDouble() * 5.0,
                parameters.ToResultData(),
                new List<ResourceRequirement> { new ResourceRequirement(PalmTreeResource) }
            ),
            baseScore,
            $"Get coconut (DC {baseDC}, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})",
            PreAction: new Func<EffectContext, bool>(effectCtx =>
            {
                // Reserve 1 coconut and 1 frond from tree bounty upfront
                var tree = effectCtx.World.GetItem<CoconutTreeItem>(Id);
                if (tree == null) return false;
                return tree.TryConsumeSupply<CoconutSupply>("coconut", 1.0)
                    && tree.TryConsumeSupply<PalmFrondSupply>("palm_frond", 1.0);
            }),
            EffectHandler: new Action<EffectContext>(effectCtx =>
            {
                if (effectCtx.Tier == null)
                    return;

                var tier = effectCtx.Tier.Value;
                var tree = effectCtx.World.GetItem<CoconutTreeItem>(Id);
                var sharedPile = effectCtx.World.SharedSupplyPile;

                switch (tier)
                {
                    case RollOutcomeTier.CriticalSuccess:
                        // Transfer base 1+1 consumed, plus try for bonus from remaining bounty
                        var bonusCoconut = tree?.TryConsumeSupply<CoconutSupply>("coconut", 1.0) ?? false;
                        var bonusFrond = tree?.TryConsumeSupply<PalmFrondSupply>("palm_frond", 1.0) ?? false;
                        sharedPile?.AddSupply("coconut", bonusCoconut ? 2.0 : 1.0, id => new CoconutSupply(id));
                        sharedPile?.AddSupply("palm_frond", bonusFrond ? 2.0 : 1.0, id => new PalmFrondSupply(id));
                        effectCtx.Actor.Morale += 5.0;
                        break;

                    case RollOutcomeTier.Success:
                        sharedPile?.AddSupply("coconut", 1.0, id => new CoconutSupply(id));
                        sharedPile?.AddSupply("palm_frond", 1.0, id => new PalmFrondSupply(id));
                        effectCtx.Actor.Morale += 3.0;
                        break;

                    case RollOutcomeTier.PartialSuccess:
                        // Consumed resources wasted (fell in the sea), actor is amused
                        effectCtx.Actor.Morale += 2.0;
                        break;

                    case RollOutcomeTier.Failure:
                        break;

                    case RollOutcomeTier.CriticalFailure:
                        effectCtx.Actor.Morale -= 5.0;
                        break;
                }
            })
        ));
    }

    public override Dictionary<string, object> SerializeToDict()
    {
        var dict = base.SerializeToDict();
        dict["LastDayCount"] = _lastDayCount;
        dict["BountySupplies"] = BountySupplies.Select(s => s.SerializeToDict()).ToList();
        return dict;
    }

    public override void DeserializeFromDict(Dictionary<string, JsonElement> data)
    {
        base.DeserializeFromDict(data);
        if (data.TryGetValue("LastDayCount", out var ldc)) _lastDayCount = ldc.GetInt32();
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
                        "supply_coconut"    => new CoconutSupply(id),
                        "supply_palm_frond" => new PalmFrondSupply(id),
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
