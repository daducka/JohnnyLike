using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Domain.Island.Vitality;

/// <summary>
/// A permanent actor buff that drives health deterioration and recovery each world tick,
/// and applies passive morale pressure from low satiety or low energy.
///
/// <b>Deterioration sources (stackable):</b>
/// <list type="bullet">
///   <item>Starvation: Satiety &lt; <see cref="StarvationSatietyThreshold"/> → <see cref="StarvationDamagePerSecond"/> damage/s</item>
///   <item>Exhaustion: Energy &lt; <see cref="ExhaustionEnergyThreshold"/> → <see cref="ExhaustionDamagePerSecond"/> damage/s</item>
///   <item>Psyche strain: Morale &lt; <see cref="PsycheStrainMoraleThreshold"/> → <see cref="PsycheDamagePerSecond"/> damage/s</item>
/// </list>
///
/// <b>Physiological morale pressure:</b>
/// Low satiety and low energy each independently apply morale decay per second:
/// mild → <see cref="MoraleMildPressurePerSecond"/>,
/// moderate → <see cref="MoraleModeratePressurePerSecond"/>,
/// strong → <see cref="MoraleStrongPressurePerSecond"/>.
///
/// <b>Recovery:</b>
/// Slow health regeneration occurs only when all three stats are above their recovery thresholds
/// (<see cref="RecoverySatietyMinimum"/>, <see cref="RecoveryEnergyMinimum"/>, <see cref="RecoveryMoraleMinimum"/>).
///
/// Health moves more slowly than Satiety/Energy/Morale by design —
/// short hardship should mostly hurt those stats first; prolonged hardship damages health.
/// </summary>
public class VitalityBuff : ActiveBuff, ITickableBuff
{
    private static readonly Duration MoralePressureBeatInterval = Duration.Minutes(1);

    private long _lastMoralePressureBeatTick = long.MinValue;
    private double _pendingMoralePressureDelta = 0.0;
    private readonly Dictionary<string, double> _pendingMoralePressureByReason = new(StringComparer.Ordinal);

    // ── Deterioration thresholds ───────────────────────────────────────────────
    /// <summary>Satiety must be below this value to trigger starvation health damage.</summary>
    public const double StarvationSatietyThreshold = 20.0;
    /// <summary>Energy must be below this value to trigger exhaustion health damage.</summary>
    public const double ExhaustionEnergyThreshold  = 15.0;
    /// <summary>Morale must be below this value to trigger psyche-strain health damage.</summary>
    public const double PsycheStrainMoraleThreshold = 10.0;

    // ── Deterioration rates (health points lost per sim-second) ───────────────
    /// <summary>Health damage per sim-second when Satiety is critically low.</summary>
    public const double StarvationDamagePerSecond = 0.0006; // ~100% drain over ~2 sim-days of starvation
    /// <summary>Health damage per sim-second when Energy is critically low.</summary>
    public const double ExhaustionDamagePerSecond = 0.0004; // ~100% drain over ~3 sim-days of exhaustion
    /// <summary>Health damage per sim-second when Morale is critically low.</summary>
    public const double PsycheDamagePerSecond     = 0.0003; // ~100% drain over ~4 sim-days of despair

    // ── Physiological morale pressure thresholds ──────────────────────────────
    /// <summary>Satiety below this value applies mild morale pressure.</summary>
    public const double SatietyMildMoraleThreshold     = 35.0;
    /// <summary>Satiety below this value applies moderate morale pressure.</summary>
    public const double SatietyModerateMoraleThreshold = 20.0;
    /// <summary>Satiety below this value applies strong morale pressure.</summary>
    public const double SatietyStrongMoraleThreshold   = 10.0;

    /// <summary>Energy below this value applies mild morale pressure.</summary>
    public const double EnergyMildMoraleThreshold     = 30.0;
    /// <summary>Energy below this value applies moderate morale pressure.</summary>
    public const double EnergyModerateMoraleThreshold = 15.0;
    /// <summary>Energy below this value applies strong morale pressure.</summary>
    public const double EnergyStrongMoraleThreshold   = 5.0;

