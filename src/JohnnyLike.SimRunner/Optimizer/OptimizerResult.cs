using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.SimRunner.PressureFuzzer;

namespace JohnnyLike.SimRunner.Optimizer;

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

