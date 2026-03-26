using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.SimRunner.PressureFuzzer;

namespace JohnnyLike.SimRunner.Optimizer;

// ─── Tunable parameter spec ───────────────────────────────────────────────────

/// <summary>
/// Describes a single tunable parameter: its bounds, step size, and how to read/write it
/// on a <see cref="DecisionTuningProfile"/>.
/// </summary>
public sealed record TunableParameter(
    string Name,
    double Min,
    double Max,
    double Step,
    Func<DecisionTuningProfile, double> Getter,
    Func<DecisionTuningProfile, double, DecisionTuningProfile> Setter);

// ─── Optimizer options ────────────────────────────────────────────────────────

/// <summary>
/// Input options for a single optimizer run.
/// </summary>
public sealed record OptimizerOptions(
    /// <summary>Starting profile. Defaults to <see cref="DecisionTuningProfile.Default"/>.</summary>
    DecisionTuningProfile? BaseProfile = null,
    /// <summary>Golden states to optimize against. Defaults to the embedded dataset.</summary>
    IReadOnlyList<GoldenStateEntry>? GoldenStates = null,
    /// <summary>
    /// Tunable parameters with search bounds. Defaults to <see cref="OptimizerRunner.DefaultParameters"/>.
    /// </summary>
    IReadOnlyList<TunableParameter>? Parameters = null,
    /// <summary>Maximum coordinate-descent iterations (outer loops). Default: 20.</summary>
    int MaxIterations = 20);

// ─── Objective scoring ────────────────────────────────────────────────────────

// Scoring constants for the objective function.
// These are intentionally simple in v1.
file static class ObjectiveWeights
{
    // Strong reward when the desired top category wins.
    public const double DesiredTopCategoryReward  = 10.0;
    // Smaller reward when the desired category appears in top-N (but not top-1).
    public const double DesiredInTopNReward        = 2.0;
    // Modest reward when an acceptable (but not preferred) category wins.
    public const double AcceptableCategoryReward   = 3.0;
    // Penalty when a forbidden category wins.
    public const double ForbiddenCategoryPenalty   = 15.0;
}

// ─── Optimizer runner ─────────────────────────────────────────────────────────

/// <summary>
/// First-pass optimizer: coordinate descent over a bounded subset of
/// <see cref="DecisionTuningProfile"/> parameters, scored against curated golden states.
///
/// <para>
/// Search strategy: one full pass over all parameters per iteration.
/// Each parameter is perturbed by ±step; the best perturbation is applied if it improves
/// the objective. Continues until no improvement is found or <see cref="OptimizerOptions.MaxIterations"/>
/// is reached.
/// </para>
/// </summary>
public static class OptimizerRunner
{
    // ── Default tunable parameter set ─────────────────────────────────────────
    // A constrained, interpretable subset of need/mood parameters as recommended in
    // the issue guidance. This avoids optimising every knob at once.

    /// <summary>
    /// Default set of tunable parameters for v1 optimizer runs.
    /// Covers: hunger curve multipliers/thresholds, food availability bias,
    /// comfort/safety/rest scaling, and prep/fun suppression under distress.
    /// </summary>
    public static IReadOnlyList<TunableParameter> DefaultParameters { get; } = BuildDefaultParameters();

