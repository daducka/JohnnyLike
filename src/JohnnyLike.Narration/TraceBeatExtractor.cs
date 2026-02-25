using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Narration;

/// <summary>
/// Consumes <see cref="TraceEvent"/>s, updates <see cref="CanonicalFacts"/>,
/// maintains a recent-beats buffer, and yields <see cref="NarrationJob"/>s.
/// <para>
/// Built-in handlers fire on <c>ActionAssigned</c> and <c>ActionCompleted</c>.
/// Domain code can call <see cref="RegisterWorldEventHandler"/> to produce
/// narration beats from arbitrary world/environment trace events.
/// </para>
/// </summary>
public sealed class TraceBeatExtractor
{
    private readonly CanonicalFacts _facts;
    private readonly NarrationPromptBuilder _promptBuilder;
    private readonly List<Beat> _recentBeats = new();
    private readonly int _maxRecentBeats;
    private readonly int _summaryRefreshEveryN;
    private int _beatsSinceLastSummary;

    // Domain-registered handlers for world / environment events
    private readonly Dictionary<string, Func<TraceEvent, Beat?>> _worldEventHandlers = new();

    public CanonicalFacts Facts => _facts;
    public IReadOnlyList<Beat> RecentBeats => _recentBeats;

    public TraceBeatExtractor(
        CanonicalFacts facts,
        NarrationPromptBuilder promptBuilder,
        int maxRecentBeats = 10,
        int summaryRefreshEveryN = 5)
    {
        _facts = facts;
        _promptBuilder = promptBuilder;
        _maxRecentBeats = maxRecentBeats;
        _summaryRefreshEveryN = summaryRefreshEveryN;
    }

    /// <summary>
    /// Register a handler for a domain-specific world/environment event type.
    /// The handler receives the raw <see cref="TraceEvent"/> and returns a
    /// <see cref="Beat"/> (or <c>null</c> to suppress narration for this instance).
    /// </summary>
    public void RegisterWorldEventHandler(string eventType, Func<TraceEvent, Beat?> handler)
        => _worldEventHandlers[eventType] = handler;

    /// <summary>
    /// Process a single trace event. Returns a <see cref="NarrationJob"/> when the
    /// event warrants narration, or <c>null</c> otherwise.
    /// </summary>
    public NarrationJob? Consume(TraceEvent evt)
    {
        // Always keep CurrentSimTime up to date
        _facts.CurrentSimTime = evt.Time;

        switch (evt.EventType)
        {
            case "ActionAssigned":
                return HandleActionAssigned(evt);

            case "ActionCompleted":
                return HandleActionCompleted(evt);

            default:
                if (_worldEventHandlers.TryGetValue(evt.EventType, out var handler))
                {
                    var beat = handler(evt);
                    if (beat != null)
                        return CreateWorldEventJob(beat);
                }
                return null;
        }
    }

    private NarrationJob? HandleActionAssigned(TraceEvent evt)
    {
        if (!evt.ActorId.HasValue) return null;

        var actorId = evt.ActorId.Value.Value;
        var actionKind = GetString(evt.Details, "actionKind");
        var actionId = GetString(evt.Details, "actionId");

        var beat = new Beat(evt.Time, actorId, "Actor", evt.EventType, actionKind, actionId);
        AddBeat(beat);

        _beatsSinceLastSummary++;
        bool wantSummary = _beatsSinceLastSummary >= _summaryRefreshEveryN;
        if (wantSummary) _beatsSinceLastSummary = 0;

        var prompt = _promptBuilder.BuildAttemptPrompt(beat, _facts, _recentBeats, wantSummary);

        return new NarrationJob(
            JobId: Guid.NewGuid(),
            PlayAtSimTime: evt.Time,
            DeadlineSimTime: evt.Time + 10.0,
            Kind: NarrationJobKind.Attempt,
            SubjectId: actorId,
            Prompt: prompt
        );
    }

