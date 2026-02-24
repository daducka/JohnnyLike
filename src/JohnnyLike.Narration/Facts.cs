namespace JohnnyLike.Narration;

/// <summary>Immutable snapshot of a single actor's known facts.</summary>
public sealed record ActorFacts(
    string ActorId,
    double? Satiety,
    double? Energy,
    double? Morale,
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

    public void UpdateActor(ActorFacts facts) => _actors[facts.ActorId] = facts;

    public ActorFacts? GetActor(string actorId) =>
        _actors.TryGetValue(actorId, out var f) ? f : null;
}

/// <summary>A single narrative beat extracted from one trace event.</summary>
public sealed record Beat(
    double SimTime,
    string ActorId,
    string EventType,
    string ActionKind,
    string ActionId,
    bool? Success = null,
    double? SatietyAfter = null,
    double? EnergyAfter = null,
    double? MoraleAfter = null
);
