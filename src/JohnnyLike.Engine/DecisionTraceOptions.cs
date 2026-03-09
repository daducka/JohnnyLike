namespace JohnnyLike.Engine;

/// <summary>Controls the verbosity of per-decision trace events emitted by the engine Director.</summary>
public enum DecisionTraceLevel
{
    /// <summary>No decision trace events are emitted (default, zero overhead).</summary>
    None = 0,

    /// <summary>
    /// Emits summary-level events: DecisionSelected / DecisionNoActionAvailable and
    /// DecisionOrderingApplied (when the domain provides ordering info).
    /// ActionAssigned is enriched with selectionReason, score, rank, and top alternatives.
    /// </summary>
    Summary = 1,

    /// <summary>
    /// All Summary events plus: DecisionCandidatesGenerated, DecisionRoomFilterApplied,
    /// DecisionCandidatesRanked (with per-candidate data), and DecisionCandidateRejected
    /// events for each reservation or pre-action failure.
    /// </summary>
    Candidates = 2,

    /// <summary>
    /// All Candidates events plus: filtered-out candidate details, full softmax weight
    /// breakdown (when available from the domain), and domain scoring explanation payload.
    /// </summary>
    Verbose = 3
}

/// <summary>
/// Immutable options object that controls decision tracing behaviour in the engine.
/// Pass this to <see cref="Engine"/> to enable structured trace output for debugging
/// why an actor chose a particular action.
/// </summary>
/// <param name="Level">The desired verbosity level. Defaults to <see cref="DecisionTraceLevel.None"/>.</param>
public record DecisionTraceOptions(DecisionTraceLevel Level = DecisionTraceLevel.None)
{
    /// <summary>Convenience singleton representing the default (no tracing) configuration.</summary>
    public static readonly DecisionTraceOptions Default = new(DecisionTraceLevel.None);

    public bool IsSummaryOrAbove    => Level >= DecisionTraceLevel.Summary;
    public bool IsCandidatesOrAbove => Level >= DecisionTraceLevel.Candidates;
    public bool IsVerbose           => Level >= DecisionTraceLevel.Verbose;
}
