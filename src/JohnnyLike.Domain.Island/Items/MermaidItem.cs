using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Kit.Dice;
using System.Text.Json;

namespace JohnnyLike.Domain.Island.Items;

/// <summary>
/// Represents a mermaid at the shore.
/// Provides "Wave at Mermaid" action and sticks around for a limited time before leaving.
/// </summary>
public class MermaidItem : MaintainableWorldItem
{
    private static readonly ResourceId ShoreEastEnd = new("island:resource:shore:east_end");
    
    public double ExpiresAt { get; set; } = 0.0;
    
    public MermaidItem(string id = "mermaid")
        : base(id, "mermaid", baseDecayPerSecond: 0.0)
    {
    }

    public override void Tick(double dtSeconds, IslandWorldState world)
    {
        base.Tick(dtSeconds, world);
        
        // Mark as expired when time is up
        if (world.CurrentTime >= ExpiresAt)
        {
            IsExpired = true;
        }
    }

    public override void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        var baseDC = 10;
        var parameters = ctx.RollSkillCheck(SkillType.Performance, baseDC);
        var baseScore = 0.6; // High priority - mermaid won't be here long!

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("wave_at_mermaid"),
                ActionKind.Interact,
                parameters,
                5.0,
                parameters.ToResultData(),
                new List<ResourceRequirement> { new ResourceRequirement(ShoreEastEnd) }
            ),
            baseScore,
            $"Wave at mermaid (DC {baseDC}, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})",
            EffectHandler: new Action<EffectContext>(effectCtx =>
            {
                if (effectCtx.Tier == null)
                    return;

                var tier = effectCtx.Tier.Value;

                // Positive impact on morale for successful interaction
                if (tier >= RollOutcomeTier.Success)
                {
                    effectCtx.Actor.Morale = Math.Min(100.0, effectCtx.Actor.Morale + 40.0);
                    effectCtx.Actor.Boredom = Math.Max(0.0, effectCtx.Actor.Boredom - 20.0);
                }

                // Critical success grants a blessing
                if (tier == RollOutcomeTier.CriticalSuccess)
                {
                    effectCtx.Actor.ActiveBuffs.Add(new ActiveBuff
                    {
                        Name = "Mermaid's Blessing",
                        Type = BuffType.Advantage,
                        SkillType = SkillType.Fishing,
                        Value = 0,
                        ExpiresAt = effectCtx.World.CurrentTime + 600.0
                    });
                }
            })
        ));
    }

    public override Dictionary<string, object> SerializeToDict()
    {
        var dict = base.SerializeToDict();
        dict["ExpiresAt"] = ExpiresAt;
        return dict;
    }

    public override void DeserializeFromDict(Dictionary<string, JsonElement> data)
    {
        base.DeserializeFromDict(data);
        ExpiresAt = data["ExpiresAt"].GetDouble();
    }
}
