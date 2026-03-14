using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Domain.Island.Vitality;

/// <summary>
/// A permanent actor buff that drives health deterioration and recovery each world tick.
///
/// <b>Deterioration sources (stackable):</b>
/// <list type="bullet">
///   <item>Starvation: Satiety &lt; <see cref="StarvationSatietyThreshold"/> → <see cref="StarvationDamagePerSecond"/> damage/s</item>
///   <item>Exhaustion: Energy &lt; <see cref="ExhaustionEnergyThreshold"/> → <see cref="ExhaustionDamagePerSecond"/> damage/s</item>
///   <item>Psyche strain: Morale &lt; <see cref="PsycheStrainMoraleThreshold"/> → <see cref="PsycheDamagePerSecond"/> damage/s</item>
/// </list>
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

        if (healthDelta == 0.0)
            return;

        // Apply and clamp.
        var newHealth = Math.Clamp(oldHealth + healthDelta, 0.0, 100.0);
        actor.Health = newHealth;

        // ── Trace ─────────────────────────────────────────────────────────────
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
}
