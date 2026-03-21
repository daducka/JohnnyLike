namespace JohnnyLike.Domain.Island;

/// <summary>
/// Centralized tunable decision-policy parameters for the Island domain.
/// The default instance (<see cref="Default"/>) reproduces the original production behaviour exactly.
/// Construct a custom profile to experiment with alternative scoring parameters without touching
/// production logic.
/// </summary>
public sealed class DecisionTuningProfile
{
    /// <summary>The production-default tuning profile. All scoring paths use this unless overridden.</summary>
    public static DecisionTuningProfile Default { get; } = new();

    /// <summary>Need-urgency scaling parameters (pressure → quality weight).</summary>
    public NeedTuning Need { get; init; } = new();

    /// <summary>Mood-multiplier suppression parameters (critical-state → personality dampening).</summary>
    public MoodTuning Mood { get; init; } = new();

    /// <summary>Personality-trait response shaping and DecisionPragmatism derivation constants.</summary>
    public PersonalityTuning Personality { get; init; } = new();

    /// <summary>Category-level bonus/penalty coefficients. Currently a placeholder for future use.</summary>
    public CategoryTuning Categories { get; init; } = new();
}

// ─────────────────────────────────────────────────────────────────────────────
// Need tuning
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Parameters that translate physiological pressures (satiety deficit, fatigue, injury, …)
/// into additive quality-weight urgency values.
/// </summary>
public sealed class NeedTuning
{
    // ── Pressure-to-need scale factors ────────────────────────────────────────
    // All pressures are in [0, 100]; scale factors keep derived weights comparable.

    /// <summary>Scales fatigue pressure (100 − Energy) into Rest need urgency. Max +1.5 at Energy=0.</summary>
    public double FatiguePressureRestScale { get; init; } = 0.015;

    /// <summary>Scales misery pressure (100 − Morale) into Comfort need urgency. Max +1.0 at Morale=0.</summary>
    public double MiseryPressureComfortScale { get; init; } = 0.01;

    /// <summary>Safety need urgency per point of injuryPressure. Max +2.5 at 0 HP.</summary>
    public double InjurySafetyNeedScale { get; init; } = 0.025;

    /// <summary>Rest need urgency per point of injuryPressure (stacks with fatigue). Max +1.0 at 0 HP.</summary>
    public double InjuryRestNeedScale { get; init; } = 0.010;

    /// <summary>Comfort need urgency per point of injuryPressure (stacks with misery). Max +0.5 at 0 HP.</summary>
    public double InjuryComfortNeedScale { get; init; } = 0.005;

    // ── Staged hunger ramp thresholds ─────────────────────────────────────────
    // Hunger urgency only builds meaningfully below certain satiety thresholds
    // so actors don't seek food when already satisfied.

    /// <summary>Satiety at or above which hunger urgency is zero; also the top of the mild urgency band.</summary>
    public double SatietyRampMild { get; init; } = 70.0;

    /// <summary>Satiety below which moderate urgency begins.</summary>
    public double SatietyRampModerate { get; init; } = 50.0;

    /// <summary>Satiety below which strong urgency begins.</summary>
    public double SatietyRampStrong { get; init; } = 30.0;

    /// <summary>Maximum hunger urgency in the mild band (Satiety 50–70).</summary>
    public double HungerMildMax { get; init; } = 0.3;

    /// <summary>Additional hunger urgency added across the moderate band (Satiety 30–50).</summary>
    public double HungerModerateRange { get; init; } = 1.2;

    /// <summary>Additional hunger urgency added across the strong band (Satiety 0–30).</summary>
    public double HungerStrongRange { get; init; } = 0.5;

    // ── Food availability split ────────────────────────────────────────────────

    /// <summary>Number of food units that counts as "plenty" for normalization purposes.</summary>
    public double FoodAvailabilityNormCap { get; init; } = 5.0;

    /// <summary>Normalised immediate-food ratio above which the immediate-food path activates.</summary>
    public double ImmediateFoodSignificanceThreshold { get; init; } = 0.2;

    /// <summary>Normalised acquirable-food ratio above which the acquirable-food path activates.</summary>
    public double AcquirableFoodSignificanceThreshold { get; init; } = 0.2;

    /// <summary>FoodConsumption share of hunger when immediate food is plentiful.</summary>
    public double FoodConsumptionShareHigh { get; init; } = 0.80;

    /// <summary>FoodConsumption share of hunger when no immediate food but acquirable food exists.</summary>
    public double FoodConsumptionShareLow { get; init; } = 0.20;

    /// <summary>Neutral 50/50 share used when neither immediate nor acquirable food is available.</summary>
    public double FoodShareNeutral { get; init; } = 0.50;

    // ── Preparation time-pressure ─────────────────────────────────────────────

    /// <summary>Maximum bounded preparation urgency added by time-on-island pressure.</summary>
    public double PrepTimePressureCap { get; init; } = 0.20;

    /// <summary>Preparation urgency gained per in-sim day stranded.</summary>
    public double PrepTimePressureRatePerDay { get; init; } = 0.05;
}

// ─────────────────────────────────────────────────────────────────────────────
// Mood tuning
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Parameters that suppress or amplify personality tendencies based on the actor's current
/// physiological/psychological state (starvation, exhaustion, injury, misery).
/// </summary>
public sealed class MoodTuning
{
    // ── Starvation suppression ─────────────────────────────────────────────────

