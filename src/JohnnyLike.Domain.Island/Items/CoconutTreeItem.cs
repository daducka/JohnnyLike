using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Supply;
using JohnnyLike.Domain.Kit.Dice;
using System.Text.Json;

namespace JohnnyLike.Domain.Island.Items;

/// <summary>
/// Represents a coconut palm tree that can be shaken for coconuts.
/// Tracks its own coconut availability and regenerates daily via CalendarItem.
/// </summary>
public class CoconutTreeItem : WorldItem, IIslandActionCandidate, ITickableWorldItem
{
    private static readonly ResourceId PalmTreeResource = new("island:resource:palm_tree");

    public int CoconutsAvailable { get; set; } = 5;
    private int _lastDayCount = 0;

    public CoconutTreeItem(string id = "palm_tree")
        : base(id, "palm_tree")
    {
    }

    public IEnumerable<string> GetDependencies() => new[] { "calendar" };

    public List<TraceEvent> Tick(double dtSeconds, IslandWorldState world, double currentTime)
    {
        var calendar = world.GetItem<CalendarItem>("calendar");
        if (calendar != null && calendar.DayCount > _lastDayCount)
        {
            CoconutsAvailable = Math.Min(10, CoconutsAvailable + 3);
            _lastDayCount = calendar.DayCount;
        }
        return new List<TraceEvent>();
    }

    public void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        if (CoconutsAvailable < 1)
            return;

        var baseDC = 12;

        if (CoconutsAvailable >= 5)
            baseDC -= 2;
        else if (CoconutsAvailable <= 2)
            baseDC += 2;

        // Roll skill check at candidate generation time
        var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC);

        var baseScore = 0.4 + ((100.0 - ctx.Actor.Satiety) / 150.0);
        if (ctx.Actor.Satiety < 30.0)
        {
            baseScore = 0.9;
        }

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
            EffectHandler: new Action<EffectContext>(effectCtx =>
            {
                if (effectCtx.Tier == null)
                    return;

                var tier = effectCtx.Tier.Value;
                var tree = effectCtx.World.GetItem<CoconutTreeItem>(Id);
                if (tree == null) return;

                var sharedPile = effectCtx.World.SharedSupplyPile;

                switch (tier)
                {
                    case RollOutcomeTier.CriticalSuccess:
                        tree.CoconutsAvailable = Math.Max(0, tree.CoconutsAvailable - 2);
                        sharedPile?.AddSupply("coconut", 2.0, id => new CoconutSupply(id));
                        break;

                    case RollOutcomeTier.Success:
                        tree.CoconutsAvailable = Math.Max(0, tree.CoconutsAvailable - 1);
                        sharedPile?.AddSupply("coconut", 1.0, id => new CoconutSupply(id));
                        break;

                    case RollOutcomeTier.PartialSuccess:
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
        dict["CoconutsAvailable"] = CoconutsAvailable;
        dict["LastDayCount"] = _lastDayCount;
        return dict;
    }

    public override void DeserializeFromDict(Dictionary<string, JsonElement> data)
    {
        base.DeserializeFromDict(data);
        if (data.TryGetValue("CoconutsAvailable", out var ca)) CoconutsAvailable = ca.GetInt32();
        if (data.TryGetValue("LastDayCount", out var ldc)) _lastDayCount = ldc.GetInt32();
    }
}
