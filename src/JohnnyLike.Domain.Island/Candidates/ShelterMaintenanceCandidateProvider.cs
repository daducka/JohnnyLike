using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Kit.Dice;
using JohnnyLike.Domain.Island.Items;

namespace JohnnyLike.Domain.Island.Candidates;

[IslandCandidateProvider(160, "repair_shelter", "reinforce_shelter", "rebuild_shelter")]
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

            var baseDC = 12;
            if (ctx.World.Weather == Weather.Rainy)
                baseDC += 2;
            else if (ctx.World.Weather == Weather.Windy)
                baseDC += 1;

            var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC, "shelter");

            var baseScore = 0.25 + (urgency * 0.5 * weatherMultiplier * foresightMultiplier);

            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("repair_shelter"),
                    ActionKind.Interact,
                    parameters,
                    30.0 + ctx.Random.NextDouble() * 10.0,
                    parameters.ToResultData()
                ),
                baseScore,
                $"Repair shelter (quality: {shelter.Quality:F0}%, {ctx.World.Weather}, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})"
            ));
        }

        if (shelter.Quality < 50.0)
        {
            var urgency = (50.0 - shelter.Quality) / 50.0;
            var foresightMultiplier = 1.0 + (foresightBonus * 0.2);

            var baseDC = 13;
            var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC, "shelter");

            var baseScore = 0.4 + (urgency * 0.5 * foresightMultiplier);

            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("reinforce_shelter"),
                    ActionKind.Interact,
                    parameters,
                    40.0 + ctx.Random.NextDouble() * 10.0,
                    parameters.ToResultData()
                ),
                baseScore,
                $"Reinforce shelter (quality: {shelter.Quality:F0}%, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})"
            ));
        }

        if (shelter.Quality < 15.0)
        {
            var baseDC = 14;
            var foresightMultiplier = 1.0 + (foresightBonus * 0.25);

            var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC, "shelter");

            var baseScore = 1.2 * foresightMultiplier;

            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("rebuild_shelter"),
                    ActionKind.Interact,
                    parameters,
                    90.0 + ctx.Random.NextDouble() * 30.0,
                    parameters.ToResultData()
                ),
                baseScore,
                $"Rebuild shelter from scratch (rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})"
            ));
        }
    }

    public void ApplyEffects(EffectContext ctx)
    {
        var shelter = ctx.World.MainShelter;
        if (shelter == null)
            return;

        if (ctx.Tier == null)
            return;

        var tier = ctx.Tier.Value;
        var actionId = ctx.Outcome.ActionId.Value;

        switch (actionId)
        {
            case "repair_shelter":
                if (tier >= RollOutcomeTier.PartialSuccess)
                {
                    var qualityRestored = tier == RollOutcomeTier.CriticalSuccess ? 35.0 : 
                                         tier == RollOutcomeTier.Success ? 20.0 : 10.0;
                    shelter.Quality = Math.Min(100.0, shelter.Quality + qualityRestored);
                    ctx.Actor.Boredom = Math.Max(0.0, ctx.Actor.Boredom - 6.0);
                }
                break;

            case "reinforce_shelter":
                if (tier >= RollOutcomeTier.Success)
                {
                    var qualityRestored = tier == RollOutcomeTier.CriticalSuccess ? 45.0 : 30.0;
                    shelter.Quality = Math.Min(100.0, shelter.Quality + qualityRestored);
                    ctx.Actor.Morale = Math.Min(100.0, ctx.Actor.Morale + 8.0);
                    ctx.Actor.Boredom = Math.Max(0.0, ctx.Actor.Boredom - 10.0);
                }
                break;

            case "rebuild_shelter":
                if (tier >= RollOutcomeTier.Success)
                {
                    shelter.Quality = tier == RollOutcomeTier.CriticalSuccess ? 100.0 : 85.0;
                    ctx.Actor.Morale = Math.Min(100.0, ctx.Actor.Morale + 20.0);
                    ctx.Actor.Boredom = Math.Max(0.0, ctx.Actor.Boredom - 25.0);
                }
                break;
        }
    }
}
