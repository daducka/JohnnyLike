using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Candidates;

[IslandCandidateProvider(420, "bash_open_treasure_chest")]
public class BashTreasureChestCandidateProvider : IIslandCandidateProvider
{
    private const double DC_MIN = 10.0;
    private const double DC_MAX = 20.0;
    private const int DEFAULT_BASH_MODIFIER = 0; // TODO: Could be derived from strength/fitness stat later

    public void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        var chest = ctx.World.TreasureChest;
        
        // Only emit candidate when chest is present and unopened
        if (chest == null || chest.IsOpened)
            return;

        // Calculate DC based on chest health
        var healthRatio = chest.Health / 100.0;
        var baseDC = (int)(DC_MIN + healthRatio * (DC_MAX - DC_MIN));

        // Roll skill check at candidate generation time
        var parameters = ctx.RollSkillCheck("Athletics", baseDC, "treasure_chest");

        var baseScore = 0.6; // High priority for treasure

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("bash_open_treasure_chest"),
                ActionKind.Interact,
                parameters,
                20.0 + ctx.Random.NextDouble() * 5.0,
                parameters.ToResultData()
            ),
            baseScore,
            $"Bash open treasure chest (DC {baseDC}, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})"
        ));
    }

    public void ApplyEffects(EffectContext ctx)
    {
        if (ctx.Tier == null)
            return;

        var chest = ctx.World.TreasureChest;
        
        // Safety check - chest might have been removed
        if (chest == null)
            return;

        var tier = ctx.Tier.Value;

        // Common effect: consume energy
        ctx.Actor.Energy = Math.Max(0.0, ctx.Actor.Energy - 15.0);

        // Success: open and remove chest, grant reward
        if (tier >= RollOutcomeTier.Success)
        {
            chest.IsOpened = true;
            chest.Health = 0.0;
            ctx.World.WorldItems.Remove(chest);

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

            chest.Health = Math.Max(0.0, chest.Health - damage);

            // Small morale penalty for failing
            ctx.Actor.Morale = Math.Max(0.0, ctx.Actor.Morale - 5.0);

            if (ctx.Outcome.ResultData != null)
            {
                ctx.Outcome.ResultData["variant_id"] = "bash_chest_failure";
                ctx.Outcome.ResultData["chest_health_after"] = chest.Health;
            }
        }
    }
}
