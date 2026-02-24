using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Narration;

/// <summary>
/// Consumes <see cref="TraceEvent"/>s, updates <see cref="CanonicalFacts"/>,
/// maintains a recent-beats buffer, and yields <see cref="NarrationJob"/>s for
/// ActionAssigned and ActionCompleted events.
/// </summary>
public sealed class TraceBeatExtractor
{
    private readonly CanonicalFacts _facts;
    private readonly NarrationPromptBuilder _promptBuilder;
    private readonly List<Beat> _recentBeats = new();
    private readonly int _maxRecentBeats;
    private readonly int _summaryRefreshEveryN;
    private int _beatsSinceLastSummary;

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
    /// Process a single trace event. Returns a <see cref="NarrationJob"/> when the event
    /// warrants narration, or <c>null</c> otherwise.
    /// </summary>
    public NarrationJob? Consume(TraceEvent evt)
    {
        switch (evt.EventType)
        {
            case "ActionAssigned":
                return HandleActionAssigned(evt);

            case "ActionCompleted":
                return HandleActionCompleted(evt);

            default:
                return null;
        }
    }

    private NarrationJob? HandleActionAssigned(TraceEvent evt)
    {
        if (!evt.ActorId.HasValue) return null;

        var actorId = evt.ActorId.Value.Value;
        var actionKind = evt.Details.TryGetValue("actionKind", out var ak) ? ak?.ToString() ?? "" : "";
        var actionId = evt.Details.TryGetValue("actionId", out var aid) ? aid?.ToString() ?? "" : "";

        var beat = new Beat(evt.Time, actorId, evt.EventType, actionKind, actionId);
        AddBeat(beat);

        _beatsSinceLastSummary++;
        bool wantSummary = _beatsSinceLastSummary >= _summaryRefreshEveryN;
        if (wantSummary) _beatsSinceLastSummary = 0;

        var prompt = _promptBuilder.BuildAttemptPrompt(actorId, beat, _facts, _recentBeats, wantSummary);

        return new NarrationJob(
            JobId: Guid.NewGuid(),
            PlayAtSimTime: evt.Time,
            DeadlineSimTime: evt.Time + 10.0,
            Kind: NarrationJobKind.Attempt,
            ActorId: actorId,
            Prompt: prompt
        );
    }

    private NarrationJob? HandleActionCompleted(TraceEvent evt)
    {
        if (!evt.ActorId.HasValue) return null;

        var actorId = evt.ActorId.Value.Value;
        var actionKind = evt.Details.TryGetValue("actionKind", out var ak) ? ak?.ToString() ?? "" : "";
        var actionId = evt.Details.TryGetValue("actionId", out var aid) ? aid?.ToString() ?? "" : "";
        var outcomeStr = evt.Details.TryGetValue("outcomeType", out var ot) ? ot?.ToString() ?? "" : "";
        var success = outcomeStr == "Success";

        double? satiety = TryGetDouble(evt.Details, "actor_satiety");
        double? energy = TryGetDouble(evt.Details, "actor_energy");
        double? morale = TryGetDouble(evt.Details, "actor_morale");

        // Update canonical facts
        var existing = _facts.GetActor(actorId);
        _facts.UpdateActor(new ActorFacts(actorId, satiety ?? existing?.Satiety, energy ?? existing?.Energy, morale ?? existing?.Morale, actionKind, actionId));

        var beat = new Beat(evt.Time, actorId, evt.EventType, actionKind, actionId, success, satiety, energy, morale);
        AddBeat(beat);

        var prompt = _promptBuilder.BuildOutcomePrompt(actorId, beat, _facts, _recentBeats, false);

        return new NarrationJob(
            JobId: Guid.NewGuid(),
            PlayAtSimTime: evt.Time,
            DeadlineSimTime: evt.Time + 15.0,
            Kind: NarrationJobKind.Outcome,
            ActorId: actorId,
            Prompt: prompt
        );
    }

    private void AddBeat(Beat beat)
    {
        _recentBeats.Add(beat);
        if (_recentBeats.Count > _maxRecentBeats)
            _recentBeats.RemoveAt(0);
    }

    private static double? TryGetDouble(Dictionary<string, object> dict, string key)
    {
        if (!dict.TryGetValue(key, out var raw)) return null;
        if (raw is double d) return d;
        if (double.TryParse(raw?.ToString(), out var parsed)) return parsed;
        return null;
    }
}
