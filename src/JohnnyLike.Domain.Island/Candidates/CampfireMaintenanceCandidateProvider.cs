using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Candidates;

[IslandCandidateProvider(150)]
public class CampfireMaintenanceCandidateProvider : IIslandCandidateProvider
{
    public void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        var campfire = ctx.World.MainCampfire;
        if (campfire == null)
            return;

        var survivalMod = ctx.Actor.SurvivalSkill;
        var wisdomMod = DndMath.AbilityModifier(ctx.Actor.WIS);
        
        var foresightBonus = (survivalMod + wisdomMod) / 2.0;

        if (campfire.IsLit && campfire.FuelSeconds < 1800.0)
        {
            var urgency = 1.0 - (campfire.FuelSeconds / 1800.0);
            var foresightMultiplier = 1.0 + (foresightBonus * 0.1);
            var baseScore = 0.3 + (urgency * 0.5 * foresightMultiplier);

            var baseDC = 10;
            var modifier = ctx.Actor.GetSkillModifier("Survival");
            var advantage = ctx.Actor.GetAdvantage("Survival");

            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("add_fuel_campfire"),
                    ActionKind.Interact,
                    new SkillCheckActionParameters(baseDC, modifier, advantage, "campfire"),
                    20.0 + ctx.Rng.NextDouble() * 5.0
                ),
                baseScore,
                $"Add fuel to campfire (fuel: {campfire.FuelSeconds:F0}s)"
            ));
        }

        if (!campfire.IsLit && campfire.Quality > 20.0)
        {
            var urgency = 0.8;
            var foresightMultiplier = 1.0 + (foresightBonus * 0.15);
            var baseScore = urgency * foresightMultiplier;

            var baseDC = 12;
            var modifier = ctx.Actor.GetSkillModifier("Survival");
            var advantage = ctx.Actor.GetAdvantage("Survival");

            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("relight_campfire"),
                    ActionKind.Interact,
                    new SkillCheckActionParameters(baseDC, modifier, advantage, "campfire"),
                    30.0 + ctx.Rng.NextDouble() * 10.0
                ),
                baseScore,
                $"Relight campfire (quality: {campfire.Quality:F0}%)"
            ));
        }

        if (campfire.Quality < 70.0)
        {
            var urgency = (70.0 - campfire.Quality) / 70.0;
            var foresightMultiplier = 1.0 + (foresightBonus * 0.12);
            var baseScore = 0.2 + (urgency * 0.4 * foresightMultiplier);

            var baseDC = 11;
            var modifier = ctx.Actor.GetSkillModifier("Survival");
            var advantage = ctx.Actor.GetAdvantage("Survival");

            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("repair_campfire"),
                    ActionKind.Interact,
                    new SkillCheckActionParameters(baseDC, modifier, advantage, "campfire"),
                    25.0 + ctx.Rng.NextDouble() * 5.0
                ),
                baseScore,
                $"Repair campfire (quality: {campfire.Quality:F0}%)"
            ));
        }

        if (campfire.Quality < 10.0)
        {
            var baseDC = 15;
            var modifier = ctx.Actor.GetSkillModifier("Survival");
            var advantage = ctx.Actor.GetAdvantage("Survival");
            var foresightMultiplier = 1.0 + (foresightBonus * 0.2);

            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("rebuild_campfire"),
                    ActionKind.Interact,
                    new SkillCheckActionParameters(baseDC, modifier, advantage, "campfire"),
                    60.0 + ctx.Rng.NextDouble() * 20.0
                ),
                1.0 * foresightMultiplier,
                "Rebuild campfire from scratch"
            ));
        }
    }
}
