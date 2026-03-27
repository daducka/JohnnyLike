using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.SimRunner.PressureFuzzer;

namespace JohnnyLike.SimRunner.Optimizer;

// ─── Per-candidate breakdown types ───────────────────────────────────────────

/// <summary>
/// Per-quality decomposition of the effective weight for a single quality dimension.
/// Used in detailed failure artifacts to show how each quality's final scoring weight
/// is built from its three independent sources.
/// </summary>
public sealed record QualityModelComponentEntry(
    /// <summary>Need-driven urgency contribution (from satiety/energy/morale/health deficits).</summary>
    double NeedAdd,
    /// <summary>Stable personality tendency derived from the actor's trait profile.</summary>
    double PersonalityBase,
    /// <summary>Multiplicative mood modulation applied to the personality component.</summary>
    double MoodMultiplier,
    /// <summary>Final combined weight = NeedAdd + PersonalityBase * MoodMultiplier.</summary>
    double EffectiveWeight);

/// <summary>
/// Per-quality contribution for a single scored candidate action.
/// </summary>
public sealed record CandidateQualityEntry(
    /// <summary>Raw quality value provided by the action.</summary>
    double QualityValue,
    /// <summary>Effective weight from the quality model for this quality type.</summary>
    double EffectiveWeight,
    /// <summary>Weighted contribution = QualityValue * EffectiveWeight.</summary>
    double Contribution);

/// <summary>
/// Detailed breakdown of a single ranked action candidate.
/// Includes score, intrinsic contribution, dominant categories,
/// and per-quality contribution breakdown.
/// </summary>
public sealed record CandidateRankEntry(
    /// <summary>Action identifier.</summary>
    string ActionId,
    /// <summary>1-based rank in the sorted candidate list.</summary>
    int Rank,
    /// <summary>Final composite score.</summary>
    double Score,
    /// <summary>Intrinsic (non-quality) component of the score.</summary>
    double IntrinsicScore,
    /// <summary>
    /// Quality type names with positive contribution, ordered by contribution descending.
    /// The first entry is the dominant category.
    /// </summary>
    IReadOnlyList<string> DominantCategories,
    /// <summary>Per-quality-type contribution breakdown.</summary>
    IReadOnlyDictionary<string, CandidateQualityEntry> QualityContributions);

/// <summary>
/// Analysis of the <c>think_about_supplies</c> candidate when it appears in the candidate list.
/// Null when the action is not available in the current state.
/// </summary>
public sealed record ThinkAboutSuppliesAnalysis(
    /// <summary>Whether think_about_supplies appeared as a candidate.</summary>
    bool Present,
    /// <summary>Its composite score. Zero when not present.</summary>
    double Score,
    /// <summary>Its 1-based rank among all candidates. Zero when not present.</summary>
    int Rank,
    /// <summary>Per-quality contribution breakdown. Null when not present.</summary>
    IReadOnlyDictionary<string, CandidateQualityEntry>? Qualities);

/// <summary>
/// Rich evaluation data for a single golden state entry, produced alongside
/// the standard <see cref="GoldenStateResult"/> for detailed failure artifacts.
/// </summary>
public sealed record GoldenStateDetailedData(
    /// <summary>Top N ranked candidates with score and quality breakdown.</summary>
    IReadOnlyList<CandidateRankEntry> TopCandidates,
    /// <summary>
    /// Quality model decomposition for the evaluated state:
    /// needAdd / personalityBase / moodMultiplier / effectiveWeight per quality type.
    /// </summary>
    IReadOnlyDictionary<string, QualityModelComponentEntry> QualityModelDecomposition,
    /// <summary>Analysis of think_about_supplies if it was in the candidate set.</summary>
    ThinkAboutSuppliesAnalysis? ThinkAboutSupplies);

// ─── Per-golden-state result ──────────────────────────────────────────────────

