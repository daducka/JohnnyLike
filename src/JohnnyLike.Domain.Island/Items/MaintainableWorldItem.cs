using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Telemetry;
using System.Text.Json;

namespace JohnnyLike.Domain.Island;

public abstract class MaintainableWorldItem : WorldItem, IIslandActionCandidate
{
    // Quality thresholds that trigger a narration beat (descending order).
    private static readonly double[] QualityThresholds = { 75.0, 50.0, 25.0, 10.0 };

    public double Quality { get; set; } = 100.0;
    public double BaseDecayPerSecond { get; set; } = 0.01;
    public bool IsExpired { get; protected set; } = false;
    /// <summary>Tracks the last threshold crossed so we only emit each beat once.</summary>
    private double _lastThresholdEmitted = 100.0;

    protected MaintainableWorldItem(string id, string type, double baseDecayPerSecond = 0.01)
        : base(id, type)
    {
        BaseDecayPerSecond = baseDecayPerSecond;
    }

    public virtual void Tick(long dtTicks, IslandWorldState world)
    {
        var prevQuality = Quality;
        Quality = Math.Max(0.0, Quality - BaseDecayPerSecond * (dtTicks / (double)EngineConstants.TickHz));

        // Emit a beat only when crossing a threshold for the first time (descending).
        foreach (var threshold in QualityThresholds)
        {
            if (prevQuality > threshold && Quality <= threshold && _lastThresholdEmitted > threshold)
            {
                _lastThresholdEmitted = threshold;
                EmitDegradationBeat(world.Tracer, threshold);
                break; // Only one beat per tick
            }
        }

        // Broken transition
        if (prevQuality > 0.0 && Quality <= 0.0 && _lastThresholdEmitted > 0.0)
        {
            _lastThresholdEmitted = 0.0;
            EmitBrokenBeat(world.Tracer);
        }
    }

    /// <summary>
    /// Override to provide a domain-specific degradation message.
    /// Default produces a generic message using the item type.
    /// </summary>
    protected virtual void EmitDegradationBeat(IEventTracer tracer, double threshold)
    {
        var description = threshold switch
        {
            >= 75.0 => "showing early wear",
            >= 50.0 => "noticeably worn",
            >= 25.0 => "in poor condition",
            _ => "barely holding together"
        };
        using (tracer.PushPhase(TracePhase.WorldTick))
            tracer.BeatWorld($"The {Type} is {description}.", subjectId: $"item:{Id}", priority: 30);
    }

    /// <summary>
    /// Override to provide a domain-specific broken/destroyed message.
    /// Default produces a generic message using the item type.
    /// </summary>
    protected virtual void EmitBrokenBeat(IEventTracer tracer)
    {
        using (tracer.PushPhase(TracePhase.WorldTick))
            tracer.BeatWorld($"The {Type} has fallen apart and is no longer usable.", subjectId: $"item:{Id}", priority: 40);
    }

    /// <summary>
    /// Called when the item expires and is about to be removed from the world.
    /// Override this to perform custom expiration logic (e.g., spawn effects, update world state).
    /// </summary>
    public virtual void PerformExpiration(IslandWorldState world, IResourceAvailability? resourceAvailability)
    {
        // Default implementation does nothing
        // Subclasses can override to add custom expiration behavior
    }

    /// <summary>
    /// Add action candidates to the output list. Default implementation does nothing.
    /// Override to provide item-specific action candidates.
    /// </summary>
    public virtual void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        // Default implementation provides no candidates
        // Concrete items override this to provide their specific candidates
    }

    public override Dictionary<string, object> SerializeToDict()
    {
        var dict = base.SerializeToDict();
        dict["Quality"] = Quality;
        dict["BaseDecayPerSecond"] = BaseDecayPerSecond;
        return dict;
    }

    public override void DeserializeFromDict(Dictionary<string, JsonElement> data)
    {
        base.DeserializeFromDict(data);
        Quality = data["Quality"].GetDouble();
        BaseDecayPerSecond = data["BaseDecayPerSecond"].GetDouble();
        // Restore threshold tracker to the current quality level so we don't
        // immediately re-emit beats when deserializing degraded items.
        _lastThresholdEmitted = Quality;
    }
}