    private NarrationJob? HandleActionCompleted(TraceEvent evt)
    {
        if (!evt.ActorId.HasValue) return null;

        var actorId = evt.ActorId.Value.Value;
        var actionKind = GetString(evt.Details, "actionKind");
        var actionId = GetString(evt.Details, "actionId");
        var outcomeStr = GetString(evt.Details, "outcomeType");
        var isSuccess = IsSuccessOutcome(outcomeStr);

        // Collect all actor_* keys generically — the domain decides what to expose
        var statsAfter = CollectActorStats(evt.Details);

        // Update canonical facts: merge new stats over existing ones
        var existing = _facts.GetActor(actorId);
        var mergedStats = MergeStats(existing?.Stats, statsAfter);
        _facts.UpdateActor(new ActorFacts(actorId, mergedStats, actionKind, actionId));

        var beat = new Beat(evt.Time, actorId, "Actor", evt.EventType, actionKind, actionId,
            Success: isSuccess, OutcomeType: outcomeStr,
            StatsAfter: statsAfter.Count > 0 ? statsAfter : null);
        AddBeat(beat);

        // Apply summary cadence to outcomes too
        _beatsSinceLastSummary++;
        bool wantSummary = _beatsSinceLastSummary >= _summaryRefreshEveryN;
        if (wantSummary) _beatsSinceLastSummary = 0;

        var prompt = _promptBuilder.BuildOutcomePrompt(beat, _facts, _recentBeats, wantSummary);

        return new NarrationJob(
            JobId: Guid.NewGuid(),
            PlayAtSimTime: evt.Time,
            DeadlineSimTime: evt.Time + 15.0,
            Kind: NarrationJobKind.Outcome,
            SubjectId: actorId,
            Prompt: prompt
        );
    }

    private NarrationJob CreateWorldEventJob(Beat beat)
    {
        AddBeat(beat);

        _beatsSinceLastSummary++;
        bool wantSummary = _beatsSinceLastSummary >= _summaryRefreshEveryN;
        if (wantSummary) _beatsSinceLastSummary = 0;

        var prompt = _promptBuilder.BuildWorldEventPrompt(beat, _facts, _recentBeats, wantSummary);

        return new NarrationJob(
            JobId: Guid.NewGuid(),
            PlayAtSimTime: beat.SimTime,
            DeadlineSimTime: beat.SimTime + 12.0,
            Kind: NarrationJobKind.WorldEvent,
            // Use beat.Subject as the world-object subject; fall back to ActorId for actor-sourced world events
            SubjectId: beat.Subject.Length > 0 ? beat.Subject : beat.ActorId,
            Prompt: prompt
        );
    }

    /// <summary>
    /// Determines whether the raw outcome type string represents a successful outcome.
    /// Recognises: Success, CriticalSuccess, PartialSuccess (success family)
    ///             and anything else (Failed, Failure, TimedOut, Cancelled, …) as failure.
    /// </summary>
    public static bool IsSuccessOutcome(string outcomeType) =>
        outcomeType is "Success" or "CriticalSuccess" or "PartialSuccess";

    private void AddBeat(Beat beat)
    {
        _recentBeats.Add(beat);
        if (_recentBeats.Count > _maxRecentBeats)
            _recentBeats.RemoveAt(0);
    }

    /// <summary>
    /// Collects all details keys prefixed with "actor_" and returns them as a
    /// stat dictionary (key = name after the prefix, value = string representation).
    /// Stat names are stored case-sensitively so domains must use consistent casing.
    /// </summary>
    private static IReadOnlyDictionary<string, string> CollectActorStats(Dictionary<string, object> details)
    {
        const string prefix = "actor_";
        var stats = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kvp in details)
        {
            if (kvp.Key.StartsWith(prefix, StringComparison.Ordinal))
            {
                var statName = kvp.Key[prefix.Length..];
                stats[statName] = kvp.Value?.ToString() ?? string.Empty;
            }
        }
        return stats;
    }

    private static IReadOnlyDictionary<string, string> MergeStats(
        IReadOnlyDictionary<string, string>? existing,
        IReadOnlyDictionary<string, string> incoming)
    {
        if (existing == null || existing.Count == 0) return incoming;
        var merged = new Dictionary<string, string>(existing, StringComparer.Ordinal);
        foreach (var kvp in incoming)
            merged[kvp.Key] = kvp.Value;
        return merged;
    }

    private static string GetString(Dictionary<string, object> dict, string key)
        => dict.TryGetValue(key, out var v) ? v?.ToString() ?? string.Empty : string.Empty;
}
