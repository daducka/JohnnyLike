using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Supply;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Items;

public enum TideLevel { Low, High }

public class BeachItem : WorldItem, ITickableWorldItem, IIslandActionCandidate, ISupplyBounty
{
    private static readonly ResourceId BeachResource = new("island:resource:beach");

    // ISupplyBounty — all method logic comes from the interface's default implementations
    public List<SupplyItem> BountySupplies { get; set; } = new()
    {
        new StickSupply("sticks", 10),
        new WoodSupply("driftwood", 10),
        new RocksSupply("rocks", 5)
    };

    public Dictionary<string, Dictionary<string, double>> ActiveReservations { get; } = new();

    // Shorthand so internal methods can call ISupplyBounty defaults without explicit casts
    private ISupplyBounty Bounty => this;

    public TideLevel Tide { get; private set; }

    public BeachItem(string id = "beach") : base(id, "beach") { }

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

        Bounty.AddSupply("sticks", regenRate * dtSeconds, id => new StickSupply(id));
        Bounty.AddSupply("driftwood", regenRate * dtSeconds, id => new WoodSupply(id));
        Bounty.AddSupply("rocks", regenRate * dtSeconds * 0.5, id => new RocksSupply(id));

        return new List<TraceEvent>();
    }

    // IIslandActionCandidate
    public void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        // Only offer explore_beach when there's enough bounty to get at least a partial result
        var sticks = Bounty.GetQuantity<StickSupply>("sticks");
        var driftwood = Bounty.GetQuantity<WoodSupply>("driftwood");
        if (sticks < 2.0 || driftwood < 2.0)
            return;

        var baseDC = Tide == TideLevel.High ? 12 : 8;
        var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC);

        // Both lambdas capture the same variable so the reservation key survives the
        // Director's resource-release call that happens before ApplyActionEffects.
        BountyCollectionContext? bountyCtx = null;
        ISupplyBounty source = this;
        var actorKey = ctx.ActorId.Value;

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
            PreAction: new Func<EffectContext, bool>(_ =>
            {
                // Reserve the MAX possible payout (CriticalSuccess upper-bound) so other actors
                // see reduced availability. The actual payout is committed in the EffectHandler.
                var availSticks = source.GetQuantity<StickSupply>("sticks");
                var availDriftwood = source.GetQuantity<WoodSupply>("driftwood");
                if (availSticks < 1.0 || availDriftwood < 1.0) return false;

                source.ReserveSupply<StickSupply>(actorKey, "sticks",    Math.Min(availSticks, 4.0));
                source.ReserveSupply<WoodSupply>(actorKey,  "driftwood", Math.Min(availDriftwood, 4.0));
                var availRocks = source.GetQuantity<RocksSupply>("rocks");
                if (availRocks > 0)
                    source.ReserveSupply<RocksSupply>(actorKey, "rocks", Math.Min(availRocks, 2.0));

                bountyCtx = new BountyCollectionContext(source, actorKey);
                return true;
            }),
            EffectHandler: new Action<EffectContext>(effectCtx =>
            {
                if (effectCtx.Tier == null || bountyCtx == null)
                {
                    bountyCtx?.Source.ReleaseReservation(bountyCtx.ReservationKey);
                    return;
                }

                var tier = effectCtx.Tier.Value;
                var src = bountyCtx.Source;
                var key = bountyCtx.ReservationKey;
                var pile = effectCtx.World.SharedSupplyPile;
                if (pile == null) { src.ReleaseReservation(key); return; }

                switch (tier)
                {
                    case RollOutcomeTier.CriticalSuccess:
                        src.CommitReservation(key, "sticks",    4.0, pile, id => new StickSupply(id));
                        src.CommitReservation(key, "driftwood", 4.0, pile, id => new WoodSupply(id));
                        src.CommitReservation(key, "rocks",     2.0, pile, id => new RocksSupply(id));
                        effectCtx.Actor.Morale += 8.0;
                        effectCtx.Actor.Energy -= 8.0;
                        break;

                    case RollOutcomeTier.Success:
                        src.CommitReservation(key, "sticks",    2.0, pile, id => new StickSupply(id));
                        src.CommitReservation(key, "driftwood", 2.0, pile, id => new WoodSupply(id));
                        src.CommitReservation(key, "rocks",     1.0, pile, id => new RocksSupply(id));
                        effectCtx.Actor.Morale += 5.0;
                        effectCtx.Actor.Energy -= 10.0;
                        break;

                    case RollOutcomeTier.PartialSuccess:
                        src.CommitReservation(key, "sticks",    1.0, pile, id => new StickSupply(id));
                        src.CommitReservation(key, "driftwood", 1.0, pile, id => new WoodSupply(id));
                        src.ReleaseReservation(key); // return reserved rocks (not committed at this tier)
                        effectCtx.Actor.Morale += 2.0;
                        effectCtx.Actor.Energy -= 12.0;
                        break;

                    default: // Failure or CriticalFailure: everything returned
                        src.ReleaseReservation(key);
                        effectCtx.Actor.Energy -= tier == RollOutcomeTier.Failure ? 12.0 : 15.0;
                        if (tier == RollOutcomeTier.CriticalFailure)
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
