using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Candidates;

[IslandCandidateProvider(420, "bash_open_treasure_chest")]
public class BashTreasureChestCandidateProvider : IIslandCandidateProvider
{
    private const double DC_MIN = 10.0;
    private const double DC_MAX = 20.0;

    public void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        // Only emit candidate when chest is present and unopened
        if (!ctx.World.TreasureChest.IsPresent || ctx.World.TreasureChest.IsOpened)
            return;

        // Calculate DC based on chest health
        var healthRatio = ctx.World.TreasureChest.Health / 100.0;
        var dc = (int)(DC_MIN + healthRatio * (DC_MAX - DC_MIN));

        var modifier = 0; // Could be derived from strength/fitness stat later
        var advantage = AdvantageType.Normal;

        var estimatedChance = DndMath.EstimateSuccessChanceD20(dc, modifier, advantage);

        var baseScore = 0.6; // High priority for treasure
        baseScore *= estimatedChance;

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("bash_open_treasure_chest"),
                ActionKind.Interact,
                new SkillCheckActionParameters(dc, modifier, advantage, "treasure_chest"),
                20.0 + ctx.Rng.NextDouble() * 5.0
            ),
            baseScore,
            $"Bash open treasure chest (DC {dc}, {estimatedChance:P0} chance)"
        ));
    }

    public void ApplyEffects(EffectContext ctx)
    {
        if (ctx.Tier == null)
            return;

        // Safety check - chest might have been removed
        if (!ctx.World.TreasureChest.IsPresent)
            return;

        var tier = ctx.Tier.Value;

        // Common effect: consume energy
        ctx.Actor.Energy = Math.Max(0.0, ctx.Actor.Energy - 15.0);

        // Success: open and remove chest, grant reward
        if (tier >= RollOutcomeTier.Success)
        {
            ctx.World.TreasureChest.IsOpened = true;
            ctx.World.TreasureChest.IsPresent = false;
            ctx.World.TreasureChest.Health = 0.0;
            ctx.World.TreasureChest.Position = null;

            // Placeholder reward
            ctx.Actor.Morale = Math.Min(100.0, ctx.Actor.Morale + 20.0);

            if (ctx.Outcome.ResultData != null)
            {
                ctx.Outcome.ResultData["variant_id"] = "bash_chest_success";
                ctx.Outcome.ResultData["loot_placeholder"] = true;
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

            ctx.World.TreasureChest.Health = Math.Max(0.0, ctx.World.TreasureChest.Health - damage);

            // Small morale penalty for failing
            ctx.Actor.Morale = Math.Max(0.0, ctx.Actor.Morale - 5.0);

            if (ctx.Outcome.ResultData != null)
            {
                ctx.Outcome.ResultData["variant_id"] = "bash_chest_failure";
                ctx.Outcome.ResultData["chest_health_after"] = ctx.World.TreasureChest.Health;
            }
        }
    }
}
