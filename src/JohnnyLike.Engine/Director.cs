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
    private readonly Dictionary<ActorId, string> _actorReservationGroups = new();
    private int _reservationCounter = 0;

    public Director(IDomainPack domainPack, ReservationTable reservations, VarietyMemory varietyMemory, ITraceSink traceSink)
    {
        _domainPack = domainPack;
        _reservations = reservations;
        _varietyMemory = varietyMemory;
        _traceSink = traceSink;
    }

    public (ActionSpec? action, object? effectHandler) PlanNextAction(
        ActorId actorId,
        ActorState actorState,
        WorldState worldState,
        Dictionary<ActorId, ActorState> allActors,
        long currentTick,
        Random rng)
    {
        var allCandidates = _domainPack.GenerateCandidates(actorId, actorState, worldState, currentTick, rng, _reservations);

        // Filter candidates by actor's current room
        var actorRoom = actorState.CurrentRoomId;
        allCandidates = allCandidates.Where(c =>
            c.ProviderItemId == null ||
            worldState.GetItemRoomId(c.ProviderItemId) == null ||
            worldState.GetItemRoomId(c.ProviderItemId) == actorRoom
        ).ToList();

        // Apply variety penalty
        var adjustedCandidates = new List<ActionCandidate>();
        foreach (var candidate in allCandidates)
        {
            var penalty = _varietyMemory.GetRepetitionPenalty(actorId.Value, candidate.Action.Id.Value, currentTick);
            adjustedCandidates.Add(candidate with { Score = candidate.Score - penalty });
        }

        // Deterministic tie-break: Score desc, then ActionId asc, then ProviderItemId asc.
        // Null ProviderItemId sorts before any non-null value (empty string < any letter).
        var sortedCandidates = adjustedCandidates
            .OrderByDescending(c => c.Score)
            .ThenBy(c => c.Action.Id.Value)
            .ThenBy(c => c.ProviderItemId ?? "")  // null providers sort first (stable fallback)
            .ToList();

        foreach (var candidate in sortedCandidates)
        {
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
                        continue;
                    }
                }

                if (groupId != null)
                    _actorReservationGroups[actorId] = groupId;

                return (candidate.Action, candidate.EffectHandler);
            }
        }

        return (null, null);
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
}
