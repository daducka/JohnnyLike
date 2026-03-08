namespace JohnnyLike.Engine;

/// <summary>Compact representation of a top-N alternative for decision summary traces.</summary>
/// <param name="ActionId">The action ID string.</param>
/// <param name="FinalScore">Score after variety penalty.</param>
/// <param name="ProviderItemId">Providing item ID (may be null for actor-self candidates).</param>
public record TopAlternative(string ActionId, double FinalScore, string? ProviderItemId);

/// <summary>
/// Structured metadata about a completed planning decision, returned by
/// <see cref="Director.PlanNextAction"/> and consumed by <see cref="Engine"/> to
/// enrich the <c>ActionAssigned</c> trace event.
/// </summary>
/// <param name="SelectionReason">
/// Human/machine-readable reason code:
/// <c>best_score</c>, <c>softmax_sample</c>,
/// <c>fallback_after_reservation_failure</c>, <c>fallback_after_preaction_failure</c>.
/// </param>
/// <param name="AttemptRank">1-based position in the domain-ordered attempt sequence.</param>
/// <param name="OriginalRank">
/// 1-based rank in the deterministic pre-ordering sort, or <c>null</c> if unknown.
/// </param>
/// <param name="FinalScore">Candidate score after variety penalty.</param>
/// <param name="IntrinsicScore">Baseline domain intrinsic score (before quality math).</param>
/// <param name="VarietyPenalty">Penalty subtracted by the variety memory system.</param>
/// <param name="ProviderItemId">Item that generated the candidate, or <c>null</c>.</param>
/// <param name="OrderingBranch"><c>"exploit"</c>, <c>"explore"</c>, or <c>null</c> if domain did not report.</param>
/// <param name="TopAlternatives">Top-N alternatives sorted by score, excluding the chosen candidate.</param>
public record DecisionInfo(
    string SelectionReason,
    int AttemptRank,
    int? OriginalRank,
    double FinalScore,
    double IntrinsicScore,
    double VarietyPenalty,
    string? ProviderItemId,
    string? OrderingBranch,
    IReadOnlyList<TopAlternative>? TopAlternatives
);
