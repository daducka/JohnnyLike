using JohnnyLike.Domain.Abstractions;
using System.Text.Json;

namespace JohnnyLike.Engine;

public class Engine
{
    private readonly IDomainPack _domainPack;
    private readonly WorldState _worldState;
    private readonly Dictionary<ActorId, ActorState> _actors;
    private readonly Director _director;
    private readonly ReservationTable _reservations;
    private readonly VarietyMemory _varietyMemory;
    private readonly ITraceSink _traceSink;
    private readonly Random _rng;
    private readonly Queue<Signal> _signalQueue;
    private double _currentTime;

    public double CurrentTime => _currentTime;
    public IReadOnlyDictionary<ActorId, ActorState> Actors => _actors;
    public WorldState WorldState => _worldState;

    public Engine(IDomainPack domainPack, int seed, ITraceSink? traceSink = null)
    {
        _domainPack = domainPack;
        _worldState = domainPack.CreateInitialWorldState();
        _actors = new Dictionary<ActorId, ActorState>();
        _reservations = new ReservationTable();
        _varietyMemory = new VarietyMemory();
        _traceSink = traceSink ?? new InMemoryTraceSink();
        _director = new Director(domainPack, _reservations, _varietyMemory, _traceSink);
        _rng = new Random(seed);
        _signalQueue = new Queue<Signal>();
        _currentTime = 0.0;
    }

    public void AddActor(ActorId actorId, Dictionary<string, object>? initialData = null)
    {
        var actorState = _domainPack.CreateActorState(actorId, initialData);
        actorState.Status = ActorStatus.Ready;
        _actors[actorId] = actorState;

        _traceSink.Record(new TraceEvent(
            _currentTime,
            actorId,
            "ActorAdded",
            new Dictionary<string, object> { ["actorId"] = actorId.Value }
        ));
    }

    public void AdvanceTime(double dtSeconds)
    {
        _currentTime += dtSeconds;

        // Tick world state and record trace events
        var worldTraceEvents = _domainPack.TickWorldState(_worldState, dtSeconds, _reservations);
        foreach (var evt in worldTraceEvents)
        {
            _traceSink.Record(evt);
        }

        // Process pending signals
        while (_signalQueue.Count > 0 && _signalQueue.Peek().Timestamp <= _currentTime)
        {
            var signal = _signalQueue.Dequeue();
            ProcessSignal(signal);
        }

        // Update scenes
        _director.UpdateScenes(_currentTime);

        // Cleanup expired reservations
        _reservations.CleanupExpired(_currentTime);
        _varietyMemory.Cleanup(_currentTime);
    }

    public void EnqueueSignal(Signal signal)
    {
        _signalQueue.Enqueue(signal);

        _traceSink.Record(new TraceEvent(
            _currentTime,
            signal.TargetActor,
            "SignalEnqueued",
            new Dictionary<string, object>
            {
                ["signalType"] = signal.Type,
                ["scheduledFor"] = signal.Timestamp
            }
        ));
    }

    public bool TryGetNextAction(ActorId actorId, out ActionSpec? action)
    {
        action = null;

        if (!_actors.TryGetValue(actorId, out var actorState))
        {
            return false;
        }

        if (actorState.Status != ActorStatus.Ready)
        {
            return false;
        }

        action = _director.PlanNextAction(actorId, actorState, _worldState, _actors, _currentTime, _rng);

        if (action != null)
        {
            actorState.Status = ActorStatus.Busy;
            actorState.CurrentAction = action;
            actorState.LastDecisionTime = _currentTime;

            var details = new Dictionary<string, object>
            {
                ["actionId"] = action.Id.Value,
                ["actionKind"] = action.Kind.ToString(),
                ["estimatedDuration"] = action.EstimatedDuration
            };
            
            // Include resource requirements if present
            if (action.ResourceRequirements != null && action.ResourceRequirements.Count > 0)
            {
                details["resourceRequirements"] = string.Join(", ", 
                    action.ResourceRequirements.Select(r => r.ResourceId.Value));
            }

            _traceSink.Record(new TraceEvent(
                _currentTime,
                actorId,
                "ActionAssigned",
                details
            ));

            return true;
        }

        return false;
    }

