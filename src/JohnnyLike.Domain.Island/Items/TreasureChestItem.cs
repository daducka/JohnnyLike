using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Kit.Dice;
using System.Text.Json;

namespace JohnnyLike.Domain.Island.Items;

public class TreasureChestItem : WorldItem, IIslandActionCandidate
{
    private const double DC_MIN = 10.0;
    private const double DC_MAX = 20.0;
    private static readonly ResourceId TreasureChestResource = new("island:resource:treasure_chest");

    public bool IsOpened { get; set; } = false;
    public double Health { get; set; } = 100.0;
    public string Position { get; set; } = "shore";

    public TreasureChestItem(string id = "treasure_chest") 
        : base(id, "treasure_chest")
    {
    }

    public void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        // Only emit candidate when chest is present and unopened
        if (IsOpened)
            return;

        // Calculate DC based on chest health
        var healthRatio = Health / 100.0;
        var baseDC = (int)(DC_MIN + healthRatio * (DC_MAX - DC_MIN));

        // Roll skill check at candidate generation time
        var parameters = ctx.RollSkillCheck(SkillType.Athletics, baseDC);

        var baseScore = 0.6; // High priority for treasure

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("bash_open_treasure_chest"),
                ActionKind.Interact,
                parameters,
                20.0 + ctx.Random.NextDouble() * 5.0,
                parameters.ToResultData(),
                new List<ResourceRequirement> { new ResourceRequirement(TreasureChestResource) }
            ),
            baseScore,
            $"Bash open treasure chest (DC {baseDC}, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})",
            EffectHandler: new Action<EffectContext>(effectCtx =>
            {
                if (effectCtx.Tier == null)
                    return;

                var tier = effectCtx.Tier.Value;

                // Common effect: consume energy
                effectCtx.Actor.Energy -= 15.0;

                // Success: open and remove chest, grant reward
                if (tier >= RollOutcomeTier.Success)
                {
                    IsOpened = true;
                    Health = 0.0;
                    effectCtx.World.WorldItems.Remove(this);

                    // Placeholder reward
                    effectCtx.Actor.Morale += 20.0;

                    if (effectCtx.Outcome.ResultData != null)
                    {
                        effectCtx.Outcome.ResultData["variant_id"] = "bash_chest_success";
                        effectCtx.Outcome.ResultData["loot_placeholder"] = true;
                    }
                }
                // Failure: damage chest, keep it in world
                else
                {
                    double damage = tier switch
                    {
                        RollOutcomeTier.CriticalFailure => 40.0,
                        RollOutcomeTier.PartialSuccess => 25.0,
                        RollOutcomeTier.Failure => 15.0,
                        _ => 0.0
                    };

                    Health = Math.Max(0.0, Health - damage);

                    // Small morale penalty for failing
                    effectCtx.Actor.Morale -= 5.0;

                    if (effectCtx.Outcome.ResultData != null)
                    {
                        effectCtx.Outcome.ResultData["variant_id"] = "bash_chest_failure";
                        effectCtx.Outcome.ResultData["chest_health_after"] = Health;
                    }
                }
            })
        ));
    }

    public override Dictionary<string, object> SerializeToDict()
    {
        var dict = base.SerializeToDict();
        dict["IsOpened"] = IsOpened;
        dict["Health"] = Health;
        dict["Position"] = Position;
        return dict;
    }

    public override void DeserializeFromDict(Dictionary<string, JsonElement> data)
    {
        base.DeserializeFromDict(data);
        IsOpened = data["IsOpened"].GetBoolean();
        Health = data["Health"].GetDouble();
        Position = data["Position"].GetString()!;
    }
}
