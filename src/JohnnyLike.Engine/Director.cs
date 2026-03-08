using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Engine;

/// <summary>
/// Plans the next action for each actor by generating domain candidates, applying variety
/// penalties, filtering by room, performing deterministic tie-breaking, and reserving resources.
/// </summary>
public class Director
{
    private readonly IDomainPack _domainPack;
    private readonly ReservationTable _reservations;
    private readonly VarietyMemory _varietyMemory;
    private readonly ITraceSink _traceSink;
    private readonly DecisionTraceOptions _traceOptions;
    private readonly Dictionary<ActorId, string> _actorReservationGroups = new();
    private int _reservationCounter = 0;

    public Director(IDomainPack domainPack, ReservationTable reservations, VarietyMemory varietyMemory, ITraceSink traceSink,
        DecisionTraceOptions? traceOptions = null)
    {
        _domainPack = domainPack;
        _reservations = reservations;
        _varietyMemory = varietyMemory;
        _traceSink = traceSink;
        _traceOptions = traceOptions ?? DecisionTraceOptions.Default;
    }

    public (ActionSpec? action, object? effectHandler, DecisionInfo? decisionInfo) PlanNextAction(
        ActorId actorId,
        ActorState actorState,
        WorldState worldState,
        Dictionary<ActorId, ActorState> allActors,
        long currentTick,
        Random rng)
    {
        var trace = _traceOptions;

        // ── 1. Generate all candidates ──────────────────────────────────────────
        var rawCandidates = _domainPack.GenerateCandidates(actorId, actorState, worldState, currentTick, rng, _reservations);
        var preFilterCount = rawCandidates.Count;

        // ── 2. Filter candidates by actor's current room ────────────────────────
        var actorRoom = actorState.CurrentRoomId;
        var filteredOut = trace.IsVerbose
            ? rawCandidates.Where(c =>
                c.ProviderItemId != null &&
                worldState.GetItemRoomId(c.ProviderItemId) != null &&
                worldState.GetItemRoomId(c.ProviderItemId) != actorRoom).ToList()
            : null;

        var roomFiltered = rawCandidates.Where(c =>
            c.ProviderItemId == null ||
            worldState.GetItemRoomId(c.ProviderItemId) == null ||
            worldState.GetItemRoomId(c.ProviderItemId) == actorRoom
        ).ToList();

        if (trace.IsCandidatesOrAbove)
        {
            var details = new Dictionary<string, object>
            {
                ["actorId"]       = actorId.Value,
                ["rawCount"]      = preFilterCount,
                ["filteredCount"] = roomFiltered.Count
            };
            if (trace.IsVerbose && filteredOut != null && filteredOut.Count > 0)
                details["filteredCandidates"] = string.Join(",",
                    filteredOut.Select(c => c.Action.Id.Value));
            _traceSink.Record(new TraceEvent(currentTick, actorId, "DecisionCandidatesGenerated", details));
        }

        // ── 3. Apply variety penalty ────────────────────────────────────────────
        var penalties = new Dictionary<string, double>(roomFiltered.Count);
        var adjustedCandidates = new List<ActionCandidate>(roomFiltered.Count);
        foreach (var candidate in roomFiltered)
        {
            var key = CandidateKey(candidate);
            var penalty = _varietyMemory.GetRepetitionPenalty(actorId.Value, candidate.Action.Id.Value, currentTick);
            penalties[key] = penalty;
            adjustedCandidates.Add(candidate with { Score = candidate.Score - penalty });
        }

        // ── 4. Deterministic tie-break sort ─────────────────────────────────────
        // Score desc, then ActionId asc, then ProviderItemId asc.
        // Null ProviderItemId sorts before any non-null value (empty string < any letter).
        var sortedCandidates = adjustedCandidates
            .OrderByDescending(c => c.Score)
            .ThenBy(c => c.Action.Id.Value)
            .ThenBy(c => c.ProviderItemId ?? "")
            .ToList();

        // Build deterministicRank lookup (1-based, keyed by actionId+providerItemId)
        var deterministicRank = new Dictionary<string, int>(sortedCandidates.Count);
        for (var i = 0; i < sortedCandidates.Count; i++)
            deterministicRank[CandidateKey(sortedCandidates[i])] = i + 1;

        // ── 5. Obtain verbose scoring explanation (Verbose only) ─────────────────
        Dictionary<string, object>? scoringExplanation = null;
        if (trace.IsVerbose)
            scoringExplanation = _domainPack.ExplainCandidateScoring(
                actorId, actorState, worldState, currentTick, roomFiltered);

        // ── 6. Domain attempt ordering ──────────────────────────────────────────
        CandidateOrderingDebugSink? orderingSink = trace.IsSummaryOrAbove ? new CandidateOrderingDebugSink() : null;
        var candidatesToTry = _domainPack.OrderCandidatesForSelection(
            actorId, actorState, worldState, currentTick, sortedCandidates, rng, orderingSink);

        // Build attemptRank lookup (1-based)
        var attemptRank = new Dictionary<string, int>(candidatesToTry.Count);
        for (var i = 0; i < candidatesToTry.Count; i++)
            attemptRank[CandidateKey(candidatesToTry[i])] = i + 1;

        // Emit DecisionCandidatesRanked (Candidates+) with full per-candidate data
        if (trace.IsCandidatesOrAbove && sortedCandidates.Count > 0)
        {
            var candidateList = sortedCandidates.Select(c =>
            {
                var key = CandidateKey(c);
                var rawPenalty = penalties.GetValueOrDefault(key, 0.0);
                var d = new Dictionary<string, object>
                {
                    ["actionId"]          = c.Action.Id.Value,
                    ["intrinsicScore"]    = c.IntrinsicScore,
                    ["preVarietyScore"]   = c.Score + rawPenalty,
                    ["varietyPenalty"]    = rawPenalty,
                    ["finalScore"]        = c.Score,
                    ["deterministicRank"] = deterministicRank.GetValueOrDefault(key, 0),
                    ["attemptRank"]       = attemptRank.GetValueOrDefault(key, 0)
                };
                if (c.ProviderItemId != null) d["providerItemId"] = c.ProviderItemId;
                if (c.Reason != null)         d["reason"]         = c.Reason;
                if (c.Qualities.Count > 0)
                    d["qualities"] = string.Join(",",
                        c.Qualities.Select(kvp => $"{kvp.Key}={kvp.Value:F3}"));
                return (object)d;
            }).ToList();

            _traceSink.Record(new TraceEvent(currentTick, actorId, "DecisionCandidatesRanked",
                new Dictionary<string, object>
                {
                    ["actorId"]        = actorId.Value,
                    ["candidateCount"] = sortedCandidates.Count,
                    ["candidates"]     = candidateList
                }));
        }

        // Emit DecisionOrderingApplied (Summary+) when domain provided ordering info
        if (trace.IsSummaryOrAbove && orderingSink?.OrderingBranch != null)
        {
            var od = new Dictionary<string, object>
            {
                ["actorId"]        = actorId.Value,
                ["orderingBranch"] = orderingSink.OrderingBranch
            };
            if (orderingSink.DecisionPragmatism.HasValue)  od["decisionPragmatism"] = orderingSink.DecisionPragmatism.Value;
            if (orderingSink.Spontaneity.HasValue)         od["spontaneity"]        = orderingSink.Spontaneity.Value;
            if (orderingSink.Temperature.HasValue)         od["temperature"]        = orderingSink.Temperature.Value;
            if (orderingSink.OriginalTopActionId != null)  od["originalTopActionId"] = orderingSink.OriginalTopActionId;
            if (orderingSink.ChosenActionId != null)       od["chosenActionId"]     = orderingSink.ChosenActionId;
            if (orderingSink.ChosenOriginalRank.HasValue)  od["chosenOriginalRank"] = orderingSink.ChosenOriginalRank.Value;
            if (trace.IsVerbose && orderingSink.SoftmaxWeightDetails != null)
                od["softmaxWeights"] = orderingSink.SoftmaxWeightDetails
                    .Select(e => (object)new Dictionary<string, object>
                    {
                        ["actionId"]    = e.ActionId,
                        ["weight"]      = e.Weight,
                        ["probability"] = e.Probability
                    }).ToList();
            _traceSink.Record(new TraceEvent(currentTick, actorId, "DecisionOrderingApplied", od));
        }

        // ── 7. Attempt candidates in domain-ordered sequence ────────────────────
        var lastRejectionReason = (string?)null;
        var attemptNumber = 0;

        foreach (var candidate in candidatesToTry)
        {
            attemptNumber++;
            var candidateKey = CandidateKey(candidate);

            if (TryReserveActionResources(actorId, candidate.Action, currentTick, out var groupId))
            {
                if (candidate.PreAction != null)
                {
                    var rngStream = new RandomRngStream(rng);
                    if (!_domainPack.TryExecutePreAction(actorId, actorState, worldState, rngStream, _reservations, candidate.PreAction))
                    {
                        if (groupId != null)
                        {
                            _reservations.ReleaseByPrefix(groupId + ":");
                            _actorReservationGroups.Remove(actorId);
                        }
                        if (trace.IsCandidatesOrAbove)
                            EmitCandidateRejected(currentTick, actorId, candidate, attemptNumber, "preaction_failed");
                        lastRejectionReason = "preaction_failed";
                        continue;
                    }
                }

                if (groupId != null)
                    _actorReservationGroups[actorId] = groupId;

                // ── Success ──────────────────────────────────────────────────────
                var rawPenalty = penalties.GetValueOrDefault(candidateKey, 0.0);
                var selectionReason = DetermineSelectionReason(
                    attemptNumber, lastRejectionReason, orderingSink?.OrderingBranch);

                var topAlternatives = sortedCandidates
                    .Where(c => CandidateKey(c) != candidateKey)
                    .Take(3)
                    .Select(c => new TopAlternative(c.Action.Id.Value, c.Score, c.ProviderItemId))
                    .ToArray();

                var decisionInfo = new DecisionInfo(
                    SelectionReason: selectionReason,
                    AttemptRank: attemptNumber,
                    OriginalRank: deterministicRank.TryGetValue(candidateKey, out var origRank) ? origRank : null,
                    FinalScore: candidate.Score,
                    IntrinsicScore: candidate.IntrinsicScore,
                    VarietyPenalty: rawPenalty,
                    ProviderItemId: candidate.ProviderItemId,
                    OrderingBranch: orderingSink?.OrderingBranch,
                    TopAlternatives: topAlternatives
                );

                if (trace.IsSummaryOrAbove)
                    EmitDecisionSelected(currentTick, actorId, candidate, decisionInfo, scoringExplanation);

                return (candidate.Action, candidate.EffectHandler, decisionInfo);
            }
            else
            {
                if (trace.IsCandidatesOrAbove)
                    EmitCandidateRejected(currentTick, actorId, candidate, attemptNumber, "reservation_failed");
                lastRejectionReason = "reservation_failed";
            }
        }

        // ── No action available ──────────────────────────────────────────────────
        if (trace.IsSummaryOrAbove)
        {
            var noActionReason = preFilterCount == 0       ? "no_candidates_generated"
                               : roomFiltered.Count == 0   ? "no_candidates_after_filter"
                               :                             "all_candidates_rejected";

            _traceSink.Record(new TraceEvent(currentTick, actorId, "DecisionNoActionAvailable",
                new Dictionary<string, object>
                {
                    ["actorId"]        = actorId.Value,
                    ["reason"]         = noActionReason,
                    ["candidateCount"] = sortedCandidates.Count,
                    ["rejectedCount"]  = attemptNumber
                }));
        }

        return (null, null, null);
    }