    public void ReportActionComplete(ActorId actorId, ActionOutcome outcome)
    {
        if (!_actors.TryGetValue(actorId, out var actorState))
        {
            return;
        }

        // Ensure ResultData dictionary exists so domain pack can populate it
        if (outcome.ResultData == null)
        {
            outcome = outcome with { ResultData = new Dictionary<string, object>() };
        }

        // Release action reservations BEFORE applying effects
        // This allows world items spawned in effects to reserve resources previously held by the actor
        _director.ReleaseActionReservations(actorId);

        // Apply effects (domain pack may populate ResultData)
        var rngStream = new RandomRngStream(_rng);
        _domainPack.ApplyActionEffects(actorId, outcome, actorState, _worldState, rngStream, _reservations);

        // Build details dictionary including all data from ResultData
        var details = new Dictionary<string, object>
        {
            ["actionId"] = outcome.ActionId.Value,
            ["outcomeType"] = outcome.Type.ToString(),
            ["actualDuration"] = outcome.ActualDuration
        };

        // Merge ResultData (populated by domain pack during ApplyActionEffects)
        // Note: ResultData is guaranteed non-null after initialization above
        if (outcome.ResultData != null)
        {
            foreach (var kvp in outcome.ResultData)
            {
                details[kvp.Key] = kvp.Value;
            }
        }

        // Add actor state snapshot from domain pack
        var actorSnapshot = _domainPack.GetActorStateSnapshot(actorState);
        foreach (var kvp in actorSnapshot)
        {
            details[$"actor_{kvp.Key}"] = kvp.Value;
        }

        _traceSink.Record(new TraceEvent(
            _currentTime,
            actorId,
            "ActionCompleted",
            details
        ));

        // Record for variety
        _varietyMemory.RecordAction(actorId.Value, outcome.ActionId.Value, _currentTime);

        // Handle scene join
        if (actorState.CurrentAction?.Kind == ActionKind.JoinScene)
        {
            if (outcome.Type == ActionOutcomeType.Success &&
                actorState.CurrentAction.Parameters is JoinSceneActionParameters joinParams)
            {
                var sceneId = new SceneId(joinParams.SceneId);
                _director.HandleSceneJoin(actorId, sceneId, _currentTime);
                actorState.CurrentScene = sceneId;
            }

            // Check if scene is complete
            if (actorState.CurrentScene.HasValue)
            {
                var scenes = _director.GetScenes();
                if (scenes.TryGetValue(actorState.CurrentScene.Value, out var scene))
                {
                    var allComplete = scene.JoinedActors.All(aid =>
                    {
                        if (_actors.TryGetValue(aid, out var as2))
                        {
                            return as2.CurrentAction == null || as2.Status == ActorStatus.Ready;
                        }
                        return false;
                    });

                    if (allComplete && scene.Status == SceneStatus.Running)
                    {
                        _director.CompleteScene(scene.Id, _currentTime);
                        actorState.CurrentScene = null;
                    }
                }
            }
        }

        // Mark actor as ready
        actorState.Status = ActorStatus.Ready;
        actorState.CurrentAction = null;
    }

    public string Serialize()
    {
        var data = new
        {
            CurrentTime = _currentTime,
            WorldState = _worldState.Serialize(),
            Actors = _actors.ToDictionary(
                kvp => kvp.Key.Value,
                kvp => kvp.Value.Serialize()
            )
        };

        return JsonSerializer.Serialize(data);
    }

    public List<TraceEvent> GetTrace()
    {
        return _traceSink.GetEvents();
    }

    private void ProcessSignal(Signal signal)
    {
        _traceSink.Record(new TraceEvent(
            _currentTime,
            signal.TargetActor,
            "SignalProcessed",
            new Dictionary<string, object>
            {
                ["signalType"] = signal.Type,
                ["data"] = JsonSerializer.Serialize(signal.Data)
            }
        ));

        // Get the target actor state if specified
        ActorState? targetActorState = null;
        if (signal.TargetActor.HasValue && _actors.TryGetValue(signal.TargetActor.Value, out var actorState))
        {
            targetActorState = actorState;
        }

        // Delegate signal handling to the domain pack
        _domainPack.OnSignal(signal, targetActorState, _worldState, _currentTime);
    }
}
