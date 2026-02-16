using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Candidates;

[IslandCandidateProvider(210, "shake_tree_coconut")]
public class CoconutCandidateProvider : IIslandCandidateProvider
{
    public void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        if (ctx.World.CoconutsAvailable < 1)
            return;

        var baseDC = 12;

        if (ctx.World.CoconutsAvailable >= 5)
            baseDC -= 2;
        else if (ctx.World.CoconutsAvailable <= 2)
            baseDC += 2;

        if (ctx.World.Weather == Weather.Windy)
            baseDC -= 1;

        var modifier = ctx.Actor.GetSkillModifier("Survival");
        var advantage = ctx.Actor.GetAdvantage("Survival");

        var estimatedChance = DndMath.EstimateSuccessChanceD20(baseDC, modifier, advantage);

        var baseScore = 0.4 + (ctx.Actor.Hunger / 150.0);
        if (ctx.Actor.Hunger > 70.0)
        {
            baseScore = 0.9;
        }
        else
        {
            baseScore *= estimatedChance;
        }

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("shake_tree_coconut"),
                ActionKind.Interact,
                new SkillCheckActionParameters(baseDC, modifier, advantage, "palm_tree"),
                10.0 + ctx.Rng.NextDouble() * 5.0
            ),
            baseScore,
            $"Get coconut (DC {baseDC}, {estimatedChance:P0} chance)"
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
                ctx.Actor.Hunger = Math.Max(0.0, ctx.Actor.Hunger - 25.0);
                ctx.World.CoconutsAvailable = Math.Max(0, ctx.World.CoconutsAvailable - 1);
                ctx.Actor.Energy = Math.Min(100.0, ctx.Actor.Energy + 15.0);
                break;

            case RollOutcomeTier.Success:
                ctx.Actor.Hunger = Math.Max(0.0, ctx.Actor.Hunger - 15.0);
                ctx.World.CoconutsAvailable = Math.Max(0, ctx.World.CoconutsAvailable - 1);
                ctx.Actor.Energy = Math.Min(100.0, ctx.Actor.Energy + 10.0);
                break;

            case RollOutcomeTier.PartialSuccess:
                ctx.Actor.Morale = Math.Min(100.0, ctx.Actor.Morale + 2.0);
                break;

            case RollOutcomeTier.Failure:
                break;

            case RollOutcomeTier.CriticalFailure:
                ctx.Actor.Morale = Math.Max(0.0, ctx.Actor.Morale - 5.0);
                break;
        }
    }
}
