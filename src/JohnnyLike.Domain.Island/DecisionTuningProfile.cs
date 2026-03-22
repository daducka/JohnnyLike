using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

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

    /// <summary>Optional human-readable name for this profile, used in logs and optimizer comparisons.</summary>
    public string ProfileName { get; init; } = "ProductionDefault";

    /// <summary>Optional free-text description of what this profile represents or differs from the default.</summary>
    public string? Description { get; init; }

    /// <summary>Need-urgency scaling parameters (pressure → quality weight).</summary>
    public NeedTuning Need { get; init; } = new();

    /// <summary>Mood-multiplier suppression parameters (critical-state → personality dampening).</summary>
    public MoodTuning Mood { get; init; } = new();

    /// <summary>Personality-trait response shaping and DecisionPragmatism derivation constants.</summary>
    public PersonalityTuning Personality { get; init; } = new();

    /// <summary>Category-level scoring parameters for specific action categories.</summary>
    public CategoryTuning Categories { get; init; } = new();

    /// <summary>
    /// Returns a compact, human-readable representation of all tuning values.
    /// Useful for logging, fuzzer output, and optimizer comparisons.
    /// </summary>
    public string ToDebugString()
    {
        var sb = new StringBuilder();
        sb.Append($"[{ProfileName}]");
        if (Description != null)
            sb.Append($" ({Description})");
        sb.AppendLine();

        AppendSection(sb, "Need",
            KV("FatiguePressureRestScale",  Need.FatiguePressureRestScale),
            KV("MiseryPressureComfortScale", Need.MiseryPressureComfortScale),
            KV("InjurySafetyNeedScale",  Need.InjurySafetyNeedScale),
            KV("InjuryRestNeedScale",    Need.InjuryRestNeedScale),
            KV("InjuryComfortNeedScale", Need.InjuryComfortNeedScale),
            KV("SatietyRampMild",     Need.SatietyRampMild),
            KV("SatietyRampModerate", Need.SatietyRampModerate),
            KV("SatietyRampStrong",   Need.SatietyRampStrong),
            KV("HungerMildMax",       Need.HungerMildMax),
            KV("HungerModerateRange", Need.HungerModerateRange),
            KV("HungerStrongRange",   Need.HungerStrongRange),
            KV("FoodAvailabilityNormCap",            Need.FoodAvailabilityNormCap),
            KV("ImmediateFoodSignificanceThreshold", Need.ImmediateFoodSignificanceThreshold),
            KV("AcquirableFoodSignificanceThreshold", Need.AcquirableFoodSignificanceThreshold),
            KV("FoodConsumptionShareHigh", Need.FoodConsumptionShareHigh),
            KV("FoodConsumptionShareLow",  Need.FoodConsumptionShareLow),
            KV("FoodShareNeutral",         Need.FoodShareNeutral),
            KV("PrepTimePressureCap",         Need.PrepTimePressureCap),
            KV("PrepTimePressureRatePerDay",  Need.PrepTimePressureRatePerDay));

        AppendSection(sb, "Mood",
            KV("StarvatingSatietyThreshold", Mood.StarvatingSatietyThreshold),
            KV("PrepStarvationFloor",        Mood.PrepStarvationFloor),
            KV("ExhaustedEnergyThreshold",   Mood.ExhaustedEnergyThreshold),
            KV("MasteryExhaustionFloor",     Mood.MasteryExhaustionFloor),
            KV("FunBaseScale",             Mood.FunBaseScale),
            KV("FunCriticalSurvivalScale", Mood.FunCriticalSurvivalScale),
            KV("FunCriticalSatietyThreshold", Mood.FunCriticalSatietyThreshold),
            KV("FunCriticalEnergyThreshold",  Mood.FunCriticalEnergyThreshold),
            KV("InjuryFunSuppressionFloor",         Mood.InjuryFunSuppressionFloor),
            KV("InjuryMasterySuppressionFloor",     Mood.InjuryMasterySuppressionFloor),
            KV("InjuryPreparationSuppressionFloor", Mood.InjuryPreparationSuppressionFloor));

        AppendSection(sb, "Personality",
            KV("PreparationScale",    Personality.PreparationScale),
            KV("EfficiencyScale",     Personality.EfficiencyScale),
            KV("MasteryScale",        Personality.MasteryScale),
            KV("ComfortScale",        Personality.ComfortScale),
            KV("SafetyScale",         Personality.SafetyScale),
            KV("FoodConsumptionScale", Personality.FoodConsumptionScale),
            KV("FoodAcquisitionScale", Personality.FoodAcquisitionScale),
            KV("PragmatismBase",            Personality.PragmatismBase),
            KV("PragmatismPlannerScale",    Personality.PragmatismPlannerScale),
            KV("PragmatismSurvivorScale",   Personality.PragmatismSurvivorScale),
            KV("PragmatismHedonistScale",   Personality.PragmatismHedonistScale),
            KV("PragmatismInstinctiveScale", Personality.PragmatismInstinctiveScale),
            KV("PragmatismMin", Personality.PragmatismMin),
            KV("PragmatismMax", Personality.PragmatismMax));

        AppendSection(sb, "Categories.ThinkAboutSupplies",
            KV("TopN",                (double)Categories.ThinkAboutSupplies.TopN),
            KV("StarvationThreshold", Categories.ThinkAboutSupplies.StarvationThreshold),
            KV("StarvationSuppression", Categories.ThinkAboutSupplies.StarvationSuppression),
            KV("FallbackPreparation", Categories.ThinkAboutSupplies.FallbackPreparation),
            KV("FallbackEfficiency",  Categories.ThinkAboutSupplies.FallbackEfficiency));

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Returns a deterministic 8-character hex hash of all tuning values.
    /// Identical parameter sets always produce the same hash; any change produces a different one.
    /// Suitable for tagging fuzzer outputs and comparing runs.
    /// </summary>
    public string ComputeHash()
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(ToJson()));
        return Convert.ToHexString(bytes)[..8];
    }

    // ── JSON serialization ────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Serializes this profile to a JSON string.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, _jsonOpts);

    /// <summary>Deserializes a <see cref="DecisionTuningProfile"/> from a JSON string.</summary>
    public static DecisionTuningProfile FromJson(string json) =>
        JsonSerializer.Deserialize<DecisionTuningProfile>(json, _jsonOpts)
            ?? throw new InvalidOperationException("Deserialized profile was null.");

    /// <summary>Loads a <see cref="DecisionTuningProfile"/> from a JSON file on disk.</summary>
    public static DecisionTuningProfile LoadFromFile(string path) =>
        FromJson(File.ReadAllText(path));

    private static void AppendSection(StringBuilder sb, string header, params string[] entries)
    {
        sb.AppendLine($"  {header}:");
        foreach (var entry in entries)
            sb.AppendLine($"    {entry}");
    }

    private static string KV(string key, double value) => $"{key}={value}";
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

    /// <summary>Scales fatigue pressure (100 − Energy) into Rest need urgency. Max +1.5 at Energy=0.
    /// Expected range: [0.005, 0.05]</summary>
    public double FatiguePressureRestScale { get; init; } = 0.015;

    /// <summary>Scales misery pressure (100 − Morale) into Comfort need urgency. Max +1.0 at Morale=0.
    /// Expected range: [0.002, 0.03]</summary>
    public double MiseryPressureComfortScale { get; init; } = 0.01;

    /// <summary>Safety need urgency per point of injuryPressure. Max +2.5 at 0 HP.
    /// Expected range: [0.005, 0.10]</summary>
    public double InjurySafetyNeedScale { get; init; } = 0.025;

    /// <summary>Rest need urgency per point of injuryPressure (stacks with fatigue). Max +1.0 at 0 HP.
    /// Expected range: [0.002, 0.04]</summary>
    public double InjuryRestNeedScale { get; init; } = 0.010;

    /// <summary>Comfort need urgency per point of injuryPressure (stacks with misery). Max +0.5 at 0 HP.
    /// Expected range: [0.001, 0.02]</summary>
    public double InjuryComfortNeedScale { get; init; } = 0.005;

    // ── Staged hunger ramp thresholds ─────────────────────────────────────────
    // Hunger urgency only builds meaningfully below certain satiety thresholds
    // so actors don't seek food when already satisfied.

    /// <summary>Satiety at or above which hunger urgency is zero; also the top of the mild urgency band.
    /// Expected range: [50.0, 90.0]. Must be &gt; SatietyRampModerate.</summary>
    public double SatietyRampMild { get; init; } = 70.0;

    /// <summary>Satiety below which moderate urgency begins.
    /// Expected range: [30.0, 70.0]. Must be between SatietyRampStrong and SatietyRampMild.</summary>
    public double SatietyRampModerate { get; init; } = 50.0;

    /// <summary>Satiety below which strong urgency begins.
    /// Expected range: [10.0, 50.0]. Must be &lt; SatietyRampModerate.</summary>
    public double SatietyRampStrong { get; init; } = 30.0;

    /// <summary>Maximum hunger urgency in the mild band (Satiety 50–70).
    /// Expected range: [0.1, 1.0]</summary>
    public double HungerMildMax { get; init; } = 0.3;

    /// <summary>Additional hunger urgency added across the moderate band (Satiety 30–50).
    /// Expected range: [0.3, 3.0]</summary>
    public double HungerModerateRange { get; init; } = 1.2;

    /// <summary>Additional hunger urgency added across the strong band (Satiety 0–30).
    /// Expected range: [0.1, 2.0]</summary>
    public double HungerStrongRange { get; init; } = 0.5;

    // ── Food availability split ────────────────────────────────────────────────

    /// <summary>Number of food units that counts as "plenty" for normalization purposes.
    /// Expected range: [1.0, 20.0]</summary>
    public double FoodAvailabilityNormCap { get; init; } = 5.0;

    /// <summary>Normalised immediate-food ratio above which the immediate-food path activates.
    /// Expected range: [0.05, 0.5]</summary>
    public double ImmediateFoodSignificanceThreshold { get; init; } = 0.2;

    /// <summary>Normalised acquirable-food ratio above which the acquirable-food path activates.
    /// Expected range: [0.05, 0.5]</summary>
    public double AcquirableFoodSignificanceThreshold { get; init; } = 0.2;

    /// <summary>FoodConsumption share of hunger when immediate food is plentiful.
    /// Expected range: [0.5, 1.0]</summary>
    public double FoodConsumptionShareHigh { get; init; } = 0.80;

    /// <summary>FoodConsumption share of hunger when no immediate food but acquirable food exists.
    /// Expected range: [0.0, 0.5]</summary>
    public double FoodConsumptionShareLow { get; init; } = 0.20;

    /// <summary>Neutral 50/50 share used when neither immediate nor acquirable food is available.
    /// Expected range: [0.3, 0.7]</summary>
    public double FoodShareNeutral { get; init; } = 0.50;

    // ── Preparation time-pressure ─────────────────────────────────────────────

    /// <summary>Maximum bounded preparation urgency added by time-on-island pressure.
    /// Expected range: [0.05, 0.5]</summary>
    public double PrepTimePressureCap { get; init; } = 0.20;

    /// <summary>Preparation urgency gained per in-sim day stranded.
    /// Expected range: [0.01, 0.2]</summary>
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

    /// <summary>Satiety threshold below which the actor is considered starving, suppressing Preparation.
    /// Expected range: [5.0, 40.0]</summary>
    public double StarvatingSatietyThreshold { get; init; } = 20.0;

    /// <summary>Preparation multiplier floor when the actor is starving.
    /// Expected range: [0.0, 1.0]</summary>
    public double PrepStarvationFloor { get; init; } = 0.3;

    // ── Exhaustion suppression ─────────────────────────────────────────────────

    /// <summary>Energy threshold below which the actor is considered exhausted, suppressing Mastery.
    /// Expected range: [5.0, 40.0]</summary>
    public double ExhaustedEnergyThreshold { get; init; } = 20.0;

    /// <summary>Mastery multiplier floor when the actor is exhausted.
    /// Expected range: [0.0, 1.0]</summary>
    public double MasteryExhaustionFloor { get; init; } = 0.4;

    // ── Fun modulation ─────────────────────────────────────────────────────────

    /// <summary>Base Fun multiplier scale — keeps fun weight below 0.6 even at maximum misery.
    /// Expected range: [0.1, 1.0]</summary>
    public double FunBaseScale { get; init; } = 0.6;

    /// <summary>Critical-survival Fun suppression: reduces Fun weight to 35% when starving or exhausted.
    /// Expected range: [0.0, 1.0]</summary>
    public double FunCriticalSurvivalScale { get; init; } = 0.35;

    /// <summary>Satiety threshold below which critical-survival Fun suppression activates.
    /// Expected range: [5.0, 50.0]</summary>
    public double FunCriticalSatietyThreshold { get; init; } = 25.0;

    /// <summary>Energy threshold below which critical-survival Fun suppression activates.
    /// Expected range: [5.0, 40.0]</summary>
    public double FunCriticalEnergyThreshold { get; init; } = 20.0;

    // ── Injury suppression floors ──────────────────────────────────────────────

    /// <summary>Minimum multiplier for Fun personality at 0 HP (suppressed to 15%).
    /// Expected range: [0.0, 0.5]</summary>
    public double InjuryFunSuppressionFloor { get; init; } = 0.15;

    /// <summary>Minimum multiplier for Mastery personality at 0 HP (suppressed to 30%).
    /// Expected range: [0.0, 0.7]</summary>
    public double InjuryMasterySuppressionFloor { get; init; } = 0.30;

    /// <summary>Minimum multiplier for Preparation personality at 0 HP (suppressed to 40%).
    /// Expected range: [0.0, 0.8]</summary>
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

    /// <summary>Planner + Industrious traits → Preparation personality weight.
    /// Expected range: [0.1, 2.0]</summary>
    public double PreparationScale { get; init; } = 0.7;

    /// <summary>Planner + Craftsman traits → Efficiency personality weight.
    /// Expected range: [0.1, 2.0]</summary>
    public double EfficiencyScale { get; init; } = 0.6;

    /// <summary>Craftsman + Industrious traits → Mastery personality weight.
    /// Expected range: [0.1, 2.0]</summary>
    public double MasteryScale { get; init; } = 0.6;

    /// <summary>Hedonist trait → Comfort personality weight.
    /// Expected range: [0.05, 1.5]</summary>
    public double ComfortScale { get; init; } = 0.4;

    /// <summary>Survivor trait → Safety personality weight.
    /// Expected range: [0.05, 1.5]</summary>
    public double SafetyScale { get; init; } = 0.3;

    /// <summary>Instinctive + Hedonist traits → FoodConsumption personality weight.
    /// Expected range: [0.05, 1.0]</summary>
    public double FoodConsumptionScale { get; init; } = 0.2;

    /// <summary>Planner + Survivor traits → FoodAcquisition personality weight.
    /// Expected range: [0.05, 1.0]</summary>
    public double FoodAcquisitionScale { get; init; } = 0.15;

    // ── DecisionPragmatism derivation ──────────────────────────────────────────
    // Planners and survivors tend toward exploitation (higher pragmatism);
    // hedonists and instinctive actors tend toward exploration (lower pragmatism).

    /// <summary>Base pragmatism before personality adjustments.
    /// Expected range: [0.5, 1.0]</summary>
    public double PragmatismBase { get; init; } = 0.80;

    /// <summary>Planner trait contribution toward higher pragmatism (exploit).
    /// Expected range: [0.0, 0.3]</summary>
    public double PragmatismPlannerScale { get; init; } = 0.10;

    /// <summary>Survivor trait contribution toward higher pragmatism (exploit).
    /// Expected range: [0.0, 0.2]</summary>
    public double PragmatismSurvivorScale { get; init; } = 0.05;

    /// <summary>Hedonist trait contribution toward lower pragmatism (explore).
    /// Expected range: [0.0, 0.2]</summary>
    public double PragmatismHedonistScale { get; init; } = 0.06;

    /// <summary>Instinctive trait contribution toward lower pragmatism (explore).
    /// Expected range: [0.0, 0.2]</summary>
    public double PragmatismInstinctiveScale { get; init; } = 0.04;

    /// <summary>Minimum derived DecisionPragmatism — keeps actors coherent even at max spontaneity.
    /// Expected range: [0.3, 0.85]</summary>
    public double PragmatismMin { get; init; } = 0.65;

    /// <summary>Maximum derived DecisionPragmatism — keeps explore branch reachable.
    /// Expected range: [0.85, 1.0]</summary>
    public double PragmatismMax { get; init; } = 0.98;
}

