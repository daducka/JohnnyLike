using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
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

    public override void Tick(long dtTicks, IslandWorldState world)
    {
        base.Tick(dtTicks, world);

        var weather = world.GetItem<WeatherItem>("weather");
        if (weather?.Temperature == TemperatureBand.Cold)
        {
            Quality = Math.Max(0.0, Quality - 0.03 * (dtTicks / (double)EngineConstants.TickHz));
        }
        if (weather?.Precipitation == PrecipitationBand.Rainy)
        {
            Quality = Math.Max(0.0, Quality - 0.02 * (dtTicks / (double)EngineConstants.TickHz));
        }
    }

    public override void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        var weather = ctx.World.GetItem<WeatherItem>("weather");
        var isCold = weather?.Temperature == TemperatureBand.Cold;

        // Repair action
        if (Quality < 70.0)
        {
            var baseDC = 12;
            if (isCold)
                baseDC += 2;

            var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC);

            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("repair_shelter"),
                    ActionKind.Interact,
                    parameters,
                    EngineConstants.TimeToTicks(30.0, 40.0, ctx.Random),
                    parameters.ToResultData(),
                    new List<ResourceRequirement> { new ResourceRequirement(ShelterResource) }
                ),
                0.5,
                $"Repair shelter (quality: {Quality:F0}%, {(isCold ? "cold" : "warm")}, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})",
                EffectHandler: new Action<EffectContext>(ApplyRepairShelterEffect),
                Qualities: new Dictionary<QualityType, double>
                {
                    [QualityType.Safety]              = 1.0,
                    [QualityType.Comfort]             = 0.4,
                    [QualityType.ResourcePreservation] = 0.6
                }
            ));
        }

        // Reinforce action
        if (Quality < 50.0)
        {
            var baseDC = 13;
            var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC);

            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("reinforce_shelter"),
                    ActionKind.Interact,
                    parameters,
                    EngineConstants.TimeToTicks(40.0, 50.0, ctx.Random),
                    parameters.ToResultData(),
                    new List<ResourceRequirement> { new ResourceRequirement(ShelterResource) }
                ),
                0.6,
                $"Reinforce shelter (quality: {Quality:F0}%, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})",
                EffectHandler: new Action<EffectContext>(ApplyReinforceShelterEffect),
                Qualities: new Dictionary<QualityType, double>
                {
                    [QualityType.Safety]              = 1.0,
                    [QualityType.ResourcePreservation] = 0.8,
                    [QualityType.Comfort]             = 0.3
                }
            ));
        }

        // Rebuild action
        if (Quality < 15.0)
        {
            var baseDC = 14;
            var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC);

            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("rebuild_shelter"),
                    ActionKind.Interact,
                    parameters,
                    EngineConstants.TimeToTicks(90.0, 120.0, ctx.Random),
                    parameters.ToResultData(),
                    new List<ResourceRequirement> { new ResourceRequirement(ShelterResource) }
                ),
                0.7,
                $"Rebuild shelter from scratch (rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})",
                EffectHandler: new Action<EffectContext>(ApplyRebuildShelterEffect),
                Qualities: new Dictionary<QualityType, double>
                {
                    [QualityType.Safety]              = 1.0,
                    [QualityType.ResourcePreservation] = 1.0,
                    [QualityType.Preparation]         = 0.5,
                    [QualityType.Comfort]             = 0.5
                }
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
            ctx.Actor.Morale += 8.0;
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
            ctx.Actor.Morale += 20.0;
        }
    }
}