    // ── Physiological morale pressure rates (morale lost per sim-second) ─────
    /// <summary>Mild morale decay per sim-second from physiological distress (~50 morale over ~2 sim-days).</summary>
    public const double MoraleMildPressurePerSecond     = 0.0003;
    /// <summary>Moderate morale decay per sim-second from physiological distress (~50 morale over ~0.8 sim-days).</summary>
    public const double MoraleModeratePressurePerSecond = 0.0007;
    /// <summary>Strong morale decay per sim-second from physiological distress (~50 morale over ~0.5 sim-days).</summary>
    public const double MoraleStrongPressurePerSecond   = 0.0012;

    // ── Recovery thresholds (all must be met simultaneously for regen) ────────
    /// <summary>Satiety must be at or above this value for health recovery to occur.</summary>
    public const double RecoverySatietyMinimum = 60.0;
    /// <summary>Energy must be at or above this value for health recovery to occur.</summary>
    public const double RecoveryEnergyMinimum  = 60.0;
    /// <summary>Morale must be at or above this value for health recovery to occur.</summary>
    public const double RecoveryMoraleMinimum  = 50.0;

    // ── Recovery rate ─────────────────────────────────────────────────────────
    /// <summary>Health recovered per sim-second under stable/good conditions.</summary>
    public const double RecoveryPerSecond = 0.0002; // ~100% recovery over ~5 sim-days of stable conditions

    /// <summary>
    /// Absolute engine tick at which <see cref="OnTick"/> was last invoked.
    /// Used to compute the elapsed delta between ticks.
    /// </summary>
    public long LastTick { get; set; } = 0L;

    /// <summary>
    /// Applies one vitality time-step: computes health deterioration from starvation, exhaustion,
    /// and psyche strain, or health recovery when conditions are stable, then clamps health to [0, 100].
    /// Also applies passive morale pressure from low satiety and low energy.
    /// </summary>
    public void OnTick(ActorState actorState, WorldState worldState, long currentTick)
    {
        if (actorState is not IslandActorState actor)
            return;

        var dtSeconds = (currentTick - LastTick) / (double)EngineConstants.TickHz;
        LastTick = currentTick;

        if (dtSeconds <= 0.0)
            return;

        var oldHealth = actor.Health;
        var healthDelta = 0.0;
        var reasons = new List<string>(3);

        // ── Deterioration ────────────────────────────────────────────────────
        if (actor.Satiety < StarvationSatietyThreshold)
        {
            var damage = StarvationDamagePerSecond * dtSeconds;
            healthDelta -= damage;
            reasons.Add($"starvation(-{damage:F3})");
        }

        if (actor.Energy < ExhaustionEnergyThreshold)
        {
            var damage = ExhaustionDamagePerSecond * dtSeconds;
            healthDelta -= damage;
            reasons.Add($"exhaustion(-{damage:F3})");
        }

        if (actor.Morale < PsycheStrainMoraleThreshold)
        {
            var damage = PsycheDamagePerSecond * dtSeconds;
            healthDelta -= damage;
            reasons.Add($"psyche_strain(-{damage:F3})");
        }

        // ── Recovery ─────────────────────────────────────────────────────────
        // Recovery only occurs when no deterioration sources are active and all conditions
        // are stable/good.
        if (healthDelta == 0.0
            && actor.Satiety >= RecoverySatietyMinimum
            && actor.Energy  >= RecoveryEnergyMinimum
            && actor.Morale  >= RecoveryMoraleMinimum)
        {
            var regen = RecoveryPerSecond * dtSeconds;
            healthDelta += regen;
            reasons.Add($"recovery(+{regen:F3})");
        }

        if (healthDelta != 0.0)
        {
            // Apply and clamp.
            var newHealth = Math.Clamp(oldHealth + healthDelta, 0.0, 100.0);
            actor.Health = newHealth;

            // ── Health trace ─────────────────────────────────────────────────
            // Only emit trace when the change is meaningful (avoid float noise spam).
            var actualDelta = newHealth - oldHealth;
            if (Math.Abs(actualDelta) >= 0.001)
            {
                var reasonStr = string.Join(", ", reasons);
                worldState.Tracer.Beat(
                    $"[VitalityBuff] health {oldHealth:F1} → {newHealth:F1} ({actualDelta:+0.000;-0.000}) | {reasonStr}",
                    actorId: actor.Id.Value,
                    priority: 30);
            }
        }

        // ── Physiological morale pressure ────────────────────────────────────
        ApplyPhysiologicalMoralePressure(actor, worldState, dtSeconds, currentTick);
    }

