using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Kit.Dice;
using System.Text.Json;

namespace JohnnyLike.Domain.Island.Items;

/// <summary>
/// Represents a plane flying overhead.
/// Provides "Try to Signal Plane" action (which currently always fails) and sticks around for a limited time.
/// </summary>
public class PlaneItem : MaintainableWorldItem
{
    private static readonly ResourceId BeachOpenArea = new("island:resource:beach:open_area");
    
    public double ExpiresAt { get; set; } = 0.0;
    
    public PlaneItem(string id = "plane")
        : base(id, "plane", baseDecayPerSecond: 0.0)
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
        var baseDC = 25; // Extremely difficult - essentially impossible
        var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC);
        var baseScore = 0.9; // Very high priority - plane won't be here long!

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("try_to_signal_plane"),
                ActionKind.Interact,
                parameters,
                10.0,
                parameters.ToResultData(),
                new List<ResourceRequirement> { new ResourceRequirement(BeachOpenArea) }
            ),
            baseScore,
            $"Try to signal plane (DC {baseDC}, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})",
            EffectHandler: new Action<EffectContext>(effectCtx =>
            {
                if (effectCtx.Tier == null)
                    return;

                var tier = effectCtx.Tier.Value;

                // Always fails - reduces morale
                effectCtx.Actor.Morale = Math.Max(0.0, effectCtx.Actor.Morale - 10.0);
                effectCtx.Actor.Energy = Math.Max(0.0, effectCtx.Actor.Energy - 15.0);
                
                // Even on "success" (unlikely), still doesn't rescue - just less morale loss
                if (tier >= RollOutcomeTier.Success)
                {
                    effectCtx.Actor.Morale = Math.Min(100.0, effectCtx.Actor.Morale + 5.0); // Partial recovery
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
