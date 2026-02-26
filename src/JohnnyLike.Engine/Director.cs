using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Engine;

public class Director
{
    private readonly IDomainPack _domainPack;
    private readonly ReservationTable _reservations;
    private readonly VarietyMemory _varietyMemory;
    private readonly ITraceSink _traceSink;
    private readonly Dictionary<SceneId, SceneInstance> _scenes = new();
    private readonly Dictionary<ActorId, SceneId> _actorReservationScenes = new();
    private int _sceneCounter = 0;

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
        var sceneAction = CheckForSceneJoin(actorId, actorState, currentTick);
        if (sceneAction != null)
            return (sceneAction, null);

        // Get candidates filtered to actor's room
        var allCandidates = _domainPack.GenerateCandidates(actorId, actorState, worldState, currentTick, rng, _reservations);

        // Apply variety penalty
        var adjustedCandidates = new List<ActionCandidate>();
        foreach (var candidate in allCandidates)
        {
            var penalty = _varietyMemory.GetRepetitionPenalty(actorId.Value, candidate.Action.Id.Value, currentTick);
            adjustedCandidates.Add(candidate with { Score = candidate.Score - penalty });
        }

        TryProposeScenes(allActors, worldState, currentTick, rng);

        // Deterministic tie-break: Score desc, then ActionId.Value asc
        var sortedCandidates = adjustedCandidates
            .OrderByDescending(c => c.Score)
            .ThenBy(c => c.Action.Id.Value)
            .ToList();

        foreach (var candidate in sortedCandidates)
        {
            if (TryReserveActionResources(actorId, candidate.Action, currentTick, out var reservationSceneId))
            {
                if (candidate.PreAction != null)
                {
                    var rngStream = new RandomRngStream(rng);
                    if (!_domainPack.TryExecutePreAction(actorId, actorState, worldState, rngStream, _reservations, candidate.PreAction))
                    {
                        if (reservationSceneId.HasValue)
                        {
                            _reservations.ReleaseByPrefix($"scene:{reservationSceneId.Value.Value}:");
                            _actorReservationScenes.Remove(actorId);
                        }
                        continue;
                    }
                }

                if (reservationSceneId.HasValue)
                    _actorReservationScenes[actorId] = reservationSceneId.Value;

                return (candidate.Action, candidate.EffectHandler);
            }
        }

