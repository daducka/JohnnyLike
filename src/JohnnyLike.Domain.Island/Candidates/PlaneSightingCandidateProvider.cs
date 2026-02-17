using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Candidates;

[IslandCandidateProvider(800, "plane_sighting")]
public class PlaneSightingCandidateProvider : IIslandCandidateProvider
{
    public void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        // Only add if random chance triggers
        if (ctx.Random.NextDouble() >= 0.05)
            return;

        var baseDC = 15;
        var skillId = "Perception";
        var modifier = ctx.Actor.GetSkillModifier(skillId);
        var advantage = ctx.Actor.GetAdvantage(skillId);

        // Roll skill check at candidate generation time
        var request = new SkillCheckRequest(baseDC, modifier, advantage, skillId);
        var result = SkillCheckResolver.Resolve(ctx.Rng, request);

        // Calculate base score with cooldown factored in
        var timeSinceLastSighting = ctx.NowSeconds - ctx.Actor.LastPlaneSightingTime;
        var cooldownFactor = Math.Min(1.0, timeSinceLastSighting / 600.0); // 600 second cooldown
        var baseScore = 0.2 * (result.OutcomeTier >= RollOutcomeTier.Success ? 1.0 : 0.3) * cooldownFactor;

        // Populate ResultData with skill check outcome
        var resultData = new Dictionary<string, object>
        {
            ["dc"] = baseDC,
            ["modifier"] = modifier,
            ["advantage"] = advantage.ToString(),
            ["skillId"] = skillId,
            ["roll"] = result.Roll,
            ["total"] = result.Total,
            ["tier"] = result.OutcomeTier.ToString()
        };

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("plane_sighting"),
                ActionKind.Interact,
                new SkillCheckActionParameters(baseDC, modifier, advantage, "beach", skillId),
                10.0,
                resultData
            ),
            baseScore,
            $"Plane sighting (DC {baseDC}, rolled {result.Total}, {result.OutcomeTier})"
        ));
    }

    public void ApplyEffects(EffectContext ctx)
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
                SkillId = "",
                Value = 2,
                ExpiresAt = ctx.World.CurrentTime + 300.0
            });
        }
    }
}
