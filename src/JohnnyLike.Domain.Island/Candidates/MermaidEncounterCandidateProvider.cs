using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Stats;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Candidates;

[IslandCandidateProvider(810, "mermaid_encounter")]
public class MermaidEncounterCandidateProvider : IIslandCandidateProvider
{
    private static readonly ResourceId ShoreEastEnd = new("island:resource:shore:east_end");

    public void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        // Only during night/dawn/dusk
        var timeOfDayStat = ctx.World.GetStat<TimeOfDayStat>("time_of_day");
        var timeOfDay = timeOfDayStat?.TimeOfDay ?? 0.5;
        if (timeOfDay <= 0.75 && timeOfDay >= 0.25)
            return;

        // Only add if random chance triggers
        if (ctx.Random.NextDouble() >= 0.02)
            return;

        var baseDC = 18;

        // Roll skill check at candidate generation time
        var parameters = ctx.RollSkillCheck(SkillType.Perception, baseDC);

        // Calculate base score with cooldown factored in
        var timeSinceLastEncounter = ctx.NowSeconds - ctx.Actor.LastMermaidEncounterTime;
        var cooldownFactor = Math.Min(1.0, timeSinceLastEncounter / 1200.0); // 1200 second cooldown
        var baseScore = 0.15 * cooldownFactor;

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("mermaid_encounter"),
                ActionKind.Interact,
                parameters,
                15.0,
                parameters.ToResultData(),
                new List<ResourceRequirement> { new ResourceRequirement(ShoreEastEnd) }
            ),
            baseScore,
            $"Mermaid encounter (DC {baseDC}, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})"
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
                SkillType = SkillType.Fishing,
                Value = 0,
                ExpiresAt = ctx.World.CurrentTime + 600.0
            });
        }
    }
}
