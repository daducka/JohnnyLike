namespace JohnnyLike.Narration;

/// <summary>Immutable snapshot of a single actor's known facts.</summary>
/// <param name="ActorId">Identifier of the actor.</param>
/// <param name="Stats">
/// Generic key-value stat snapshot provided by the domain's
/// <c>GetActorStateSnapshot</c> (e.g. "satiety"→"42", "energy"→"77").
/// The narration system never hard-codes specific stat names.
/// </param>
/// <param name="LastActionKind">Kind of the most recent action, if any.</param>
/// <param name="LastActionId">Id of the most recent action, if any.</param>
public sealed record ActorFacts(
    string ActorId,
    IReadOnlyDictionary<string, string> Stats,
    string? LastActionKind,
    string? LastActionId
);

/// <summary>All deterministic facts extracted from the trace so far.</summary>
public sealed class CanonicalFacts
{
    private readonly Dictionary<string, ActorFacts> _actors = new();

    public IReadOnlyDictionary<string, ActorFacts> Actors => _actors;
    public string Domain { get; set; } = string.Empty;
    public double CurrentSimTime { get; set; }
    /// <summary>
    /// The current time-of-day phase (e.g. "Morning", "Afternoon", "Night").
    /// Set when a <c>DayPhaseChanged</c> trace event is consumed.
    /// Null until the first phase change is observed.
    /// </summary>
    public string? CurrentDayPhase { get; set; }

    public void UpdateActor(ActorFacts facts) => _actors[facts.ActorId] = facts;

    public ActorFacts? GetActor(string actorId) =>
        _actors.TryGetValue(actorId, out var f) ? f : null;
}

/// <summary>
/// A single narrative beat extracted from one trace event.
/// <para>
/// For actor action events <see cref="ActorId"/> is set and
/// <see cref="SubjectKind"/> is <c>"Actor"</c>.
/// For world / environment events <see cref="ActorId"/> is <c>null</c> and
/// <see cref="SubjectKind"/> is <c>"World"</c> or a domain-defined label.
/// </para>
/// </summary>
/// <param name="Subject">
/// For actor-action beats: the action identifier.
/// For world-event beats: the world-object or entity identifier set by the domain handler.
/// </param>
/// <param name="OutcomeType">Raw outcome string from the trace (e.g. Success, CriticalSuccess, Failed).</param>
public sealed record Beat(
    double SimTime,
    string? ActorId,
    string SubjectKind,
    string EventType,
    string ActionKind,
    string Subject,
    bool? Success = null,
    string? OutcomeType = null,
    /// <summary>
    /// Generic stat snapshot after the action (mirrors the domain's
    /// <c>GetActorStateSnapshot</c> output).  <c>null</c> for world-event beats.
    /// </summary>
    IReadOnlyDictionary<string, string>? StatsAfter = null
);
