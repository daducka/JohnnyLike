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

    // ISupplyBounty — all method logic comes from the interface's default implementations
    public List<SupplyItem> BountySupplies { get; set; } = new()
    {
        new CoconutSupply("coconut", 5),
        new PalmFrondSupply("palm_frond", 8)
    };

    public Dictionary<string, Dictionary<string, double>> ActiveReservations { get; } = new();

    // Shorthand so internal methods can call ISupplyBounty defaults without explicit casts
    private ISupplyBounty Bounty => this;

    private int _lastDayCount = 0;

    public CoconutTreeItem(string id = "palm_tree")
        : base(id, "palm_tree")
    {
    }

    // ITickableWorldItem
    public IEnumerable<string> GetDependencies() => new[] { "calendar" };

    public List<TraceEvent> Tick(double dtSeconds, IslandWorldState world, double currentTime)
    {
        var calendar = world.GetItem<CalendarItem>("calendar");
        if (calendar != null && calendar.DayCount > _lastDayCount)
        {
            // Regenerate coconuts and fronds daily (capped)
            var coconuts = Bounty.GetOrCreateSupply("coconut", id => new CoconutSupply(id));
            coconuts.Quantity = Math.Min(10, coconuts.Quantity + 3);

            var fronds = Bounty.GetOrCreateSupply("palm_frond", id => new PalmFrondSupply(id));
            fronds.Quantity = Math.Min(12, fronds.Quantity + 4);

            _lastDayCount = calendar.DayCount;
        }
        return new List<TraceEvent>();
    }

    // IIslandActionCandidate
    public void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        var coconutsAvailable = Bounty.GetQuantity<CoconutSupply>("coconut");
        var frondsAvailable = Bounty.GetQuantity<PalmFrondSupply>("palm_frond");

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

        // Shared context captured by both PreAction and EffectHandler lambdas.
        BountyCollectionContext? bountyCtx = null;
        var actorKey = ctx.ActorId.Value;

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
            PreAction: new Func<EffectContext, bool>(_ =>
            {
                // Reserve the MAX possible payout (CriticalSuccess = 2 coconuts + 2 fronds).
                var availCoconuts = Bounty.GetQuantity<CoconutSupply>("coconut");
                var availFronds   = Bounty.GetQuantity<PalmFrondSupply>("palm_frond");
                if (availCoconuts < 1.0 || availFronds < 1.0) return false;

                Bounty.ReserveSupply<CoconutSupply>(actorKey,   "coconut",     Math.Min(availCoconuts, 2.0));
                Bounty.ReserveSupply<PalmFrondSupply>(actorKey, "palm_frond",  Math.Min(availFronds,   2.0));

                bountyCtx = new BountyCollectionContext(Bounty, actorKey);
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
                var sharedPile = effectCtx.World.SharedSupplyPile;
                if (sharedPile == null) { src.ReleaseReservation(key); return; }

                switch (tier)
                {
                    case RollOutcomeTier.CriticalSuccess:
                        src.CommitReservation(key, "coconut",    2.0, sharedPile, id => new CoconutSupply(id));
                        src.CommitReservation(key, "palm_frond", 2.0, sharedPile, id => new PalmFrondSupply(id));
                        effectCtx.Actor.Morale += 5.0;
                        break;

                    case RollOutcomeTier.Success:
                        src.CommitReservation(key, "coconut",    1.0, sharedPile, id => new CoconutSupply(id));
                        src.CommitReservation(key, "palm_frond", 1.0, sharedPile, id => new PalmFrondSupply(id));
                        effectCtx.Actor.Morale += 3.0;
                        break;

                    default: // PartialSuccess / Failure / CriticalFailure — wasted
                        src.ReleaseReservation(key);
                        if (tier == RollOutcomeTier.PartialSuccess) effectCtx.Actor.Morale += 2.0;
                        else if (tier == RollOutcomeTier.CriticalFailure) effectCtx.Actor.Morale -= 5.0;
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
