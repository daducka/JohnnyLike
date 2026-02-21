using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Supply;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Items;

public enum TideLevel { Low, High }

public class BeachItem : WorldItem, ITickableWorldItem, IIslandActionCandidate, ISupplyBounty
{
    private static readonly ResourceId BeachResource = new("island:resource:beach");

    public List<SupplyItem> BountySupplies { get; set; } = new()
    {
        new StickSupply("sticks", 10),
        new WoodSupply("driftwood", 10),
        new RocksSupply("rocks", 5)
    };

    public TideLevel Tide { get; private set; }

    public BeachItem(string id = "beach") : base(id, "beach") { }

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
        var weather = world.GetItem<WeatherItem>("weather");

        if (calendar == null)
            return new List<TraceEvent>();

        var tidePhase = calendar.HourOfDay % 12;
        Tide = tidePhase >= 6 ? TideLevel.High : TideLevel.Low;

        double regenRate = 0.1;

        if (Tide == TideLevel.High)
            regenRate *= 2;

        if (weather?.Temperature == TemperatureBand.Cold)
            regenRate *= 1.2;

        AddSupply("sticks", regenRate * dtSeconds, id => new StickSupply(id));
        AddSupply("driftwood", regenRate * dtSeconds, id => new WoodSupply(id));
        AddSupply("rocks", regenRate * dtSeconds * 0.5, id => new RocksSupply(id));

        return new List<TraceEvent>();
    }

    // IIslandActionCandidate
    public void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        // Only offer explore_beach when there's enough bounty to get at least a partial result
        var sticks = GetQuantity<StickSupply>("sticks");
        var driftwood = GetQuantity<WoodSupply>("driftwood");
        if (sticks < 2.0 || driftwood < 2.0)
            return;

        var baseDC = Tide == TideLevel.High ? 12 : 8;
        var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC);

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("explore_beach"),
                ActionKind.Interact,
                parameters,
                20.0 + ctx.Random.NextDouble() * 10.0,
                parameters.ToResultData(),
                new List<ResourceRequirement> { new ResourceRequirement(BeachResource) }
            ),
            0.5,
            $"Explore beach (sticks: {sticks:F0}, driftwood: {driftwood:F0}, DC {baseDC}, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})",
            PreAction: new Func<EffectContext, bool>(effectCtx =>
            {
                // Consume the base harvest amount from bounty upfront
                var gotSticks = TryConsumeSupply<StickSupply>("sticks", 2.0);
                var gotDriftwood = TryConsumeSupply<WoodSupply>("driftwood", 2.0);
                var gotRocks = TryConsumeSupply<RocksSupply>("rocks", 1.0);
                return gotSticks && gotDriftwood; // rocks optional
            }),
            EffectHandler: new Action<EffectContext>(effectCtx =>
            {
                if (effectCtx.Tier == null)
                    return;

                var pile = effectCtx.World.SharedSupplyPile;
                if (pile == null) return;

                var tier = effectCtx.Tier.Value;
                var beach = effectCtx.World.GetItem<BeachItem>(Id)!;

                switch (tier)
                {
                    case RollOutcomeTier.CriticalSuccess:
                        // Base 2+2+1 consumed, plus try for bonus from remaining bounty
                        var bonusSticks = beach.TryConsumeSupply<StickSupply>("sticks", 2.0) ? 2 : 0;
                        var bonusDriftwood = beach.TryConsumeSupply<WoodSupply>("driftwood", 2.0) ? 2 : 0;
                        var bonusRocks = beach.TryConsumeSupply<RocksSupply>("rocks", 1.0) ? 1 : 0;
                        pile.AddSupply("sticks", 2 + bonusSticks, id => new StickSupply(id));
                        pile.AddSupply("wood", 2 + bonusDriftwood, id => new WoodSupply(id));
                        pile.AddSupply("rocks", 1 + bonusRocks, id => new RocksSupply(id));
                        effectCtx.Actor.Morale += 8.0;
                        effectCtx.Actor.Energy -= 8.0;
                        break;

                    case RollOutcomeTier.Success:
                        // All consumed base goes to pile
                        pile.AddSupply("sticks", 2, id => new StickSupply(id));
                        pile.AddSupply("wood", 2, id => new WoodSupply(id));
                        pile.AddSupply("rocks", 1, id => new RocksSupply(id));
                        effectCtx.Actor.Morale += 5.0;
                        effectCtx.Actor.Energy -= 10.0;
                        break;

                    case RollOutcomeTier.PartialSuccess:
                        // Partial return — some resources wasted
                        pile.AddSupply("sticks", 1, id => new StickSupply(id));
                        pile.AddSupply("wood", 1, id => new WoodSupply(id));
                        effectCtx.Actor.Morale += 2.0;
                        effectCtx.Actor.Energy -= 12.0;
                        break;

                    case RollOutcomeTier.Failure:
                        effectCtx.Actor.Energy -= 12.0;
                        break;

                    case RollOutcomeTier.CriticalFailure:
                        effectCtx.Actor.Energy -= 15.0;
                        effectCtx.Actor.Morale -= 5.0;
                        break;
                }
            }),
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Preparation] = 0.6,
                [QualityType.ResourcePreservation] = 0.4
            }
        ));
    }

    public override Dictionary<string, object> SerializeToDict()
    {
        var dict = base.SerializeToDict();
        dict["Tide"] = Tide.ToString();
        dict["BountySupplies"] = BountySupplies.Select(s => s.SerializeToDict()).ToList();
        return dict;
    }

    public override void DeserializeFromDict(Dictionary<string, System.Text.Json.JsonElement> data)
    {
        base.DeserializeFromDict(data);
        if (data.TryGetValue("Tide", out var tideEl))
            Tide = Enum.Parse<TideLevel>(tideEl.GetString()!);
        if (data.TryGetValue("BountySupplies", out var bountyEl))
        {
            var list = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, System.Text.Json.JsonElement>>>(bountyEl.GetRawText());
            if (list != null)
            {
                BountySupplies.Clear();
                foreach (var sd in list)
                {
                    var type = sd["Type"].GetString()!;
                    var id = sd["Id"].GetString()!;
                    SupplyItem? supply = type switch
                    {
                        "supply_stick"  => new StickSupply(id),
                        "supply_wood"   => new WoodSupply(id),
                        "supply_rocks"  => new RocksSupply(id),
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
