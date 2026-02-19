using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Stats;
using JohnnyLike.Domain.Kit.Dice;
using System.Text.Json;

namespace JohnnyLike.Domain.Island.Items;

public class ShelterItem : ToolItem
{
    private static readonly ResourceId ShelterResource = new("island:resource:shelter");
    
    public ShelterItem(string id = "main_shelter") 
        : base(id, "shelter", OwnershipType.Shared, baseDecayPerSecond: 0.015)
    {
    }

    public override void Tick(double dtSeconds, IslandWorldState world)
    {
        base.Tick(dtSeconds, world);

        var weatherStat = world.GetStat<WeatherStat>("weather");
        if (weatherStat?.Weather == Weather.Rainy)
        {
            Quality = Math.Max(0.0, Quality - 0.03 * dtSeconds);
        }
        else if (weatherStat?.Weather == Weather.Windy)
        {
            Quality = Math.Max(0.0, Quality - 0.02 * dtSeconds);
        }
    }

    public override void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        var survivalMod = ctx.Actor.SurvivalSkill;
        var wisdomMod = DndMath.AbilityModifier(ctx.Actor.WIS);
        var foresightBonus = (survivalMod + wisdomMod) / 2.0;

        var weatherStat = ctx.World.GetStat<WeatherStat>("weather");
        var weather = weatherStat?.Weather ?? Weather.Clear;

        // Repair action
        if (Quality < 70.0)
        {
            var urgency = (70.0 - Quality) / 70.0;
            var weatherMultiplier = weather switch
            {
                Weather.Rainy => 1.5,
                Weather.Windy => 1.3,
                _ => 1.0
            };

            var foresightMultiplier = 1.0 + (foresightBonus * 0.15);
            var baseDC = 12;
            if (weather == Weather.Rainy)
                baseDC += 2;
            else if (weather == Weather.Windy)
                baseDC += 1;

            var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC);
            var baseScore = 0.25 + (urgency * 0.5 * weatherMultiplier * foresightMultiplier);

            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("repair_shelter"),
                    ActionKind.Interact,
                    parameters,
                    30.0 + ctx.Random.NextDouble() * 10.0,
                    parameters.ToResultData(),
                    new List<ResourceRequirement> { new ResourceRequirement(ShelterResource) }
                ),
                baseScore,
                $"Repair shelter (quality: {Quality:F0}%, {weather}, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})",
                EffectHandler: new Action<EffectContext>(ApplyRepairShelterEffect)
            ));
        }

        // Reinforce action
        if (Quality < 50.0)
        {
            var urgency = (50.0 - Quality) / 50.0;
            var foresightMultiplier = 1.0 + (foresightBonus * 0.2);
            var baseDC = 13;
            var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC);
            var baseScore = 0.4 + (urgency * 0.5 * foresightMultiplier);

            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("reinforce_shelter"),
                    ActionKind.Interact,
                    parameters,
                    40.0 + ctx.Random.NextDouble() * 10.0,
                    parameters.ToResultData(),
                    new List<ResourceRequirement> { new ResourceRequirement(ShelterResource) }
                ),
                baseScore,
                $"Reinforce shelter (quality: {Quality:F0}%, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})",
                EffectHandler: new Action<EffectContext>(ApplyReinforceShelterEffect)
            ));
        }

        // Rebuild action
        if (Quality < 15.0)
        {
            var baseDC = 14;
            var foresightMultiplier = 1.0 + (foresightBonus * 0.25);
            var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC);
            var baseScore = 1.2 * foresightMultiplier;

            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("rebuild_shelter"),
                    ActionKind.Interact,
                    parameters,
                    90.0 + ctx.Random.NextDouble() * 30.0,
                    parameters.ToResultData(),
                    new List<ResourceRequirement> { new ResourceRequirement(ShelterResource) }
                ),
                baseScore,
                $"Rebuild shelter from scratch (rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})",
                EffectHandler: new Action<EffectContext>(ApplyRebuildShelterEffect)
            ));
        }
    }

    public void ApplyRepairShelterEffect(EffectContext ctx)
    {
        if (ctx.Tier == null)
            return;

        var tier = ctx.Tier.Value;

        if (tier >= RollOutcomeTier.PartialSuccess)
        {
            var qualityRestored = tier == RollOutcomeTier.CriticalSuccess ? 35.0 : 
                                 tier == RollOutcomeTier.Success ? 20.0 : 10.0;
            Quality = Math.Min(100.0, Quality + qualityRestored);
            ctx.Actor.Morale += 6.0;
        }
    }

    public void ApplyReinforceShelterEffect(EffectContext ctx)
    {
        if (ctx.Tier == null)
            return;

        var tier = ctx.Tier.Value;

        if (tier >= RollOutcomeTier.Success)
        {
            var qualityRestored = tier == RollOutcomeTier.CriticalSuccess ? 45.0 : 30.0;
            Quality = Math.Min(100.0, Quality + qualityRestored);
            ctx.Actor.Morale += 18.0;
        }
    }

    public void ApplyRebuildShelterEffect(EffectContext ctx)
    {
        if (ctx.Tier == null)
            return;

        var tier = ctx.Tier.Value;

        if (tier >= RollOutcomeTier.Success)
        {
            Quality = tier == RollOutcomeTier.CriticalSuccess ? 100.0 : 85.0;
            ctx.Actor.Morale += 45.0;
        }
    }
}