/// <summary>
/// The outcome of evaluating a single golden state against a candidate profile.
/// </summary>
public sealed record GoldenStateResult(
    /// <summary>Stable identifier matching <see cref="GoldenStateEntry.SampleKey"/>.</summary>
    string SampleKey,
    /// <summary>Human-readable label from the golden state definition.</summary>
    string? Label,
    /// <summary>Relative importance weight of this state.</summary>
    double Priority,
    /// <summary>
    /// Action ID of the top-scoring candidate.
    /// Null if no candidates were generated.
    /// </summary>
    string? ActualTopActionId,
    /// <summary>
    /// Dominant quality type name of the top-scoring candidate (the quality with the
    /// highest weighted contribution). Null if no candidates were generated.
    /// </summary>
    string? ActualTopCategory,
    /// <summary>
    /// All quality type names with positive contribution for the winning action,
    /// ordered by contribution descending. Provides full context for why an action won
    /// instead of just the single dominant quality.
    /// </summary>
    IReadOnlyList<string> ActualTopCategories,
    /// <summary>Expected top category from the golden state desired outcome.</summary>
    string? DesiredTopCategory,
    /// <summary>Whether the actual top category exactly matches the desired top category.</summary>
    bool DesiredTopCategoryMet,
    /// <summary>Whether the actual top category is among the acceptable categories.</summary>
    bool AcceptableCategoryMet,
    /// <summary>Whether the actual top category is in the forbidden list.</summary>
    bool ForbiddenCategoryTriggered,
    /// <summary>
    /// 1-based rank of the best candidate whose dominant category matches
    /// <see cref="DesiredTopCategory"/> in the sorted candidate list.
    /// Null when <see cref="DesiredTopCategory"/> is not specified or no such candidate exists.
    /// A value of 1 means the desired category won. Higher values indicate "closer misses".
    /// </summary>
    int? BestDesiredCategoryRank,
    /// <summary>
    /// Score of the best desired candidate minus the winner's score.
    /// Zero when the desired category wins; negative when it lost by this amount.
    /// Null when <see cref="DesiredTopCategory"/> is not specified or no desired candidate exists.
    /// Use this to distinguish "close misses" (small negative delta) from "totally wrong" states.
    /// </summary>
    double? DesiredCategoryVsWinnerDelta,
    /// <summary>Contribution of this golden state to the total objective score.</summary>
    double Score)
{
    /// <summary>
    /// True when this state is considered satisfied: either the desired top category won
    /// (<see cref="DesiredTopCategoryMet"/>) or an acceptable alternative won
    /// (<see cref="AcceptableCategoryMet"/>).
    /// </summary>
    public bool StateSatisfied => DesiredTopCategoryMet || AcceptableCategoryMet;
}

// ─── Profile comparison ───────────────────────────────────────────────────────

/// <summary>
/// A single parameter value in a profile diff.
/// </summary>
public sealed record ProfileDiffEntry(
    string ParameterName,
    double BaselineValue,
    double CandidateValue,
    double Delta);

// ─── Optimizer run result ─────────────────────────────────────────────────────

/// <summary>
/// Complete result of a single optimizer run.
/// </summary>
public sealed record OptimizerRunResult(
    /// <summary>The base profile used as starting point.</summary>
    string BaseProfileName,
    string BaseProfileHash,
    /// <summary>The best profile found during the search.</summary>
    string BestProfileName,
    string BestProfileHash,
    string BestProfileJson,
    /// <summary>Total objective score of the base profile on golden states.</summary>
    double BaseScore,
    /// <summary>Total objective score of the best profile on golden states.</summary>
    double BestScore,
    /// <summary>Score improvement over the base profile (BestScore - BaseScore).</summary>
    double ScoreImprovement,
    /// <summary>
    /// Number of golden states where the desired top category was exactly matched
    /// on the base profile.
    /// </summary>
    int BaseDesiredPassCount,
    /// <summary>
    /// Number of golden states where the desired top category was exactly matched
    /// on the best profile.
    /// </summary>
    int BestDesiredPassCount,
    /// <summary>
    /// Number of golden states that were satisfied (desired OR acceptable category won)
    /// on the base profile.
    /// </summary>
    int BaseSatisfiedCount,
    /// <summary>
    /// Number of golden states that were satisfied (desired OR acceptable category won)
    /// on the best profile.
    /// </summary>
    int BestSatisfiedCount,
    /// <summary>Per-state results evaluated against the base profile.</summary>
    IReadOnlyList<GoldenStateResult> BaseResults,
    /// <summary>Per-state results evaluated against the best profile.</summary>
    IReadOnlyList<GoldenStateResult> BestResults,
    /// <summary>Parameters that changed from base to best profile.</summary>
    IReadOnlyList<ProfileDiffEntry> ProfileDiff,
    /// <summary>Number of coordinate-descent iterations performed.</summary>
    int IterationsPerformed,
    /// <summary>Max iterations allowed by run settings.</summary>
    int MaxIterations,
    /// <summary>The search bounds used (parameter name → [min, max, step]).</summary>
    IReadOnlyDictionary<string, (double Min, double Max, double Step)> SearchBounds,
    /// <summary>ISO-8601 timestamp of when the run completed.</summary>
    string CompletedAt);

