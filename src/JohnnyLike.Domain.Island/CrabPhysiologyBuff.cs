using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Metabolism;

namespace JohnnyLike.Domain.Island;

/// <summary>
/// A permanent buff that drives crab physiology each world tick.
///
/// Crab physiology is intentionally simpler than human physiology:
/// <list type="bullet">
///   <item>Extremely slow satiety drain (crabs have very low metabolic rate).</item>
///   <item>Extremely slow energy drain — near-zero rest deterioration.</item>
///   <item>No morale deterioration (crabs have no morale concept).</item>
///   <item>Health is driven primarily by satiety: starvation damage applies when
///         <see cref="StarvationSatietyThreshold"/> is crossed, but exhaustion and
///         psyche-strain damage do not apply.</item>
///   <item>Slow health recovery when satiety is sufficient.</item>
/// </list>
///
/// This buff replaces both <see cref="Metabolism.MetabolicBuff"/> and
/// <see cref="Vitality.VitalityBuff"/> on crab actors.
/// It is added to every crab in <see cref="IslandDomainPack.CreateCrabActorState"/>.
/// </summary>
public class CrabPhysiologyBuff : ActiveBuff, ITickableBuff
{
    // ── Satiety drain ────────────────────────────────────────────────────────
    /// <summary>
    /// Satiety drain per sim-second. Crabs have a very low metabolic rate —
    /// approximately 1/10 of the human basal rate, so satiety lasts ~10× longer.
    /// </summary>
    public const double SatietyDrainPerSecond = MetabolismMath.BasalKcalPerSecond
        * 0.1  // 10% of human basal
        / MetabolismMath.SatietyKcalAt100
        * 100.0;  // convert kcal/s → Satiety%/s  ≈ 0.000139 %/s

    // ── Energy drain ─────────────────────────────────────────────────────────
    /// <summary>
    /// Energy drain per sim-second. Crabs barely need active energy — roughly
    /// 1/20 of human light activity, giving extremely long time before exhaustion.
    /// </summary>
    public const double EnergyDrainPerSecond = 0.000050; // ~100% drain over ~23 sim-days

    // ── Energy recovery (rest/idle action) ───────────────────────────────────
    /// <summary>Energy recovered per sim-second during idle/rest action. Applied via PreAction.</summary>
    public const double EnergyRestRecoveryPerSecond = 0.00200; // ~100% recovery in ~14 hours rest

    // ── Starvation health damage ──────────────────────────────────────────────
    /// <summary>Satiety threshold below which starvation health damage begins.</summary>
    public const double StarvationSatietyThreshold = 10.0;
    /// <summary>Health damage per sim-second when satiety is critically low.</summary>
    public const double StarvationDamagePerSecond = 0.0003; // ~100% drain over ~3.9 sim-days

    // ── Health recovery ───────────────────────────────────────────────────────
    /// <summary>Minimum satiety required for health recovery.</summary>
    public const double RecoverySatietyMinimum = 50.0;
    /// <summary>Health recovered per sim-second when well-fed and resting.</summary>
    public const double RecoveryPerSecond = 0.0001; // ~100% recovery over ~11.6 sim-days

    /// <summary>Absolute engine tick at which <see cref="OnTick"/> was last invoked.</summary>
    public long LastTick { get; set; } = 0L;

    /// <inheritdoc/>
    public override string Describe(long currentTick) => $"{Name}(permanent)";

    /// <summary>
    /// Applies one crab physiology time-step:
    /// drains satiety and energy slowly, checks for starvation health damage,
    /// and applies slow health recovery when well-fed.
    /// </summary>
    public void OnTick(ActorState actorState, WorldState worldState, long currentTick)
    {
        if (actorState is not LivingActorState actor)
            return;

        var dtSeconds = (currentTick - LastTick) / (double)EngineConstants.TickHz;
        LastTick = currentTick;

        if (dtSeconds <= 0.0)
            return;

        // Skip health calculations for non-Alive crabs.
        var aliveness = actor.TryGetBuff<AlivenessBuff>();
        if (aliveness != null && aliveness.State != AlivenessState.Alive)
            return;

        // ── Satiety and energy drain ──────────────────────────────────────────
        actor.Satiety -= SatietyDrainPerSecond * dtSeconds;
        actor.Energy  -= EnergyDrainPerSecond  * dtSeconds;

        // ── Health: starvation damage (only health driver for crabs) ──────────
        if (actor.Satiety < StarvationSatietyThreshold)
        {
            var damage = StarvationDamagePerSecond * dtSeconds;
            actor.Health -= damage;

            if (actor.Health <= 0.0 && aliveness != null && aliveness.State == AlivenessState.Alive)
            {
                actor.Health = 0.0;
                MortalityWorkflow.Collapse(aliveness, actor.Id.Value, worldState.Tracer);
            }
        }
        else if (actor.Satiety >= RecoverySatietyMinimum)
        {
            // Slow health recovery when satiety is good.
            actor.Health += RecoveryPerSecond * dtSeconds;
        }
    }
}
