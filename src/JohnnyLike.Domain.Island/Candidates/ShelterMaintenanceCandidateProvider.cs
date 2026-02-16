using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Candidates;

[IslandCandidateProvider(160)]
public class ShelterMaintenanceCandidateProvider : IIslandCandidateProvider
{
    public void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        var shelter = ctx.World.MainShelter;
        if (shelter == null)
            return;

        var survivalMod = ctx.Actor.SurvivalSkill;
        var wisdomMod = DndMath.AbilityModifier(ctx.Actor.WIS);
        
        var foresightBonus = (survivalMod + wisdomMod) / 2.0;

        if (shelter.Quality < 70.0)
        {
            var urgency = (70.0 - shelter.Quality) / 70.0;
            
            var weatherMultiplier = ctx.World.Weather switch
            {
                Weather.Rainy => 1.5,
                Weather.Windy => 1.3,
                _ => 1.0
            };

            var foresightMultiplier = 1.0 + (foresightBonus * 0.15);
            var baseScore = 0.25 + (urgency * 0.5 * weatherMultiplier * foresightMultiplier);

            var baseDC = 12;
            if (ctx.World.Weather == Weather.Rainy)
                baseDC += 2;
            else if (ctx.World.Weather == Weather.Windy)
                baseDC += 1;

            var modifier = ctx.Actor.GetSkillModifier("Survival");
            var advantage = ctx.Actor.GetAdvantage("Survival");

            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("repair_shelter"),
                    ActionKind.Interact,
                    new SkillCheckActionParameters(baseDC, modifier, advantage, "shelter"),
                    30.0 + ctx.Rng.NextDouble() * 10.0
                ),
                baseScore,
                $"Repair shelter (quality: {shelter.Quality:F0}%, {ctx.World.Weather})"
            ));
        }

        if (shelter.Quality < 50.0)
        {
            var urgency = (50.0 - shelter.Quality) / 50.0;
            var foresightMultiplier = 1.0 + (foresightBonus * 0.2);
            var baseScore = 0.4 + (urgency * 0.5 * foresightMultiplier);

            var baseDC = 13;
            var modifier = ctx.Actor.GetSkillModifier("Survival");
            var advantage = ctx.Actor.GetAdvantage("Survival");

            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("reinforce_shelter"),
                    ActionKind.Interact,
                    new SkillCheckActionParameters(baseDC, modifier, advantage, "shelter"),
                    40.0 + ctx.Rng.NextDouble() * 10.0
                ),
                baseScore,
                $"Reinforce shelter (quality: {shelter.Quality:F0}%)"
            ));
        }

        if (shelter.Quality < 15.0)
        {
            var baseDC = 14;
            var modifier = ctx.Actor.GetSkillModifier("Survival");
            var advantage = ctx.Actor.GetAdvantage("Survival");
            var foresightMultiplier = 1.0 + (foresightBonus * 0.25);

            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("rebuild_shelter"),
                    ActionKind.Interact,
                    new SkillCheckActionParameters(baseDC, modifier, advantage, "shelter"),
                    90.0 + ctx.Rng.NextDouble() * 30.0
                ),
                1.2 * foresightMultiplier,
                "Rebuild shelter from scratch"
            ));
        }
    }
}
