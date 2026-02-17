using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Candidates;

[IslandCandidateProvider(810, "mermaid_encounter")]
public class MermaidEncounterCandidateProvider : IIslandCandidateProvider
{
    public void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        // Only during night/dawn/dusk
        if (ctx.World.TimeOfDay <= 0.75 && ctx.World.TimeOfDay >= 0.25)
            return;

        // Only add if random chance triggers
        if (ctx.Random.NextDouble() >= 0.02)
            return;

        var baseDC = 18;
        var skillId = "Perception";
        var modifier = ctx.Actor.GetSkillModifier(skillId);
        var advantage = ctx.Actor.GetAdvantage(skillId);

        // Roll skill check at candidate generation time
        var request = new SkillCheckRequest(baseDC, modifier, advantage, skillId);
        var result = SkillCheckResolver.Resolve(ctx.Rng, request);

        // Calculate base score with cooldown factored in
        var timeSinceLastEncounter = ctx.NowSeconds - ctx.Actor.LastMermaidEncounterTime;
        var cooldownFactor = Math.Min(1.0, timeSinceLastEncounter / 1200.0); // 1200 second cooldown
        var baseScore = 0.15 * (result.OutcomeTier >= RollOutcomeTier.Success ? 1.0 : 0.3) * cooldownFactor;

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
                new ActionId("mermaid_encounter"),
                ActionKind.Interact,
                new SkillCheckActionParameters(baseDC, modifier, advantage, "shore", skillId),
                15.0,
                resultData
            ),
            baseScore,
            $"Mermaid encounter (DC {baseDC}, rolled {result.Total}, {result.OutcomeTier})"
        ));
    }

    public void ApplyEffects(EffectContext ctx)
    {
        ctx.Actor.LastMermaidEncounterTime = ctx.World.CurrentTime;

        if (ctx.Tier == null)
            return;

        var tier = ctx.Tier.Value;

        if (tier >= RollOutcomeTier.Success)
        {
            ctx.Actor.Morale = Math.Min(100.0, ctx.Actor.Morale + 40.0);
        }

        if (tier == RollOutcomeTier.CriticalSuccess)
        {
            ctx.Actor.ActiveBuffs.Add(new ActiveBuff
            {
                Name = "Mermaid's Blessing",
                Type = BuffType.Advantage,
                SkillId = "Fishing",
                Value = 0,
                ExpiresAt = ctx.World.CurrentTime + 600.0
            });
        }
    }
}
