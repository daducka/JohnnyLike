using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Kit.Dice;
using System.Text.Json;

namespace JohnnyLike.Domain.Island.Items;

/// <summary>
/// Represents an actual sand castle on the beach.
/// Decays over time, faster during rain and high tide.
/// </summary>
public class SandCastleItem : MaintainableWorldItem
{
    private static readonly ResourceId BeachSandcastleArea = new("island:resource:beach:sandcastle_area");

    public SandCastleItem(string id = "sandcastle")
        : base(id, "sandcastle", baseDecayPerSecond: 0.02)
    {
        Quality = 100.0;
    }

    public override void Tick(long dtTicks, IslandWorldState world)
    {
        // Calculate decay rate based on environmental conditions
        var decayRate = BaseDecayPerSecond;
        
        // Decay faster in rain
        var weather = world.GetItem<WeatherItem>("weather");
        if (weather?.Precipitation == PrecipitationBand.Rainy)
        {
            decayRate *= 5.0; // 5x faster decay in rain
        }
        
        // Decay faster at high tide
        var beach = world.GetItem<BeachItem>("beach");
        if (beach?.Tide == TideLevel.High)
        {
            decayRate *= 3.0; // 3x faster decay at high tide
        }
        
        // Apply decay
        Quality = Math.Max(0.0, Quality - decayRate * (dtTicks / (double)EngineConstants.TickHz));
        
        // Mark as expired when quality reaches 0
        if (Quality <= 0.0)
        {
            IsExpired = true;
        }
    }

    public override void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        // Only provide stomp action if actor's morale is low and sand castle quality is less than half
        if (ctx.Actor.Morale >= 30.0 || Quality >= 50.0)
            return;

        var baseScore = 0.5; // Moderate priority for catharsis
        var parameters = new EmptyActionParameters();

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("stomp_on_sandcastle"),
                ActionKind.Interact,
                parameters,
                EngineConstants.TimeToTicks(5.0),
                null,
                new List<ResourceRequirement> { new ResourceRequirement(BeachSandcastleArea) }
            ),
            baseScore,
            "Stomp on sandcastle",
            EffectHandler: new Action<EffectContext>(effectCtx =>
            {
                // Destroy the sand castle
                Quality = 0.0;
                IsExpired = true;
                effectCtx.World.WorldItems.Remove(this);

                // Grant a large morale boost (cathartic release)
                effectCtx.Actor.Morale += 30.0;
            })
        ));
    }
}