    private bool TryReserveActionResources(ActorId actorId, ActionSpec action, long currentTick, out string? groupId)
    {
        groupId = null;

        if (action.ResourceRequirements == null || action.ResourceRequirements.Count == 0)
            return true;

        var newGroupId = $"rsv:{actorId.Value}:{_reservationCounter++}";
        groupId = newGroupId;

        var reservedResources = new List<ResourceId>();
        foreach (var req in action.ResourceRequirements)
        {
            var until = currentTick + (req.DurationTicksOverride ?? action.EstimatedDurationTicks);
            var utilityId = $"{newGroupId}:res:{req.ResourceId.Value}";
            if (_reservations.TryReserve(req.ResourceId, utilityId, until))
            {
                reservedResources.Add(req.ResourceId);
            }
            else
            {
                foreach (var rid in reservedResources)
                    _reservations.Release(rid);
                return false;
            }
        }

        return true;
    }

    public void ReleaseActionReservations(ActorId actorId)
    {
        if (_actorReservationGroups.TryGetValue(actorId, out var groupId))
        {
            _reservations.ReleaseByPrefix(groupId + ":");
            _actorReservationGroups.Remove(actorId);
        }
    }

    // ── Private helpers ──────────────────────────────────────────────────────────

    /// <summary>Stable key for candidate identity (actionId + providerItemId).</summary>
    private static string CandidateKey(ActionCandidate c)
        => c.ProviderItemId == null
            ? c.Action.Id.Value
            : $"{c.Action.Id.Value}|{c.ProviderItemId}";