    /// <summary>
    /// Applies passive morale pressure from low satiety and low energy.
    /// Each physiological stat independently contributes mild, moderate, or strong morale decay
    /// depending on how distressed the actor is.
    /// </summary>
    private void ApplyPhysiologicalMoralePressure(
        IslandActorState actor,
        WorldState worldState,
        double dtSeconds,
        long currentTick)
    {
        var pressures = new List<(double delta, string reason)>(2);

        // Satiety-based morale pressure (only the highest applicable tier applies).
        if (actor.Satiety < SatietyStrongMoraleThreshold)
        {
            var pressure = MoraleStrongPressurePerSecond * dtSeconds;
            pressures.Add((-pressure, "Low satiety pressure"));
        }
        else if (actor.Satiety < SatietyModerateMoraleThreshold)
        {
            var pressure = MoraleModeratePressurePerSecond * dtSeconds;
            pressures.Add((-pressure, "Low satiety pressure"));
        }
        else if (actor.Satiety < SatietyMildMoraleThreshold)
        {
            var pressure = MoraleMildPressurePerSecond * dtSeconds;
            pressures.Add((-pressure, "Low satiety pressure"));
        }

        // Energy-based morale pressure (only the highest applicable tier applies).
        if (actor.Energy < EnergyStrongMoraleThreshold)
        {
            var pressure = MoraleStrongPressurePerSecond * dtSeconds;
            pressures.Add((-pressure, "Low energy pressure"));
        }
        else if (actor.Energy < EnergyModerateMoraleThreshold)
        {
            var pressure = MoraleModeratePressurePerSecond * dtSeconds;
            pressures.Add((-pressure, "Low energy pressure"));
        }
        else if (actor.Energy < EnergyMildMoraleThreshold)
        {
            var pressure = MoraleMildPressurePerSecond * dtSeconds;
            pressures.Add((-pressure, "Low energy pressure"));
        }

        if (pressures.Count == 0)
        {
            EmitPendingMoralePressureBeat(actor, worldState, currentTick, force: true);
            return;
        }

        // Apply all pressures and batch narration beats to avoid per-tick spam.
        foreach (var (delta, reason) in pressures)
        {
            var before = actor.Morale;
            actor.Morale += delta;
            var applied = actor.Morale - before;

            if (Math.Abs(applied) >= 0.0001)
            {
                _pendingMoralePressureDelta += applied;
                _pendingMoralePressureByReason[reason] =
                    _pendingMoralePressureByReason.GetValueOrDefault(reason, 0.0) + applied;
            }
        }

        EmitPendingMoralePressureBeat(actor, worldState, currentTick, force: false);
    }

    private void EmitPendingMoralePressureBeat(
        IslandActorState actor,
        WorldState worldState,
        long currentTick,
        bool force)
    {
        if (Math.Abs(_pendingMoralePressureDelta) < 0.0001)
            return;

        var cooldownElapsed = _lastMoralePressureBeatTick == long.MinValue
            || currentTick - _lastMoralePressureBeatTick >= MoralePressureBeatInterval.Ticks;
        if (!force && !cooldownElapsed)
            return;

        var reasonText = string.Join(
            ", ",
            _pendingMoralePressureByReason
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => $"{kvp.Key}({kvp.Value:+0.0000;-0.0000})"));

        worldState.Tracer.Beat(
            $"[Morale] {_pendingMoralePressureDelta:+0.0000;-0.0000} ({reasonText})",
            actorId: actor.Id.Value,
            priority: 25);

        _lastMoralePressureBeatTick = currentTick;
        _pendingMoralePressureDelta = 0.0;
        _pendingMoralePressureByReason.Clear();
    }
}
