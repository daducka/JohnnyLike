using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Telemetry;

namespace JohnnyLike.Domain.Island.Items;

/// <summary>
/// Represents the remains of a dead actor on the island.
/// Decays slowly over 14 sim-days (1,209,600 sim-seconds).
/// Other actors can discover and interact with the corpse in future features
/// (bury_body, mourn_actor, etc.).
/// </summary>
public class CorpseItem : MaintainableWorldItem
{
    /// <summary>14 sim-days in seconds (14 × 86 400).</summary>
    public const double DecayDurationSeconds = 14.0 * 86_400.0;

    /// <summary>Health points per sim-second of decay: 100 / 1 209 600 ≈ 8.27e-5.</summary>
    public const double CorpseDecayPerSecond = 100.0 / DecayDurationSeconds;

    /// <summary>Name of the actor whose remains this represents.</summary>
    public string ActorName { get; set; } = "Unknown";

    public CorpseItem(string id = "corpse")
        : base(id, "corpse", baseDecayPerSecond: CorpseDecayPerSecond)
    {
    }

    public override void Tick(long dtTicks, IslandWorldState world)
    {
        base.Tick(dtTicks, world);
        if (Quality <= 0.0)
            IsExpired = true;
    }

    protected override void EmitDegradationBeat(IEventTracer tracer, double threshold)
    {
        var description = threshold switch
        {
            >= 75.0 => "is beginning to decompose",
            >= 50.0 => "is noticeably decomposed",
            >= 25.0 => "is heavily decomposed",
            _ => "is barely recognisable"
        };
        using (tracer.PushPhase(TracePhase.WorldTick))
            tracer.BeatWorld(
                $"The remains of {ActorName} {description}.",
                subjectId: $"item:{Id}",
                priority: 30);
    }

    protected override void EmitBrokenBeat(IEventTracer tracer)
    {
        using (tracer.PushPhase(TracePhase.WorldTick))
            tracer.BeatWorld(
                $"The remains of {ActorName} have fully decomposed and returned to the earth.",
                subjectId: $"item:{Id}",
                priority: 40);
    }

    public override Dictionary<string, object> SerializeToDict()
    {
        var dict = base.SerializeToDict();
        dict["ActorName"] = ActorName;
        return dict;
    }

    public override void DeserializeFromDict(Dictionary<string, System.Text.Json.JsonElement> data)
    {
        base.DeserializeFromDict(data);
        if (data.TryGetValue("ActorName", out var nameEl))
            ActorName = nameEl.GetString() ?? "Unknown";
    }
}
