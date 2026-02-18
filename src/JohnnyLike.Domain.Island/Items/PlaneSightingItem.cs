using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Items;

/// <summary>
/// Represents a plane sighting opportunity on the beach.
/// </summary>
public class PlaneSightingItem : MaintainableWorldItem
{
    private static readonly ResourceId BeachOpenArea = new("island:resource:beach:open_area");

    public PlaneSightingItem(string id = "plane_sighting_opportunity")
        : base(id, "plane_sighting_opportunity", baseDecayPerSecond: 0.0)
    {
        // This is an opportunity/event, not a physical item, so it doesn't decay
    }

    public override void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        // Only add if random chance triggers
        if (ctx.Random.NextDouble() >= 0.05)
            return;

        var baseDC = 15;

        // Roll skill check at candidate generation time
        var parameters = ctx.RollSkillCheck(SkillType.Perception, baseDC);

        // Calculate base score with cooldown factored in
        var timeSinceLastSighting = ctx.NowSeconds - ctx.Actor.LastPlaneSightingTime;
        var cooldownFactor = Math.Min(1.0, timeSinceLastSighting / 600.0); // 600 second cooldown
        var baseScore = 0.2 * cooldownFactor;

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("plane_sighting"),
                ActionKind.Interact,
                parameters,
                10.0,
                parameters.ToResultData(),
                new List<ResourceRequirement> { new ResourceRequirement(BeachOpenArea) }
            ),
            baseScore,
            $"Plane sighting (DC {baseDC}, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})",
            EffectHandler: new Action<EffectContext>(ApplyEffects)
        ));
    }

    public override void ApplyEffects(EffectContext ctx)
    {
        ctx.Actor.LastPlaneSightingTime = ctx.World.CurrentTime;

        if (ctx.Tier == null)
            return;

        var tier = ctx.Tier.Value;

        if (tier >= RollOutcomeTier.Success)
        {
            ctx.Actor.Morale = Math.Min(100.0, ctx.Actor.Morale + 30.0);
        }

        if (tier == RollOutcomeTier.CriticalSuccess)
        {
            ctx.Actor.ActiveBuffs.Add(new ActiveBuff
            {
                Name = "Luck",
                Type = BuffType.SkillBonus,
                SkillType = null, // Applies to all skills
                Value = 2,
                ExpiresAt = ctx.World.CurrentTime + 300.0
            });
        }
    }
}