// ─────────────────────────────────────────────────────────────────────────────
// Category tuning
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Category-level scoring parameters for specific action categories.
/// </summary>
public sealed class CategoryTuning
{
    /// <summary>Tuning for the <c>think_about_supplies</c> action candidate.</summary>
    public ThinkAboutSuppliesTuning ThinkAboutSupplies { get; init; } = new();
}

/// <summary>
/// Decision-policy parameters for the <c>think_about_supplies</c> action, which drives
/// recipe discovery opportunity scoring.
/// </summary>
public sealed class ThinkAboutSuppliesTuning
{
    /// <summary>Maximum number of top-ranked discoverable recipes considered when blending opportunity qualities.
    /// Expected range: [1, 10]</summary>
    public int TopN { get; init; } = 3;

    /// <summary>Satiety threshold below which the actor is considered survival-distressed for
    /// the purpose of suppressing think_about_supplies when no food-relevant recipes are discoverable.
    /// Expected range: [5.0, 50.0]</summary>
    public double StarvationThreshold { get; init; } = 25.0;

    /// <summary>Multiplier applied to think_about_supplies qualities when starving and no food/safety-relevant
    /// discoverable recipes are available. Reduces action priority so direct food actions take over.
    /// Expected range: [0.0, 0.5]</summary>
    public double StarvationSuppression { get; init; } = 0.2;

    /// <summary>Small fallback Preparation quality used when no recipes are currently discoverable.
    /// Keeps the action in the pool without dominating over survival options.
    /// Expected range: [0.0, 0.5]</summary>
    public double FallbackPreparation { get; init; } = 0.15;

    /// <summary>Small fallback Efficiency quality used when no recipes are currently discoverable.
    /// Expected range: [0.0, 0.5]</summary>
    public double FallbackEfficiency { get; init; } = 0.10;
}
