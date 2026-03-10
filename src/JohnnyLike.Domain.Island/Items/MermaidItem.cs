using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Items;

/// <summary>
/// Represents a mermaid at the shore.
/// Provides "Wave at Mermaid" action and sticks around for a limited time before leaving.
/// </summary>
public class MermaidItem : ExpirableWorldItem
{
    private static readonly ResourceId ShoreEastEnd = new("island:resource:shore:east_end");
    
    public MermaidItem(string id = "mermaid")
        : base(id, "mermaid")
    {
    }

    public override void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        var baseDC = 10;
        var parameters = ctx.RollSkillCheck(SkillType.Performance, baseDC);
        var baseScore = 0.28; // Rare opportunity — mermaid won't be here long!

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("wave_at_mermaid"),
                ActionKind.Interact,
                parameters,
                EngineConstants.TimeToTicks(5.0),
                "wave at mermaid",
                parameters.ToResultData(),
                new List<ResourceRequirement> { new ResourceRequirement(ShoreEastEnd) }
            ),
            baseScore,
            Reason: $"Wave at mermaid (DC {baseDC}, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})",
            EffectHandler: new Action<EffectContext>(effectCtx =>
            {
                if (effectCtx.Tier == null)
                    return;

                var tier = effectCtx.Tier.Value;
                var actor = effectCtx.ActorId.Value;

                // Positive impact on morale for successful interaction
                if (tier >= RollOutcomeTier.Success)
                {
                    effectCtx.Actor.Morale += 40.0;
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
                        ExpiresAtTick = effectCtx.World.CurrentTick + 600L * 20
                    });
                    effectCtx.SetOutcomeNarration($"{actor} waves at the mermaid and she smiles warmly, granting a blessing.");
                }
                else if (tier == RollOutcomeTier.Success)
                {
                    effectCtx.SetOutcomeNarration($"{actor} waves; the mermaid notices and responds with a friendly gesture.");
                }
                else if (tier == RollOutcomeTier.PartialSuccess)
                {
                    effectCtx.SetOutcomeNarration($"The mermaid barely acknowledges {actor}'s wave.");
                }
                else
                {
                    effectCtx.SetOutcomeNarration($"{actor} waves enthusiastically, but the mermaid slips beneath the waves without reacting.");
                }
            }),
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Fun]    = 0.8,
                [QualityType.Comfort] = 0.2
            }
        ));
    }
}