    private static IReadOnlyList<TunableParameter> BuildDefaultParameters() =>
    [
        // ── Hunger curve multipliers ───────────────────────────────────────
        new("HungerMildMax",
            Min: 0.1, Max: 1.0, Step: 0.05,
            Getter: p => p.Need.HungerMildMax,
            Setter: (p, v) => WithNeed(p, v,
                (n, val) => new NeedTuning
                {
                    FatiguePressureRestScale           = n.FatiguePressureRestScale,
                    MiseryPressureComfortScale          = n.MiseryPressureComfortScale,
                    InjurySafetyNeedScale               = n.InjurySafetyNeedScale,
                    InjuryRestNeedScale                 = n.InjuryRestNeedScale,
                    InjuryComfortNeedScale              = n.InjuryComfortNeedScale,
                    SatietyRampMild                    = n.SatietyRampMild,
                    SatietyRampModerate                = n.SatietyRampModerate,
                    SatietyRampStrong                  = n.SatietyRampStrong,
                    HungerMildMax                      = val,
                    HungerModerateRange                = n.HungerModerateRange,
                    HungerStrongRange                  = n.HungerStrongRange,
                    FoodAvailabilityNormCap            = n.FoodAvailabilityNormCap,
                    ImmediateFoodSignificanceThreshold = n.ImmediateFoodSignificanceThreshold,
                    AcquirableFoodSignificanceThreshold = n.AcquirableFoodSignificanceThreshold,
                    FoodConsumptionShareHigh           = n.FoodConsumptionShareHigh,
                    FoodConsumptionShareLow            = n.FoodConsumptionShareLow,
                    FoodShareNeutral                   = n.FoodShareNeutral,
                    PrepTimePressureCap                = n.PrepTimePressureCap,
                    PrepTimePressureRatePerDay         = n.PrepTimePressureRatePerDay,
                })),

        new("HungerModerateRange",
            Min: 0.3, Max: 3.0, Step: 0.1,
            Getter: p => p.Need.HungerModerateRange,
            Setter: (p, v) => WithNeed(p, v,
                (n, val) => new NeedTuning
                {
                    FatiguePressureRestScale           = n.FatiguePressureRestScale,
                    MiseryPressureComfortScale          = n.MiseryPressureComfortScale,
                    InjurySafetyNeedScale               = n.InjurySafetyNeedScale,
                    InjuryRestNeedScale                 = n.InjuryRestNeedScale,
                    InjuryComfortNeedScale              = n.InjuryComfortNeedScale,
                    SatietyRampMild                    = n.SatietyRampMild,
                    SatietyRampModerate                = n.SatietyRampModerate,
                    SatietyRampStrong                  = n.SatietyRampStrong,
                    HungerMildMax                      = n.HungerMildMax,
                    HungerModerateRange                = val,
                    HungerStrongRange                  = n.HungerStrongRange,
                    FoodAvailabilityNormCap            = n.FoodAvailabilityNormCap,
                    ImmediateFoodSignificanceThreshold = n.ImmediateFoodSignificanceThreshold,
                    AcquirableFoodSignificanceThreshold = n.AcquirableFoodSignificanceThreshold,
                    FoodConsumptionShareHigh           = n.FoodConsumptionShareHigh,
                    FoodConsumptionShareLow            = n.FoodConsumptionShareLow,
                    FoodShareNeutral                   = n.FoodShareNeutral,
                    PrepTimePressureCap                = n.PrepTimePressureCap,
                    PrepTimePressureRatePerDay         = n.PrepTimePressureRatePerDay,
                })),

        new("HungerStrongRange",
            Min: 0.1, Max: 2.0, Step: 0.05,
            Getter: p => p.Need.HungerStrongRange,
            Setter: (p, v) => WithNeed(p, v,
                (n, val) => new NeedTuning
                {
                    FatiguePressureRestScale           = n.FatiguePressureRestScale,
                    MiseryPressureComfortScale          = n.MiseryPressureComfortScale,
                    InjurySafetyNeedScale               = n.InjurySafetyNeedScale,
                    InjuryRestNeedScale                 = n.InjuryRestNeedScale,
                    InjuryComfortNeedScale              = n.InjuryComfortNeedScale,
                    SatietyRampMild                    = n.SatietyRampMild,
                    SatietyRampModerate                = n.SatietyRampModerate,
                    SatietyRampStrong                  = n.SatietyRampStrong,
                    HungerMildMax                      = n.HungerMildMax,
                    HungerModerateRange                = n.HungerModerateRange,
                    HungerStrongRange                  = val,
                    FoodAvailabilityNormCap            = n.FoodAvailabilityNormCap,
                    ImmediateFoodSignificanceThreshold = n.ImmediateFoodSignificanceThreshold,
                    AcquirableFoodSignificanceThreshold = n.AcquirableFoodSignificanceThreshold,
                    FoodConsumptionShareHigh           = n.FoodConsumptionShareHigh,
                    FoodConsumptionShareLow            = n.FoodConsumptionShareLow,
                    FoodShareNeutral                   = n.FoodShareNeutral,
                    PrepTimePressureCap                = n.PrepTimePressureCap,
                    PrepTimePressureRatePerDay         = n.PrepTimePressureRatePerDay,
                })),

        // ── Immediate food availability bias ───────────────────────────────
        new("FoodConsumptionShareHigh",
            Min: 0.5, Max: 1.0, Step: 0.05,
            Getter: p => p.Need.FoodConsumptionShareHigh,
            Setter: (p, v) => WithNeed(p, v,
                (n, val) => new NeedTuning
                {
                    FatiguePressureRestScale           = n.FatiguePressureRestScale,
                    MiseryPressureComfortScale          = n.MiseryPressureComfortScale,
                    InjurySafetyNeedScale               = n.InjurySafetyNeedScale,
                    InjuryRestNeedScale                 = n.InjuryRestNeedScale,
                    InjuryComfortNeedScale              = n.InjuryComfortNeedScale,
                    SatietyRampMild                    = n.SatietyRampMild,
                    SatietyRampModerate                = n.SatietyRampModerate,
                    SatietyRampStrong                  = n.SatietyRampStrong,
                    HungerMildMax                      = n.HungerMildMax,
                    HungerModerateRange                = n.HungerModerateRange,
                    HungerStrongRange                  = n.HungerStrongRange,
                    FoodAvailabilityNormCap            = n.FoodAvailabilityNormCap,
                    ImmediateFoodSignificanceThreshold = n.ImmediateFoodSignificanceThreshold,
                    AcquirableFoodSignificanceThreshold = n.AcquirableFoodSignificanceThreshold,
                    FoodConsumptionShareHigh           = val,
                    FoodConsumptionShareLow            = n.FoodConsumptionShareLow,
                    FoodShareNeutral                   = n.FoodShareNeutral,
                    PrepTimePressureCap                = n.PrepTimePressureCap,
                    PrepTimePressureRatePerDay         = n.PrepTimePressureRatePerDay,
                })),

        new("ImmediateFoodSignificanceThreshold",
            Min: 0.05, Max: 0.5, Step: 0.05,
            Getter: p => p.Need.ImmediateFoodSignificanceThreshold,
            Setter: (p, v) => WithNeed(p, v,
                (n, val) => new NeedTuning
                {
                    FatiguePressureRestScale           = n.FatiguePressureRestScale,
                    MiseryPressureComfortScale          = n.MiseryPressureComfortScale,
                    InjurySafetyNeedScale               = n.InjurySafetyNeedScale,
                    InjuryRestNeedScale                 = n.InjuryRestNeedScale,
                    InjuryComfortNeedScale              = n.InjuryComfortNeedScale,
                    SatietyRampMild                    = n.SatietyRampMild,
                    SatietyRampModerate                = n.SatietyRampModerate,
                    SatietyRampStrong                  = n.SatietyRampStrong,
                    HungerMildMax                      = n.HungerMildMax,
                    HungerModerateRange                = n.HungerModerateRange,
                    HungerStrongRange                  = n.HungerStrongRange,
                    FoodAvailabilityNormCap            = n.FoodAvailabilityNormCap,
                    ImmediateFoodSignificanceThreshold = val,
                    AcquirableFoodSignificanceThreshold = n.AcquirableFoodSignificanceThreshold,
                    FoodConsumptionShareHigh           = n.FoodConsumptionShareHigh,
                    FoodConsumptionShareLow            = n.FoodConsumptionShareLow,
                    FoodShareNeutral                   = n.FoodShareNeutral,
                    PrepTimePressureCap                = n.PrepTimePressureCap,
                    PrepTimePressureRatePerDay         = n.PrepTimePressureRatePerDay,
                })),

        // ── Acquirable food availability bias ──────────────────────────────
        new("FoodConsumptionShareLow",
            Min: 0.0, Max: 0.5, Step: 0.05,
            Getter: p => p.Need.FoodConsumptionShareLow,
            Setter: (p, v) => WithNeed(p, v,
                (n, val) => new NeedTuning
                {
                    FatiguePressureRestScale           = n.FatiguePressureRestScale,
                    MiseryPressureComfortScale          = n.MiseryPressureComfortScale,
                    InjurySafetyNeedScale               = n.InjurySafetyNeedScale,
                    InjuryRestNeedScale                 = n.InjuryRestNeedScale,
                    InjuryComfortNeedScale              = n.InjuryComfortNeedScale,
                    SatietyRampMild                    = n.SatietyRampMild,
                    SatietyRampModerate                = n.SatietyRampModerate,
                    SatietyRampStrong                  = n.SatietyRampStrong,
                    HungerMildMax                      = n.HungerMildMax,
                    HungerModerateRange                = n.HungerModerateRange,
                    HungerStrongRange                  = n.HungerStrongRange,
                    FoodAvailabilityNormCap            = n.FoodAvailabilityNormCap,
                    ImmediateFoodSignificanceThreshold = n.ImmediateFoodSignificanceThreshold,
                    AcquirableFoodSignificanceThreshold = n.AcquirableFoodSignificanceThreshold,
                    FoodConsumptionShareHigh           = n.FoodConsumptionShareHigh,
                    FoodConsumptionShareLow            = val,
                    FoodShareNeutral                   = n.FoodShareNeutral,
                    PrepTimePressureCap                = n.PrepTimePressureCap,
                    PrepTimePressureRatePerDay         = n.PrepTimePressureRatePerDay,
                })),

        new("AcquirableFoodSignificanceThreshold",
            Min: 0.05, Max: 0.5, Step: 0.05,
            Getter: p => p.Need.AcquirableFoodSignificanceThreshold,
            Setter: (p, v) => WithNeed(p, v,
                (n, val) => new NeedTuning
                {
                    FatiguePressureRestScale           = n.FatiguePressureRestScale,
                    MiseryPressureComfortScale          = n.MiseryPressureComfortScale,
                    InjurySafetyNeedScale               = n.InjurySafetyNeedScale,
                    InjuryRestNeedScale                 = n.InjuryRestNeedScale,
                    InjuryComfortNeedScale              = n.InjuryComfortNeedScale,
                    SatietyRampMild                    = n.SatietyRampMild,
                    SatietyRampModerate                = n.SatietyRampModerate,
                    SatietyRampStrong                  = n.SatietyRampStrong,
                    HungerMildMax                      = n.HungerMildMax,
                    HungerModerateRange                = n.HungerModerateRange,
                    HungerStrongRange                  = n.HungerStrongRange,
                    FoodAvailabilityNormCap            = n.FoodAvailabilityNormCap,
                    ImmediateFoodSignificanceThreshold = n.ImmediateFoodSignificanceThreshold,
                    AcquirableFoodSignificanceThreshold = val,
                    FoodConsumptionShareHigh           = n.FoodConsumptionShareHigh,
                    FoodConsumptionShareLow            = n.FoodConsumptionShareLow,
                    FoodShareNeutral                   = n.FoodShareNeutral,
                    PrepTimePressureCap                = n.PrepTimePressureCap,
                    PrepTimePressureRatePerDay         = n.PrepTimePressureRatePerDay,
                })),

        // ── Comfort / safety / rest scaling ───────────────────────────────
        new("FatiguePressureRestScale",
            Min: 0.005, Max: 0.05, Step: 0.005,
            Getter: p => p.Need.FatiguePressureRestScale,
            Setter: (p, v) => WithNeed(p, v,
                (n, val) => new NeedTuning
                {
                    FatiguePressureRestScale           = val,
                    MiseryPressureComfortScale          = n.MiseryPressureComfortScale,
                    InjurySafetyNeedScale               = n.InjurySafetyNeedScale,
                    InjuryRestNeedScale                 = n.InjuryRestNeedScale,
                    InjuryComfortNeedScale              = n.InjuryComfortNeedScale,
                    SatietyRampMild                    = n.SatietyRampMild,
                    SatietyRampModerate                = n.SatietyRampModerate,
                    SatietyRampStrong                  = n.SatietyRampStrong,
                    HungerMildMax                      = n.HungerMildMax,
                    HungerModerateRange                = n.HungerModerateRange,
                    HungerStrongRange                  = n.HungerStrongRange,
                    FoodAvailabilityNormCap            = n.FoodAvailabilityNormCap,
                    ImmediateFoodSignificanceThreshold = n.ImmediateFoodSignificanceThreshold,
                    AcquirableFoodSignificanceThreshold = n.AcquirableFoodSignificanceThreshold,
                    FoodConsumptionShareHigh           = n.FoodConsumptionShareHigh,
                    FoodConsumptionShareLow            = n.FoodConsumptionShareLow,
                    FoodShareNeutral                   = n.FoodShareNeutral,
                    PrepTimePressureCap                = n.PrepTimePressureCap,
                    PrepTimePressureRatePerDay         = n.PrepTimePressureRatePerDay,
                })),

        new("MiseryPressureComfortScale",
            Min: 0.002, Max: 0.03, Step: 0.002,
            Getter: p => p.Need.MiseryPressureComfortScale,
            Setter: (p, v) => WithNeed(p, v,
                (n, val) => new NeedTuning
                {
                    FatiguePressureRestScale           = n.FatiguePressureRestScale,
                    MiseryPressureComfortScale          = val,
                    InjurySafetyNeedScale               = n.InjurySafetyNeedScale,
                    InjuryRestNeedScale                 = n.InjuryRestNeedScale,
                    InjuryComfortNeedScale              = n.InjuryComfortNeedScale,
                    SatietyRampMild                    = n.SatietyRampMild,
                    SatietyRampModerate                = n.SatietyRampModerate,
                    SatietyRampStrong                  = n.SatietyRampStrong,
                    HungerMildMax                      = n.HungerMildMax,
                    HungerModerateRange                = n.HungerModerateRange,
                    HungerStrongRange                  = n.HungerStrongRange,
                    FoodAvailabilityNormCap            = n.FoodAvailabilityNormCap,
                    ImmediateFoodSignificanceThreshold = n.ImmediateFoodSignificanceThreshold,
                    AcquirableFoodSignificanceThreshold = n.AcquirableFoodSignificanceThreshold,
                    FoodConsumptionShareHigh           = n.FoodConsumptionShareHigh,
                    FoodConsumptionShareLow            = n.FoodConsumptionShareLow,
                    FoodShareNeutral                   = n.FoodShareNeutral,
                    PrepTimePressureCap                = n.PrepTimePressureCap,
                    PrepTimePressureRatePerDay         = n.PrepTimePressureRatePerDay,
                })),

        new("InjurySafetyNeedScale",
            Min: 0.005, Max: 0.10, Step: 0.005,
            Getter: p => p.Need.InjurySafetyNeedScale,
            Setter: (p, v) => WithNeed(p, v,
                (n, val) => new NeedTuning
                {
                    FatiguePressureRestScale           = n.FatiguePressureRestScale,
                    MiseryPressureComfortScale          = n.MiseryPressureComfortScale,
                    InjurySafetyNeedScale               = val,
                    InjuryRestNeedScale                 = n.InjuryRestNeedScale,
                    InjuryComfortNeedScale              = n.InjuryComfortNeedScale,
                    SatietyRampMild                    = n.SatietyRampMild,
                    SatietyRampModerate                = n.SatietyRampModerate,
                    SatietyRampStrong                  = n.SatietyRampStrong,
                    HungerMildMax                      = n.HungerMildMax,
                    HungerModerateRange                = n.HungerModerateRange,
                    HungerStrongRange                  = n.HungerStrongRange,
                    FoodAvailabilityNormCap            = n.FoodAvailabilityNormCap,
                    ImmediateFoodSignificanceThreshold = n.ImmediateFoodSignificanceThreshold,
                    AcquirableFoodSignificanceThreshold = n.AcquirableFoodSignificanceThreshold,
                    FoodConsumptionShareHigh           = n.FoodConsumptionShareHigh,
                    FoodConsumptionShareLow            = n.FoodConsumptionShareLow,
                    FoodShareNeutral                   = n.FoodShareNeutral,
                    PrepTimePressureCap                = n.PrepTimePressureCap,
                    PrepTimePressureRatePerDay         = n.PrepTimePressureRatePerDay,
                })),

        // ── Prep suppression under hunger ──────────────────────────────────
        new("StarvatingSatietyThreshold",
            Min: 5.0, Max: 40.0, Step: 2.5,
            Getter: p => p.Mood.StarvatingSatietyThreshold,
            Setter: (p, v) => WithMood(p, v,
                (m, val) => new MoodTuning
                {
                    StarvatingSatietyThreshold      = val,
                    PrepStarvationFloor             = m.PrepStarvationFloor,
                    ExhaustedEnergyThreshold        = m.ExhaustedEnergyThreshold,
                    MasteryExhaustionFloor          = m.MasteryExhaustionFloor,
                    FunBaseScale                    = m.FunBaseScale,
                    FunCriticalSurvivalScale        = m.FunCriticalSurvivalScale,
                    FunCriticalSatietyThreshold     = m.FunCriticalSatietyThreshold,
                    FunCriticalEnergyThreshold      = m.FunCriticalEnergyThreshold,
                    InjuryFunSuppressionFloor       = m.InjuryFunSuppressionFloor,
                    InjuryMasterySuppressionFloor   = m.InjuryMasterySuppressionFloor,
                    InjuryPreparationSuppressionFloor = m.InjuryPreparationSuppressionFloor,
                })),

        new("PrepStarvationFloor",
            Min: 0.0, Max: 1.0, Step: 0.05,
            Getter: p => p.Mood.PrepStarvationFloor,
            Setter: (p, v) => WithMood(p, v,
                (m, val) => new MoodTuning
                {
                    StarvatingSatietyThreshold      = m.StarvatingSatietyThreshold,
                    PrepStarvationFloor             = val,
                    ExhaustedEnergyThreshold        = m.ExhaustedEnergyThreshold,
                    MasteryExhaustionFloor          = m.MasteryExhaustionFloor,
                    FunBaseScale                    = m.FunBaseScale,
                    FunCriticalSurvivalScale        = m.FunCriticalSurvivalScale,
                    FunCriticalSatietyThreshold     = m.FunCriticalSatietyThreshold,
                    FunCriticalEnergyThreshold      = m.FunCriticalEnergyThreshold,
                    InjuryFunSuppressionFloor       = m.InjuryFunSuppressionFloor,
                    InjuryMasterySuppressionFloor   = m.InjuryMasterySuppressionFloor,
                    InjuryPreparationSuppressionFloor = m.InjuryPreparationSuppressionFloor,
                })),

        // ── Fun suppression under distress ────────────────────────────────
        new("FunCriticalSurvivalScale",
            Min: 0.0, Max: 1.0, Step: 0.05,
            Getter: p => p.Mood.FunCriticalSurvivalScale,
            Setter: (p, v) => WithMood(p, v,
                (m, val) => new MoodTuning
                {
                    StarvatingSatietyThreshold      = m.StarvatingSatietyThreshold,
                    PrepStarvationFloor             = m.PrepStarvationFloor,
                    ExhaustedEnergyThreshold        = m.ExhaustedEnergyThreshold,
                    MasteryExhaustionFloor          = m.MasteryExhaustionFloor,
                    FunBaseScale                    = m.FunBaseScale,
                    FunCriticalSurvivalScale        = val,
                    FunCriticalSatietyThreshold     = m.FunCriticalSatietyThreshold,
                    FunCriticalEnergyThreshold      = m.FunCriticalEnergyThreshold,
                    InjuryFunSuppressionFloor       = m.InjuryFunSuppressionFloor,
                    InjuryMasterySuppressionFloor   = m.InjuryMasterySuppressionFloor,
                    InjuryPreparationSuppressionFloor = m.InjuryPreparationSuppressionFloor,
                })),

        new("FunCriticalSatietyThreshold",
            Min: 5.0, Max: 50.0, Step: 2.5,
            Getter: p => p.Mood.FunCriticalSatietyThreshold,
            Setter: (p, v) => WithMood(p, v,
                (m, val) => new MoodTuning
                {
                    StarvatingSatietyThreshold      = m.StarvatingSatietyThreshold,
                    PrepStarvationFloor             = m.PrepStarvationFloor,
                    ExhaustedEnergyThreshold        = m.ExhaustedEnergyThreshold,
                    MasteryExhaustionFloor          = m.MasteryExhaustionFloor,
                    FunBaseScale                    = m.FunBaseScale,
                    FunCriticalSurvivalScale        = m.FunCriticalSurvivalScale,
                    FunCriticalSatietyThreshold     = val,
                    FunCriticalEnergyThreshold      = m.FunCriticalEnergyThreshold,
                    InjuryFunSuppressionFloor       = m.InjuryFunSuppressionFloor,
                    InjuryMasterySuppressionFloor   = m.InjuryMasterySuppressionFloor,
                    InjuryPreparationSuppressionFloor = m.InjuryPreparationSuppressionFloor,
                })),
    ];

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Runs the optimizer and returns the best profile found along with a full result report.
    /// </summary>
    public static OptimizerRunResult Run(OptimizerOptions options)
    {
        var baseProfile    = options.BaseProfile   ?? DecisionTuningProfile.Default;
        var goldenStates   = options.GoldenStates  ?? GoldenStateLoader.LoadEmbedded();
        var parameters     = options.Parameters    ?? DefaultParameters;
        var maxIterations  = options.MaxIterations;

        // Evaluate the base profile.
        var baseResults = EvaluateProfile(baseProfile, goldenStates);
        var baseScore   = baseResults.Sum(r => r.Score);

        // Coordinate descent search.
        var current      = baseProfile;
        var currentScore = baseScore;
        var iterations   = 0;

        for (int iter = 0; iter < maxIterations; iter++)
        {
            var improved = false;

            foreach (var param in parameters)
            {
                var currentValue = param.Getter(current);

                // Try +step and -step.
                foreach (var direction in new[] { +1.0, -1.0 })
                {
                    var newValue = currentValue + direction * param.Step;

                    // Clamp to bounds (with small epsilon for floating-point safety).
                    if (newValue < param.Min - 1e-9 || newValue > param.Max + 1e-9)
                        continue;
                    newValue = Math.Clamp(newValue, param.Min, param.Max);

                    var candidate      = param.Setter(current, newValue);
                    var candidateScore = EvaluateProfile(candidate, goldenStates).Sum(r => r.Score);

                    if (candidateScore > currentScore)
                    {
                        current       = candidate;
                        currentScore  = candidateScore;
                        improved      = true;
                        break; // Move on to the next parameter after finding an improvement.
                    }
                }
            }

            iterations++;
            if (!improved)
                break;
        }

        // Evaluate the best profile found.
        var bestResults  = EvaluateProfile(current, goldenStates);
        var bestScore    = bestResults.Sum(r => r.Score);

        // Build diff from base to best.
        var diff = BuildProfileDiff(baseProfile, current, parameters);

        // Build search bounds summary.
        var searchBounds = parameters.ToDictionary(
            p => p.Name,
            p => (p.Min, p.Max, p.Step));

        var bestProfileName = $"optimized-{current.ComputeHash()}";
        var bestProfileDescription = $"Optimizer output from base '{baseProfile.ProfileName}'; " +
                          $"{diff.Count} parameter(s) changed; " +
                          $"score {baseScore:F2} → {bestScore:F2}";
        var bestProfile = new DecisionTuningProfile
        {
            ProfileName  = bestProfileName,
            Description  = bestProfileDescription,
            Need         = current.Need,
            Mood         = current.Mood,
            Personality  = current.Personality,
            Categories   = current.Categories,
        };

        return new OptimizerRunResult(
            BaseProfileName:      baseProfile.ProfileName,
            BaseProfileHash:      baseProfile.ComputeHash(),
            BestProfileName:      bestProfile.ProfileName,
            BestProfileHash:      bestProfile.ComputeHash(),
            BestProfileJson:      bestProfile.ToJson(),
            BaseScore:            Math.Round(baseScore, 4),
            BestScore:            Math.Round(bestScore, 4),
            ScoreImprovement:     Math.Round(bestScore - baseScore, 4),
            BaseDesiredPassCount: baseResults.Count(r => r.DesiredTopCategoryMet),
            BestDesiredPassCount: bestResults.Count(r => r.DesiredTopCategoryMet),
            BaseSatisfiedCount:   baseResults.Count(r => r.StateSatisfied),
            BestSatisfiedCount:   bestResults.Count(r => r.StateSatisfied),
            BaseResults:          baseResults,
            BestResults:          bestResults,
            ProfileDiff:          diff,
            IterationsPerformed:  iterations,
            MaxIterations:        maxIterations,
            SearchBounds:         searchBounds,
            CompletedAt:          DateTime.UtcNow.ToString("o"));
    }

