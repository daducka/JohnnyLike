using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Domain.Island.Metabolism;

/// <summary>
/// Intensity level used by <see cref="MetabolicBuff"/> to determine how much Energy
/// is drained (or recovered) each tick.
/// </summary>
public enum MetabolicIntensity
{
    /// <summary>Default resting/walking activity. Satiety→Energy conversion fully offsets drain.</summary>
    Light,
    /// <summary>Moderate exertion (hiking, fishing). Slightly elevated Energy drain.</summary>
    Moderate,
    /// <summary>Vigorous exertion (swimming). Net Energy drain ≈ 0.5 points/sim-s after conversion.</summary>
    Heavy,
    /// <summary>Actor is sleeping. Energy recovers via sleep + conversion; no activity drain.</summary>
    Sleeping
}

/// <summary>
/// A permanent actor buff that drives all metabolic effects each world tick:
/// basal Satiety drain, activity-level Energy drain, sleep recovery, and the
/// Satiety→Energy conversion.
///
/// This buff is added to every actor in <c>CreateActorState</c> and never expires
/// (<c>ExpiresAtTick = long.MaxValue</c>).
///
/// <b>Intensity transitions:</b>
/// <list type="bullet">
///   <item>Set to <see cref="MetabolicIntensity.Heavy"/> in the swim PreAction.</item>
///   <item>Set to <see cref="MetabolicIntensity.Sleeping"/> in the sleep PreAction.</item>
///   <item>Reset to <see cref="MetabolicIntensity.Light"/> in <c>ApplyActionEffects</c>
///         after any action completes.</item>
/// </list>
/// </summary>
public class MetabolicBuff : ActiveBuff, ITickableBuff
{
    /// <summary>Current activity level, determines which <see cref="MetabolismMath"/> constants are used.</summary>
    public MetabolicIntensity Intensity { get; set; } = MetabolicIntensity.Light;

    /// <summary>
    /// Absolute engine tick at which <see cref="OnTick"/> was last invoked.
    /// Used to compute the elapsed delta between ticks without storing a reference to the engine.
    /// Initialised to 0; on the first tick the delta equals <c>currentTick / TickHz</c> seconds.
    /// </summary>
    public long LastTick { get; set; } = 0L;

    /// <summary>
    /// Applies one metabolism time-step using <see cref="MetabolismMath.ApplyTimeStep"/>.
    /// The elapsed time is derived from <paramref name="currentTick"/> minus <see cref="LastTick"/>.
    /// </summary>
    public void OnTick(ActorState actorState, WorldState worldState, long currentTick)
    {
        if (actorState is not IslandActorState actor)
            return;

        var dtSeconds = (currentTick - LastTick) / (double)EngineConstants.TickHz;
        LastTick = currentTick;

        if (dtSeconds <= 0.0)
            return;

        var (activityKcal, isSleeping) = Intensity switch
        {
            MetabolicIntensity.Light    => (MetabolismMath.LightActivityKcalPerSecond,    false),
            MetabolicIntensity.Moderate => (MetabolismMath.ModerateActivityKcalPerSecond, false),
            MetabolicIntensity.Heavy    => (MetabolismMath.HeavyActivityKcalPerSecond,    false),
            MetabolicIntensity.Sleeping => (0.0,                                          true),
            _ => throw new InvalidOperationException($"Unhandled MetabolicIntensity value: {Intensity}")
        };

        var satiety = actor.Satiety;
        var energy  = actor.Energy;
        MetabolismMath.ApplyTimeStep(ref satiety, ref energy, dtSeconds, activityKcal, isSleeping);
        actor.Satiety = satiety;
        actor.Energy  = energy;
    }
}