        return (null, null);
    }

    private ActionSpec? CheckForSceneJoin(ActorId actorId, ActorState actorState, long currentTick)
    {
        var assignedScene = _scenes.Values
            .Where(s => (s.Status == SceneStatus.Proposed || s.Status == SceneStatus.Staging) &&
                       s.RoleAssignments.Values.Contains(actorId))
            .FirstOrDefault();

        if (assignedScene != null)
        {
            var role = assignedScene.RoleAssignments.First(kvp => kvp.Value == actorId).Key;
            var roleSpec = assignedScene.Template.Roles.First(r => r.RoleName == role);

            return new ActionSpec(
                new ActionId($"join_{assignedScene.Id.Value}_{role}"),
                ActionKind.JoinScene,
                new JoinSceneActionParameters(
                    assignedScene.Id.Value,
                    role,
                    assignedScene.ProposedTick + assignedScene.Template.JoinWindowTicks
                ),
                roleSpec.ActionTemplate.EstimatedDurationTicks
            );
        }

        return null;
    }

    private void TryProposeScenes(
        Dictionary<ActorId, ActorState> allActors,
        WorldState worldState,
        long currentTick,
        Random rng)
    {
        var templates = _domainPack.GetSceneTemplates();
        var readyActors = allActors.Where(kvp => kvp.Value.Status == ActorStatus.Ready).ToList();

        foreach (var template in templates)
        {
            var roleAssignments = new Dictionary<string, ActorId>();
            var usedActors = new HashSet<ActorId>();

            foreach (var role in template.Roles)
            {
                var eligible = readyActors
                    .Where(kvp => !usedActors.Contains(kvp.Key) && role.EligibilityPredicate(kvp.Value))
                    .ToList();

                if (eligible.Count == 0)
                {
                    roleAssignments.Clear();
                    break;
                }

                var chosen = eligible[rng.Next(eligible.Count)];
                roleAssignments[role.RoleName] = chosen.Key;
                usedActors.Add(chosen.Key);
            }

            if (roleAssignments.Count == template.Roles.Count)
            {
                var resourceIds = template.RequiredResources.Keys.Select(k => new ResourceId(k)).ToList();
                var canReserve = resourceIds.All(rid => !_reservations.IsReserved(rid));

                if (canReserve)
                {
                    var sceneId = new SceneId($"scene_{_sceneCounter++}");
                    var scene = new SceneInstance
                    {
                        Id = sceneId,
                        Template = template,
                        Status = SceneStatus.Proposed,
                        ProposedTick = currentTick,
                        RoleAssignments = roleAssignments,
                        ReservedResources = resourceIds
                    };

                    var deadline = currentTick + template.JoinWindowTicks + template.MaxDurationTicks;
                    foreach (var rid in resourceIds)
                        _reservations.TryReserve(rid, $"scene:{sceneId.Value}", deadline);

                    _scenes[sceneId] = scene;

                    _traceSink.Record(new TraceEvent(
                        currentTick,
                        null,
                        "SceneProposed",
                        new Dictionary<string, object>
                        {
                            ["sceneId"] = sceneId.Value,
                            ["type"] = template.SceneType,
                            ["roles"] = string.Join(",", roleAssignments.Select(kvp => $"{kvp.Key}:{kvp.Value}"))
                        }
                    ));

                    return;
                }
            }
        }
    }

    public void HandleSceneJoin(ActorId actorId, SceneId sceneId, long currentTick)
    {
        if (_scenes.TryGetValue(sceneId, out var scene))
        {
            scene.JoinedActors.Add(actorId);

            _traceSink.Record(new TraceEvent(
                currentTick,
                actorId,
                "SceneJoined",
                new Dictionary<string, object>
                {
                    ["sceneId"] = sceneId.Value,
                    ["joinedCount"] = scene.JoinedActors.Count,
                    ["requiredCount"] = scene.RoleAssignments.Count
                }
            ));

            if (scene.JoinedActors.Count == scene.RoleAssignments.Count)
            {
                scene.Status = SceneStatus.Running;
                scene.StartTick = currentTick;

                _traceSink.Record(new TraceEvent(
                    currentTick,
                    null,
                    "SceneStarted",
                    new Dictionary<string, object> { ["sceneId"] = sceneId.Value }
                ));
            }
        }
    }

    public void CompleteScene(SceneId sceneId, long currentTick)
    {
        if (_scenes.TryGetValue(sceneId, out var scene))
        {
            scene.Status = SceneStatus.Complete;
            scene.EndTick = currentTick;
            _reservations.ReleaseByPrefix($"scene:{sceneId.Value}:");

            _traceSink.Record(new TraceEvent(
                currentTick,
                null,
                "SceneCompleted",
                new Dictionary<string, object> { ["sceneId"] = sceneId.Value }
            ));
        }
    }

    public void UpdateScenes(long currentTick)
    {
        var toAbort = _scenes.Values
            .Where(s => s.Status != SceneStatus.Complete && s.Status != SceneStatus.Aborted)
            .Where(s => currentTick > s.ProposedTick + s.Template.JoinWindowTicks + s.Template.MaxDurationTicks)
            .ToList();

        foreach (var scene in toAbort)
        {
            scene.Status = SceneStatus.Aborted;
            scene.EndTick = currentTick;
            _reservations.ReleaseByPrefix($"scene:{scene.Id.Value}:");

            _traceSink.Record(new TraceEvent(
                currentTick,
                null,
                "SceneAborted",
                new Dictionary<string, object>
                {
                    ["sceneId"] = scene.Id.Value,
                    ["reason"] = "deadline"
                }
            ));
        }
    }

    public IReadOnlyDictionary<SceneId, SceneInstance> GetScenes() => _scenes;

    private bool TryReserveActionResources(ActorId actorId, ActionSpec action, long currentTick, out SceneId? reservationSceneId)
    {
        reservationSceneId = null;

        if (action.ResourceRequirements == null || action.ResourceRequirements.Count == 0)
            return true;

        var sceneId = new SceneId($"action:{actorId.Value}:{action.Id.Value}:{currentTick}");
        reservationSceneId = sceneId;

        var reservedResources = new List<ResourceId>();
        foreach (var req in action.ResourceRequirements)
        {
            var until = currentTick + (req.DurationTicksOverride ?? action.EstimatedDurationTicks);
            var utilityId = $"scene:{sceneId.Value}:actor:{actorId.Value}:action:{action.Id.Value}";
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
        if (_actorReservationScenes.TryGetValue(actorId, out var sceneId))
        {
            _reservations.ReleaseByPrefix($"scene:{sceneId.Value}:");
            _actorReservationScenes.Remove(actorId);
        }
    }
}
