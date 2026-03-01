using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Items;

/// <summary>
/// Represents a plane flying overhead.
/// Provides "Try to Signal Plane" action (which currently always fails) and sticks around for a limited time.
/// </summary>
public class PlaneItem : ExpirableWorldItem
{
    private static readonly ResourceId BeachOpenArea = new("island:resource:beach:open_area");
    
    public PlaneItem(string id = "plane")
        : base(id, "plane")
    {
    }

    public override void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        var baseDC = 25; // Extremely difficult - essentially impossible
        var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC);
        var baseScore = 0.8; // Very high priority - plane won't be here long!

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("try_to_signal_plane"),
                ActionKind.Interact,
                parameters,
                EngineConstants.TimeToTicks(10.0),
                parameters.ToResultData(),
                new List<ResourceRequirement> { new ResourceRequirement(BeachOpenArea) }
            ),
            baseScore,
            Reason: $"Try to signal plane (DC {baseDC}, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})",
            EffectHandler: new Action<EffectContext>(effectCtx =>
            {
                if (effectCtx.Tier == null)
                    return;

                var tier = effectCtx.Tier.Value;

                // Always fails - reduces morale
                effectCtx.Actor.Morale -= 10.0;
                effectCtx.Actor.Energy -= 15.0;
                
                // Even on "success" (unlikely), still doesn't rescue - just less morale loss
                if (tier >= RollOutcomeTier.Success)
                {
                    effectCtx.Actor.Morale += 5.0; // Partial recovery
                }
            }),
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Safety]      = 1.0,
                [QualityType.Preparation] = 0.8
            }
        ));
    }
}