    private static string DetermineSelectionReason(int attemptRank, string? lastRejectionReason, string? orderingBranch)
    {
        if (attemptRank == 1)
            return orderingBranch == "explore" ? "softmax_sample" : "best_score";

        return lastRejectionReason == "preaction_failed"
            ? "fallback_after_preaction_failure"
            : "fallback_after_reservation_failure";
    }

    private void EmitCandidateRejected(long tick, ActorId actorId, ActionCandidate candidate, int attemptRank, string reason)
    {
        var d = new Dictionary<string, object>
        {
            ["rejectionReason"] = reason,
            ["failedActionId"]  = candidate.Action.Id.Value,
            ["attemptRank"]     = attemptRank
        };
        if (candidate.ProviderItemId != null)
            d["failedProviderItemId"] = candidate.ProviderItemId;
        _traceSink.Record(new TraceEvent(tick, actorId, "DecisionCandidateRejected", d));
    }

    private void EmitDecisionSelected(
        long tick,
        ActorId actorId,
        ActionCandidate candidate,
        DecisionInfo info,
        Dictionary<string, object>? scoringExplanation)
    {
        var d = new Dictionary<string, object>
        {
            ["actorId"]         = actorId.Value,
            ["actionId"]        = candidate.Action.Id.Value,
            ["finalScore"]      = info.FinalScore,
            ["intrinsicScore"]  = info.IntrinsicScore,
            ["varietyPenalty"]  = info.VarietyPenalty,
            ["selectionReason"] = info.SelectionReason,
            ["attemptRank"]     = info.AttemptRank
        };
        if (info.ProviderItemId != null)  d["providerItemId"]  = info.ProviderItemId;
        if (info.OriginalRank.HasValue)   d["originalRank"]    = info.OriginalRank.Value;
        if (info.OrderingBranch != null)  d["orderingBranch"]  = info.OrderingBranch;
        if (info.TopAlternatives != null && info.TopAlternatives.Count > 0)
            d["topAlternatives"] = string.Join(",",
                info.TopAlternatives.Select(a => $"{a.ActionId}={a.FinalScore:F3}"));
        if (scoringExplanation != null)
            d["scoringExplanation"] = scoringExplanation;
        _traceSink.Record(new TraceEvent(tick, actorId, "DecisionSelected", d));
    }
}
