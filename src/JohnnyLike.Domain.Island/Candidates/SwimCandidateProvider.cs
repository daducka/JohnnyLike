using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Candidates;

[IslandCandidateProvider(410, "swim")]
public class SwimCandidateProvider : IIslandCandidateProvider
{
    private const double SHARK_MORALE_PENALTY = 10.0;

    public void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        if (ctx.Actor.Energy < 20.0)
            return;

        // Block swimming if shark is present
        if (ctx.World.Shark.IsPresent)
            return;

        var baseDC = 10;

        if (ctx.World.Weather == Weather.Windy)
            baseDC += 3;
        else if (ctx.World.Weather == Weather.Rainy)
            baseDC += 1;

        var modifier = ctx.Actor.GetSkillModifier("Survival");
        var advantage = ctx.Actor.GetAdvantage("Survival");

        var estimatedChance = DndMath.EstimateSuccessChanceD20(baseDC, modifier, advantage);

        var baseScore = 0.35 + (ctx.Actor.Morale < 30 ? 0.2 : 0.0);
        baseScore *= estimatedChance;

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("swim"),
                ActionKind.Interact,
                new SkillCheckActionParameters(baseDC, modifier, advantage, "water"),
                15.0 + ctx.Rng.NextDouble() * 5.0
            ),
            baseScore,
            $"Swim (DC {baseDC}, {estimatedChance:P0} chance)"
        ));
    }

    public void ApplyEffects(EffectContext ctx)
    {
        if (ctx.Tier == null)
            return;

        var tier = ctx.Tier.Value;

        switch (tier)
        {
            case RollOutcomeTier.CriticalSuccess:
                ctx.Actor.Morale = Math.Min(100.0, ctx.Actor.Morale + 20.0);
                ctx.Actor.Energy = Math.Max(0.0, ctx.Actor.Energy - 5.0);
                ctx.Actor.Boredom = Math.Max(0.0, ctx.Actor.Boredom - 15.0);
                
                // Spawn treasure chest if not already present
                if (!ctx.World.TreasureChest.IsPresent)
                {
                    ctx.World.TreasureChest.IsPresent = true;
                    ctx.World.TreasureChest.IsOpened = false;
                    ctx.World.TreasureChest.Health = 100.0;
                    ctx.World.TreasureChest.Position = "shore";
                    
                    if (ctx.Outcome.ResultData != null)
                    {
                        ctx.Outcome.ResultData["variant_id"] = "swim_crit_success_treasure";
                        ctx.Outcome.ResultData["encounter_type"] = "treasure_chest";
                    }
                }
                break;

            case RollOutcomeTier.Success:
                ctx.Actor.Morale = Math.Min(100.0, ctx.Actor.Morale + 10.0);
                ctx.Actor.Energy = Math.Max(0.0, ctx.Actor.Energy - 10.0);
                ctx.Actor.Boredom = Math.Max(0.0, ctx.Actor.Boredom - 10.0);
                break;

            case RollOutcomeTier.PartialSuccess:
                ctx.Actor.Morale = Math.Min(100.0, ctx.Actor.Morale + 3.0);
                ctx.Actor.Energy = Math.Max(0.0, ctx.Actor.Energy - 15.0);
                break;

            case RollOutcomeTier.Failure:
                ctx.Actor.Energy = Math.Max(0.0, ctx.Actor.Energy - 15.0);
                break;

            case RollOutcomeTier.CriticalFailure:
                ctx.Actor.Energy = Math.Max(0.0, ctx.Actor.Energy - 25.0);
                ctx.Actor.Morale = Math.Max(0.0, ctx.Actor.Morale - SHARK_MORALE_PENALTY);
                
                // Spawn shark if not already present
                if (!ctx.World.Shark.IsPresent)
                {
                    var duration = 60.0 + ctx.Rng.NextDouble() * 120.0; // 60-180 seconds
                    ctx.World.Shark.IsPresent = true;
                    ctx.World.Shark.ExpiresAt = ctx.World.CurrentTime + duration;
                    
                    // Additional morale penalty for shark encounter
                    ctx.Actor.Morale = Math.Max(0.0, ctx.Actor.Morale - SHARK_MORALE_PENALTY);
                    
                    if (ctx.Outcome.ResultData != null)
                    {
                        ctx.Outcome.ResultData["variant_id"] = "swim_crit_failure_shark";
                        ctx.Outcome.ResultData["encounter_type"] = "shark";
                        ctx.Outcome.ResultData["shark_duration"] = duration;
                    }
                }
                break;
        }
    }
}
