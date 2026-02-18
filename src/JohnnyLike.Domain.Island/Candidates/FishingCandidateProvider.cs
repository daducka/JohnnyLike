using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Stats;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Candidates;

[IslandCandidateProvider(200, "fish_for_food")]
public class FishingCandidateProvider : IIslandCandidateProvider
{
    private static readonly ResourceId PrimaryFishingSpot = new("island:fishing:spot:primary");
    private static readonly ResourceId SecondaryFishingSpot = new("island:fishing:spot:secondary");

    public void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        var fishStat = ctx.World.GetStat<FishPopulationStat>("fish_population");
        if (fishStat == null || fishStat.FishAvailable < 5.0)
            return;

        var baseDC = 10;
        
        // Morning (0.0-0.25) and dusk (0.75-1.0) are better for fishing - LOWER DC
        var timeOfDayStat = ctx.World.GetStat<TimeOfDayStat>("time_of_day");
        var timeOfDay = timeOfDayStat?.TimeOfDay ?? 0.5;
        if (timeOfDay < 0.25 || timeOfDay > 0.75)
            baseDC -= 2;  // Easier in morning/dusk
        else if (timeOfDay >= 0.375 && timeOfDay <= 0.625)
            baseDC += 1;  // Slightly harder in afternoon

        // Rainy weather is good for fishing - LOWER DC
        var weatherStat = ctx.World.GetStat<WeatherStat>("weather");
        if (weatherStat?.Weather == Weather.Rainy)
            baseDC -= 2;  // Easier when rainy
        else if (weatherStat?.Weather == Weather.Windy)
            baseDC += 1;  // Harder when windy

        if (fishStat.FishAvailable < 20.0)
            baseDC += 3;
        else if (fishStat.FishAvailable < 50.0)
            baseDC += 1;

        if (ctx.Actor.Energy < 30.0)
            baseDC += 2;

        var baseScore = 0.5 + (ctx.Actor.Hunger / 100.0);
        if (ctx.Actor.Hunger > 70.0 || ctx.Actor.Energy < 20.0)
        {
            baseScore = 1.0;
        }

        // Check fishing spot availability and prefer primary
        bool primaryAvailable = !ctx.ResourceAvailability.IsReserved(PrimaryFishingSpot);
        bool secondaryAvailable = !ctx.ResourceAvailability.IsReserved(SecondaryFishingSpot);

        if (!primaryAvailable && !secondaryAvailable)
        {
            // Both spots reserved, no fishing candidate
            return;
        }

        // Prefer primary spot if available
        ResourceId chosenSpot;
        if (primaryAvailable)
        {
            chosenSpot = PrimaryFishingSpot;
        }
        else
        {
            chosenSpot = SecondaryFishingSpot;
        }

        // Roll skill check at candidate generation time
        var parameters = ctx.RollSkillCheck(SkillType.Fishing, baseDC);

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("fish_for_food"),
                ActionKind.Interact,
                parameters,
                15.0 + ctx.Random.NextDouble() * 5.0,
                parameters.ToResultData(),
                new List<ResourceRequirement> { new ResourceRequirement(chosenSpot) }
            ),
            baseScore,
            $"Fishing (DC {baseDC}, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})"
        ));
    }

    public void ApplyEffects(EffectContext ctx)
    {
        if (ctx.Tier == null)
            return;

        var tier = ctx.Tier.Value;
        var fishStat = ctx.World.GetStat<FishPopulationStat>("fish_population");
        if (fishStat == null)
            return;

        switch (tier)
        {
            case RollOutcomeTier.CriticalSuccess:
                ctx.Actor.Hunger = Math.Max(0.0, ctx.Actor.Hunger - 50.0);
                fishStat.FishAvailable = Math.Max(0.0, fishStat.FishAvailable - 30.0);
                ctx.Actor.Morale = Math.Min(100.0, ctx.Actor.Morale + 15.0);
                break;

            case RollOutcomeTier.Success:
                ctx.Actor.Hunger = Math.Max(0.0, ctx.Actor.Hunger - 30.0);
                fishStat.FishAvailable = Math.Max(0.0, fishStat.FishAvailable - 15.0);
                ctx.Actor.Morale = Math.Min(100.0, ctx.Actor.Morale + 5.0);
                break;

            case RollOutcomeTier.PartialSuccess:
                ctx.Actor.Morale = Math.Min(100.0, ctx.Actor.Morale + 5.0);
                break;

            case RollOutcomeTier.Failure:
                break;

            case RollOutcomeTier.CriticalFailure:
                ctx.Actor.Morale = Math.Max(0.0, ctx.Actor.Morale - 10.0);
                break;
        }

        ctx.Actor.Boredom = Math.Max(0.0, ctx.Actor.Boredom - 10.0);
    }
}
