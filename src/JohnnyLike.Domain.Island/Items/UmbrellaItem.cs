using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;

namespace JohnnyLike.Domain.Island.Items;

/// <summary>
/// A crafted umbrella tool owned by a specific actor.
/// Offers DeployUmbrella during rain (when rain-protection buff is absent)
/// and HolsterUmbrella when the buff is active but rain has stopped.
/// </summary>
public class UmbrellaItem : ToolItem
{
    public const string RainProtectionBuffName = "rain_protection";

    public UmbrellaItem(string id, ActorId? ownerActorId = null)
        : base(id, "umbrella_tool", OwnershipType.Exclusive, baseDecayPerSecond: 0.0, maxOwners: 1)
    {
        OwnerActorId = ownerActorId;
    }

    public override void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        if (!CanActorUseTool(ctx.ActorId))
            return;

        var weather = ctx.World.GetItem<WeatherItem>("weather");
        var isRaining = weather?.Precipitation == PrecipitationBand.Rainy;
        var hasRainBuff = ctx.Actor.ActiveBuffs.Any(b => b.Name == RainProtectionBuffName);

        // Deploy: offered during rain when the buff is not yet active
        if (isRaining && !hasRainBuff)
        {
            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("deploy_umbrella"),
                    ActionKind.Interact,
                    new LocationActionParameters("camp"),
                    5.0
                ),
                0.7,
                "Deploy umbrella (rain protection)",
                EffectHandler: new Action<EffectContext>(effectCtx =>
                {
                    effectCtx.Actor.ActiveBuffs.Add(new ActiveBuff
                    {
                        Name = RainProtectionBuffName,
                        Type = BuffType.RainProtection,
                        SkillType = null,
                        Value = 1,
                        ExpiresAt = double.MaxValue
                    });
                    effectCtx.Actor.Morale += 5.0;
                }),
                Qualities: new Dictionary<QualityType, double>
                {
                    [QualityType.Comfort] = 0.8,
                    [QualityType.Safety]  = 0.5
                }
            ));
        }

        // Holster: offered when the buff is active but rain has stopped
        if (hasRainBuff && !isRaining)
        {
            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("holster_umbrella"),
                    ActionKind.Interact,
                    new LocationActionParameters("camp"),
                    3.0
                ),
                0.4,
                "Holster umbrella",
                EffectHandler: new Action<EffectContext>(effectCtx =>
                {
                    effectCtx.Actor.ActiveBuffs.RemoveAll(b => b.Name == RainProtectionBuffName);
                }),
                Qualities: new Dictionary<QualityType, double>
                {
                    [QualityType.Efficiency] = 0.3
                }
            ));
        }
    }
}