    /// <summary>
    /// Evaluates a single <see cref="DecisionTuningProfile"/> against all provided golden states
    /// and returns per-state results.
    ///
    /// <para>
    /// Each golden state is evaluated with its own deterministic RNG seeded from the
    /// state's <see cref="GoldenStateEntry.SampleKey"/>. This ensures evaluation of a given
    /// state is always identical regardless of dataset ordering.
    /// </para>
    /// </summary>
    public static IReadOnlyList<GoldenStateResult> EvaluateProfile(
        DecisionTuningProfile profile,
        IReadOnlyList<GoldenStateEntry> goldenStates)
    {
        var domain = new IslandDomainPack(profile);

        var results = new List<GoldenStateResult>(goldenStates.Count);
        foreach (var gs in goldenStates)
            results.Add(EvaluateEntry(gs, domain));

        return results;
    }

    /// <summary>
    /// Evaluates a single golden state entry against an already-configured domain pack.
    /// Uses a deterministic RNG seeded from the entry's <see cref="GoldenStateEntry.SampleKey"/>
    /// so evaluation is always order-independent.
    ///
    /// <para>
    /// The actor's ability scores (STR/DEX/CON/INT/WIS/CHA) are set to neutral defaults and
    /// are not used for personality scoring. Instead, <see cref="GoldenStateEntry.TraitProfile"/>
    /// is injected directly into the quality model, ensuring evaluation reflects the exact
    /// authored trait vector without any lossy inverse mapping.
    /// Physiological state (satiety, health, energy, morale) is taken from the golden entry as
    /// usual and still drives need pressures.
    /// </para>
    /// </summary>
    public static GoldenStateResult EvaluateEntry(
        GoldenStateEntry entry,
        IslandDomainPack domain)
    {
        if (!Enum.TryParse<FuzzerScenarioKind>(entry.Scenario, out var scenario))
            throw new ArgumentException($"Unknown scenario kind '{entry.Scenario}'.");

        var actorId = new ActorId("trait-actor");

        // Ability scores are irrelevant — the TraitProfile is injected directly into the scoring
        // model. Only the physiological state (satiety, energy, morale) matters here.
        var stateData = new Dictionary<string, object>
        {
            ["satiety"] = entry.State.Satiety,
            ["energy"]  = entry.State.Energy,
            ["morale"]  = entry.State.Morale,
        };
        var actorState = (IslandActorState)domain.CreateActorState(actorId, stateData);
        actorState.Health = entry.State.Health;

        var worldState = PressureFuzzerScenarios.Build(scenario, actorId);

        // Use a per-entry deterministic RNG seeded from the SampleKey so evaluation
        // is identical regardless of golden-state list ordering.
        var rng = new Random(StableSeed(entry.SampleKey));

        // Pass the TraitProfile directly — no stat synthesis, no trait re-derivation.
        var candidates = domain.GenerateCandidates(
            actorId, actorState, worldState, 0L, rng,
            NullResourceAvailability.Instance,
            entry.TraitProfile);

        var sorted = candidates.OrderByDescending(c => c.Score).ToList();

        // Compute quality contributions using the same explicit trait profile so
        // contribution weights are consistent with the candidate scoring above.
        var contributions = domain.ComputeQualityContributions(
            actorState, worldState, 0L, sorted,
            entry.TraitProfile);

        return ScoreGoldenState(entry, sorted, contributions);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static GoldenStateResult ScoreGoldenState(
        GoldenStateEntry entry,
        List<ActionCandidate> sorted,
        IReadOnlyList<IReadOnlyList<(QualityType Quality, double Contribution)>> contributions)
    {
        var outcome   = entry.DesiredOutcome;
        var desired   = outcome.DesiredTopCategory?.ToString();
        var forbidden = outcome.ForbiddenTopCategories?.Select(q => q.ToString()).ToHashSet()
                        ?? new HashSet<string>();
        var acceptable = outcome.AcceptableTopCategories?.Select(q => q.ToString()).ToHashSet()
                         ?? new HashSet<string>();

        // ── Winning action details ─────────────────────────────────────────
        var winningActionId = sorted.Count > 0 ? sorted[0].Action.Id.Value : (string?)null;
        var topCategory     = sorted.Count > 0 ? GetTopCategory(contributions[0]) : null;
        var topCategories   = sorted.Count > 0
            ? GetCategoryList(contributions[0])
            : Array.Empty<string>();

        var desiredMet         = topCategory != null && topCategory == desired;
        var acceptableMet      = !desiredMet && topCategory != null && acceptable.Contains(topCategory);
        var forbiddenTriggered = topCategory != null && forbidden.Contains(topCategory);

        // ── Best desired candidate rank and delta ──────────────────────────
        int?    bestDesiredRank      = null;
        double? desiredVsWinnerDelta = null;

        if (desired != null)
        {
            var winnerScore = sorted.Count > 0 ? sorted[0].Score : 0.0;
            for (int i = 0; i < sorted.Count; i++)
            {
                if (GetTopCategory(contributions[i]) == desired)
                {
                    bestDesiredRank      = i + 1;       // 1-based
                    desiredVsWinnerDelta = Math.Round(sorted[i].Score - winnerScore, 4);
                    break;
                }
            }
        }

        // ── Desired in top-N (for objective scoring) ───────────────────────
        var desiredInTopN = desired != null &&
            Enumerable.Range(0, Math.Min(5, sorted.Count))
                .Any(i => GetTopCategory(contributions[i]) == desired);

        double score = 0.0;
        if (desiredMet)
            score = ObjectiveWeights.DesiredTopCategoryReward * entry.Priority;
        else if (acceptableMet)
            score = ObjectiveWeights.AcceptableCategoryReward * entry.Priority;
        else if (desiredInTopN)
            score = ObjectiveWeights.DesiredInTopNReward * entry.Priority;

        if (forbiddenTriggered)
            score -= ObjectiveWeights.ForbiddenCategoryPenalty * entry.Priority;

        return new GoldenStateResult(
            SampleKey:                  entry.SampleKey,
            Label:                      entry.Label,
            Priority:                   entry.Priority,
            ActualTopActionId:          winningActionId,
            ActualTopCategory:          topCategory,
            ActualTopCategories:        topCategories,
            DesiredTopCategory:         desired,
            DesiredTopCategoryMet:      desiredMet,
            AcceptableCategoryMet:      acceptableMet,
            ForbiddenCategoryTriggered: forbiddenTriggered,
            BestDesiredCategoryRank:    bestDesiredRank,
            DesiredCategoryVsWinnerDelta: desiredVsWinnerDelta,
            Score:                      Math.Round(score, 4));
    }

    /// <summary>
    /// Returns the dominant category name for a candidate given its pre-computed contributions.
    /// The dominant category is the quality with the highest weighted contribution.
    /// </summary>
    private static string? GetTopCategory(
        IReadOnlyList<(QualityType Quality, double Contribution)> contributions)
        => contributions.Count > 0 ? contributions[0].Quality.ToString() : null;

    /// <summary>
    /// Returns all category names for a candidate given its pre-computed contributions,
    /// in contribution-descending order.
    /// </summary>
    private static IReadOnlyList<string> GetCategoryList(
        IReadOnlyList<(QualityType Quality, double Contribution)> contributions)
        => contributions.Select(x => x.Quality.ToString()).ToList();

    /// <summary>
    /// Computes a stable, process-independent 32-bit hash of <paramref name="s"/>
    /// using FNV-1a. Used to seed per-entry RNGs so evaluation is deterministic
    /// regardless of golden-state list ordering.
    /// </summary>
    private static int StableSeed(string input)
    {
        unchecked
        {
            uint hash = 2166136261u;
            foreach (char c in input)
            {
                hash ^= c;
                hash *= 16777619u;
            }
            return (int)hash;
        }
    }

    private static IReadOnlyList<ProfileDiffEntry> BuildProfileDiff(
        DecisionTuningProfile baseline,
        DecisionTuningProfile candidate,
        IReadOnlyList<TunableParameter> parameters)
    {
        var diff = new List<ProfileDiffEntry>();
        foreach (var p in parameters)
        {
            var baseVal = p.Getter(baseline);
            var candVal = p.Getter(candidate);
            if (Math.Abs(baseVal - candVal) > 1e-9)
                diff.Add(new ProfileDiffEntry(p.Name, baseVal, candVal, Math.Round(candVal - baseVal, 6)));
        }
        return diff;
    }

    // ── Profile mutation helpers ──────────────────────────────────────────────
    // DecisionTuningProfile is a sealed class with init-only properties, not a record,
    // so we construct new instances explicitly.

    private static DecisionTuningProfile WithNeed(
        DecisionTuningProfile profile,
        double newValue,
        Func<NeedTuning, double, NeedTuning> factory) =>
        new()
        {
            ProfileName  = profile.ProfileName,
            Description  = profile.Description,
            Need         = factory(profile.Need, newValue),
            Mood         = profile.Mood,
            Personality  = profile.Personality,
            Categories   = profile.Categories,
        };

    private static DecisionTuningProfile WithMood(
        DecisionTuningProfile profile,
        double newValue,
        Func<MoodTuning, double, MoodTuning> factory) =>
        new()
        {
            ProfileName  = profile.ProfileName,
            Description  = profile.Description,
            Need         = profile.Need,
            Mood         = factory(profile.Mood, newValue),
            Personality  = profile.Personality,
            Categories   = profile.Categories,
        };

    // ── Null IResourceAvailability stub ──────────────────────────────────────

    private sealed class NullResourceAvailability : IResourceAvailability
    {
        public static readonly NullResourceAvailability Instance = new();
        public bool IsReserved(ResourceId _) => false;
        public bool TryReserve(ResourceId _, string __, long ___) => true;
        public void Release(ResourceId _) { }
    }
}