    /// <summary>Satiety threshold below which the actor is considered starving, suppressing Preparation.</summary>
    public double StarvatingSatietyThreshold { get; init; } = 20.0;

    /// <summary>Preparation multiplier floor when the actor is starving.</summary>
    public double PrepStarvationFloor { get; init; } = 0.3;

    // ── Exhaustion suppression ─────────────────────────────────────────────────

    /// <summary>Energy threshold below which the actor is considered exhausted, suppressing Mastery.</summary>
    public double ExhaustedEnergyThreshold { get; init; } = 20.0;

    /// <summary>Mastery multiplier floor when the actor is exhausted.</summary>
    public double MasteryExhaustionFloor { get; init; } = 0.4;

    // ── Fun modulation ─────────────────────────────────────────────────────────

    /// <summary>Base Fun multiplier scale — keeps fun weight below 0.6 even at maximum misery.</summary>
    public double FunBaseScale { get; init; } = 0.6;

    /// <summary>Critical-survival Fun suppression: reduces Fun weight to 35% when starving or exhausted.</summary>
    public double FunCriticalSurvivalScale { get; init; } = 0.35;

    /// <summary>Satiety threshold below which critical-survival Fun suppression activates.</summary>
    public double FunCriticalSatietyThreshold { get; init; } = 25.0;

    /// <summary>Energy threshold below which critical-survival Fun suppression activates.</summary>
    public double FunCriticalEnergyThreshold { get; init; } = 20.0;

    // ── Injury suppression floors ──────────────────────────────────────────────

    /// <summary>Minimum multiplier for Fun personality at 0 HP (suppressed to 15%).</summary>
    public double InjuryFunSuppressionFloor { get; init; } = 0.15;

    /// <summary>Minimum multiplier for Mastery personality at 0 HP (suppressed to 30%).</summary>
    public double InjuryMasterySuppressionFloor { get; init; } = 0.30;

    /// <summary>Minimum multiplier for Preparation personality at 0 HP (suppressed to 40%).</summary>
    public double InjuryPreparationSuppressionFloor { get; init; } = 0.40;
}

// ─────────────────────────────────────────────────────────────────────────────
// Personality tuning
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Parameters that shape how personality traits translate into stable quality-weight baselines
/// and how traits influence DecisionPragmatism (exploit/explore balance).
/// </summary>
public sealed class PersonalityTuning
{
    // ── Quality weight scales ──────────────────────────────────────────────────
    // Traits are normalised to [0,1]; these scales set the practical weight ceiling.

    /// <summary>Planner + Industrious traits → Preparation personality weight.</summary>
    public double PreparationScale { get; init; } = 0.7;

    /// <summary>Planner + Craftsman traits → Efficiency personality weight.</summary>
    public double EfficiencyScale { get; init; } = 0.6;

    /// <summary>Craftsman + Industrious traits → Mastery personality weight.</summary>
    public double MasteryScale { get; init; } = 0.6;

    /// <summary>Hedonist trait → Comfort personality weight.</summary>
    public double ComfortScale { get; init; } = 0.4;

    /// <summary>Survivor trait → Safety personality weight.</summary>
    public double SafetyScale { get; init; } = 0.3;

    /// <summary>Instinctive + Hedonist traits → FoodConsumption personality weight.</summary>
    public double FoodConsumptionScale { get; init; } = 0.2;

    /// <summary>Planner + Survivor traits → FoodAcquisition personality weight.</summary>
    public double FoodAcquisitionScale { get; init; } = 0.15;

    // ── DecisionPragmatism derivation ──────────────────────────────────────────
    // Planners and survivors tend toward exploitation (higher pragmatism);
    // hedonists and instinctive actors tend toward exploration (lower pragmatism).

    /// <summary>Base pragmatism before personality adjustments.</summary>
    public double PragmatismBase { get; init; } = 0.80;

    /// <summary>Planner trait contribution toward higher pragmatism (exploit).</summary>
    public double PragmatismPlannerScale { get; init; } = 0.10;

    /// <summary>Survivor trait contribution toward higher pragmatism (exploit).</summary>
    public double PragmatismSurvivorScale { get; init; } = 0.05;

    /// <summary>Hedonist trait contribution toward lower pragmatism (explore).</summary>
    public double PragmatismHedonistScale { get; init; } = 0.06;

    /// <summary>Instinctive trait contribution toward lower pragmatism (explore).</summary>
    public double PragmatismInstinctiveScale { get; init; } = 0.04;

    /// <summary>Minimum derived DecisionPragmatism — keeps actors coherent even at max spontaneity.</summary>
    public double PragmatismMin { get; init; } = 0.65;

    /// <summary>Maximum derived DecisionPragmatism — keeps explore branch reachable.</summary>
    public double PragmatismMax { get; init; } = 0.98;
}

// ─────────────────────────────────────────────────────────────────────────────
// Category tuning
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Category-level bonus/penalty coefficients.
/// Currently a placeholder for future per-category scoring knobs (e.g. immediacy bonuses).
/// </summary>
public sealed class CategoryTuning
{
}
