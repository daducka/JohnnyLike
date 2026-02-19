using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Engine;

public class Director
{
    private readonly IDomainPack _domainPack;
    private readonly ReservationTable _reservations;
    private readonly VarietyMemory _varietyMemory;
    private readonly ITraceSink _traceSink;
    private readonly Dictionary<SceneId, SceneInstance> _scenes = new();
    private readonly Dictionary<ActorId, SceneId> _actorReservationScenes = new(); // Tracks synthetic scene IDs for action reservations
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
        double currentTime,
        Random rng)
    {
        // First check if actor should join a scene
        var sceneAction = CheckForSceneJoin(actorId, actorState, currentTime);
        if (sceneAction != null)
        {
            return (sceneAction, null);
        }

        // Get candidates from domain pack, passing resource availability
        var candidates = _domainPack.GenerateCandidates(actorId, actorState, worldState, currentTime, rng, _reservations);

        // Apply variety penalty
        var adjustedCandidates = new List<ActionCandidate>();
        foreach (var candidate in candidates)
        {
            var penalty = _varietyMemory.GetRepetitionPenalty(actorId.Value, candidate.Action.Id.Value, currentTime);
            adjustedCandidates.Add(candidate with { Score = candidate.Score - penalty });
        }
        candidates = adjustedCandidates;

        // Try to propose scenes
        TryProposeScenes(allActors, worldState, currentTime, rng);

        // Pick best candidate and try to reserve resources
        var sortedCandidates = candidates.OrderByDescending(c => c.Score).ToList();
        foreach (var candidate in sortedCandidates)
        {
            if (TryReserveActionResources(actorId, candidate.Action, currentTime, out var reservationSceneId))
            {
                // Execute PreAction at action start (before the action duration begins).
                // If it fails (e.g. required supply missing), release the reservation and try the next candidate.
                if (candidate.PreAction != null)
                {
                    var rngStream = new RandomRngStream(rng);
                    if (!_domainPack.TryExecutePreAction(actorId, actorState, worldState, rngStream, _reservations, candidate.PreAction))
                    {
                        // Release the reservation we just acquired and keep trying
                        if (reservationSceneId.HasValue)
                        {
                            _reservations.ReleaseByPrefix($"scene:{reservationSceneId.Value.Value}:");
                            _actorReservationScenes.Remove(actorId);
                        }
                        continue;
                    }
                }

                // Successfully reserved resources (and PreAction passed) for this action
                if (reservationSceneId.HasValue)
                {
                    _actorReservationScenes[actorId] = reservationSceneId.Value;
                }
                return (candidate.Action, candidate.EffectHandler);
            }
        }

        // No candidate could be reserved (or no candidates at all)
        return (null, null);
    }

    private ActionSpec? CheckForSceneJoin(ActorId actorId, ActorState actorState, double currentTime)
    {
        // Check if actor is assigned to a staging/proposed scene
        var assignedScene = _scenes.Values
            .Where(s => (s.Status == SceneStatus.Proposed || s.Status == SceneStatus.Staging) &&
                       s.RoleAssignments.Values.Contains(actorId))
            .FirstOrDefault();

        if (assignedScene != null)
        {
            // Find the role
            var role = assignedScene.RoleAssignments.First(kvp => kvp.Value == actorId).Key;
            var roleSpec = assignedScene.Template.Roles.First(r => r.RoleName == role);

            // Create JoinScene action
            return new ActionSpec(
                new ActionId($"join_{assignedScene.Id.Value}_{role}"),
                ActionKind.JoinScene,
                new JoinSceneActionParameters(
                    assignedScene.Id.Value,
                    role,
                    assignedScene.ProposedTime + assignedScene.Template.JoinWindowSeconds
                ),
                roleSpec.ActionTemplate.EstimatedDuration
            );
        }

        return null;
    }

    private void TryProposeScenes(
        Dictionary<ActorId, ActorState> allActors,
        WorldState worldState,
        double currentTime,
        Random rng)
    {
        var templates = _domainPack.GetSceneTemplates();
        var readyActors = allActors.Where(kvp => kvp.Value.Status == ActorStatus.Ready).ToList();

        foreach (var template in templates)
        {
            // Check if we can cast this scene
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
                // Try to reserve resources
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
                        ProposedTime = currentTime,
                        RoleAssignments = roleAssignments,
                        ReservedResources = resourceIds
                    };

                    // Reserve resources
                    var deadline = currentTime + template.JoinWindowSeconds + template.MaxDurationSeconds;
                    foreach (var rid in resourceIds)
                    {
                        // Reserve for scene duration
                        _reservations.TryReserve(rid, $"scene:{sceneId.Value}", deadline);
                    }

                    _scenes[sceneId] = scene;

                    _traceSink.Record(new TraceEvent(
                        currentTime,
                        null,
                        "SceneProposed",
                        new Dictionary<string, object>
                        {
                            ["sceneId"] = sceneId.Value,
                            ["type"] = template.SceneType,
                            ["roles"] = string.Join(",", roleAssignments.Select(kvp => $"{kvp.Key}:{kvp.Value}"))
                        }
                    ));

                    return; // Only propose one scene at a time
                }
            }
        }
    }

    public void HandleSceneJoin(ActorId actorId, SceneId sceneId, double currentTime)
    {
        if (_scenes.TryGetValue(sceneId, out var scene))
        {
            scene.JoinedActors.Add(actorId);

            _traceSink.Record(new TraceEvent(
                currentTime,
                actorId,
                "SceneJoined",
                new Dictionary<string, object>
                {
                    ["sceneId"] = sceneId.Value,
                    ["joinedCount"] = scene.JoinedActors.Count,
                    ["requiredCount"] = scene.RoleAssignments.Count
                }
            ));

            // Check if all actors joined
            if (scene.JoinedActors.Count == scene.RoleAssignments.Count)
            {
                scene.Status = SceneStatus.Running;
                scene.StartTime = currentTime;

                _traceSink.Record(new TraceEvent(
                    currentTime,
                    null,
                    "SceneStarted",
                    new Dictionary<string, object>
                    {
                        ["sceneId"] = sceneId.Value
                    }
                ));
            }
        }
    }

    public void CompleteScene(SceneId sceneId, double currentTime)
    {
        if (_scenes.TryGetValue(sceneId, out var scene))
        {
            scene.Status = SceneStatus.Complete;
            scene.EndTime = currentTime;

            _reservations.ReleaseByPrefix($"scene:{sceneId.Value}:");

            _traceSink.Record(new TraceEvent(
                currentTime,
                null,
                "SceneCompleted",
                new Dictionary<string, object>
                {
                    ["sceneId"] = sceneId.Value
                }
            ));
        }
    }

    public void UpdateScenes(double currentTime)
    {
        // Check for expired scenes
        var toAbort = _scenes.Values
            .Where(s => s.Status != SceneStatus.Complete && s.Status != SceneStatus.Aborted)
            .Where(s => currentTime > s.ProposedTime + s.Template.JoinWindowSeconds + s.Template.MaxDurationSeconds)
            .ToList();

        foreach (var scene in toAbort)
        {
            scene.Status = SceneStatus.Aborted;
            scene.EndTime = currentTime;
            _reservations.ReleaseByPrefix($"scene:{scene.Id.Value}:");

            _traceSink.Record(new TraceEvent(
                currentTime,
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

    /// <summary>
    /// Attempts to reserve resources for an action.
    /// </summary>
    /// <returns>True if reservation succeeded (or no resources needed), false otherwise.</returns>
    private bool TryReserveActionResources(ActorId actorId, ActionSpec action, double currentTime, out SceneId? reservationSceneId)
    {
        reservationSceneId = null;

        // If no resources required, always succeed
        if (action.ResourceRequirements == null || action.ResourceRequirements.Count == 0)
        {
            return true;
        }

        // Create a synthetic SceneId to group these reservations
        var sceneId = new SceneId($"action:{actorId.Value}:{action.Id.Value}:{currentTime}");
        reservationSceneId = sceneId;

        // Try to reserve all resources
        var reservedResources = new List<ResourceId>();
        foreach (var req in action.ResourceRequirements)
        {
            var until = currentTime + (req.DurationOverride ?? action.EstimatedDuration);
            var utilityId = $"scene:{sceneId.Value}:actor:{actorId.Value}:action:{action.Id.Value}";
            if (_reservations.TryReserve(req.ResourceId, utilityId, until))
            {
                reservedResources.Add(req.ResourceId);
            }
            else
            {
                // Reservation failed, rollback
                foreach (var rid in reservedResources)
                {
                    _reservations.Release(rid);
                }
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Releases resources reserved for an actor's action.
    /// Should be called when an action completes or is cancelled.
    /// </summary>
    public void ReleaseActionReservations(ActorId actorId)
    {
        if (_actorReservationScenes.TryGetValue(actorId, out var sceneId))
        {
            _reservations.ReleaseByPrefix($"scene:{sceneId.Value}:");
            _actorReservationScenes.Remove(actorId);
        }
    }
}
