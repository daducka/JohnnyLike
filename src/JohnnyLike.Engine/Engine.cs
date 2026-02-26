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
    private readonly EventTracer _eventTracer;
    private readonly Random _rng;
    private readonly Queue<Signal> _signalQueue;
    private readonly Dictionary<ActionId, object?> _effectHandlers;
    private long _currentTick;

    public const int TickHz = 20;

    public long CurrentTick => _currentTick;
    public double CurrentSeconds => (double)_currentTick / TickHz;
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
        _eventTracer = new EventTracer();
        _director = new Director(domainPack, _reservations, _varietyMemory, _traceSink);
        _rng = new Random(seed);
        _signalQueue = new Queue<Signal>();
        _effectHandlers = new Dictionary<ActionId, object?>();
        _currentTick = 0L;
    }

    public void AddActor(ActorId actorId, Dictionary<string, object>? initialData = null)
    {
        var actorState = _domainPack.CreateActorState(actorId, initialData);
        actorState.Status = ActorStatus.Ready;
        _actors[actorId] = actorState;

        _traceSink.Record(new TraceEvent(
            _currentTick,
            actorId,
            "ActorAdded",
            new Dictionary<string, object> { ["actorId"] = actorId.Value }
        ));
    }

    public void AdvanceTicks(long ticks)
    {
        _currentTick += ticks;

        _worldState.Tracer = _eventTracer;
        var worldTraceEvents = _domainPack.TickWorldState(_worldState, _currentTick, _reservations);
        _worldState.Tracer = NullEventTracer.Instance;
        foreach (var evt in worldTraceEvents)
            _traceSink.Record(evt);
        EmitBeats(_eventTracer.Drain(), defaultActorId: null);

        while (_signalQueue.Count > 0 && _signalQueue.Peek().Tick <= _currentTick)
        {
            var signal = _signalQueue.Dequeue();
            ProcessSignal(signal);
        }

        _director.UpdateScenes(_currentTick);
        _reservations.CleanupExpired(_currentTick);
        _varietyMemory.Cleanup(_currentTick);
    }

    public void EnqueueSignal(Signal signal)
    {
        _signalQueue.Enqueue(signal);

        _traceSink.Record(new TraceEvent(
            _currentTick,
            signal.TargetActor,
            "SignalEnqueued",
            new Dictionary<string, object>
            {
                ["signalType"] = signal.Type,
                ["scheduledForTick"] = signal.Tick
            }
        ));
    }

    public bool TryGetNextAction(ActorId actorId, out ActionSpec? action)
    {
        action = null;

        if (!_actors.TryGetValue(actorId, out var actorState))
            return false;

        if (actorState.Status != ActorStatus.Ready)
            return false;

        var (plannedAction, effectHandler) = _director.PlanNextAction(actorId, actorState, _worldState, _actors, _currentTick, _rng);

        if (plannedAction != null)
        {
            actorState.Status = ActorStatus.Busy;
            actorState.CurrentAction = plannedAction;
            actorState.LastDecisionTick = _currentTick;

            if (effectHandler != null)
                _effectHandlers[plannedAction.Id] = effectHandler;

            var details = new Dictionary<string, object>
            {
                ["actionId"] = plannedAction.Id.Value,
                ["actionKind"] = plannedAction.Kind.ToString(),
                ["estimatedDurationTicks"] = plannedAction.EstimatedDurationTicks
            };

            if (plannedAction.ResourceRequirements != null && plannedAction.ResourceRequirements.Count > 0)
            {
                details["resourceRequirements"] = string.Join(", ",
                    plannedAction.ResourceRequirements.Select(r => r.ResourceId.Value));
            }

            _traceSink.Record(new TraceEvent(
                _currentTick,
                actorId,
                "ActionAssigned",
                details
            ));

            action = plannedAction;
            return true;
        }

        return false;
    }

    public void ReportActionComplete(ActorId actorId, ActionOutcome outcome)
    {
        if (!_actors.TryGetValue(actorId, out var actorState))
            return;

        if (outcome.ResultData == null)
            outcome = outcome with { ResultData = new Dictionary<string, object>() };

        _director.ReleaseActionReservations(actorId);

        _effectHandlers.TryGetValue(outcome.ActionId, out var effectHandler);
        _effectHandlers.Remove(outcome.ActionId);

        var rngStream = new RandomRngStream(_rng);
        _worldState.Tracer = _eventTracer;
        _domainPack.ApplyActionEffects(actorId, outcome, actorState, _worldState, rngStream, _reservations, effectHandler);
        _worldState.Tracer = NullEventTracer.Instance;
        EmitBeats(_eventTracer.Drain(), defaultActorId: actorId.Value);

        var details = new Dictionary<string, object>
        {
            ["actionId"] = outcome.ActionId.Value,
            ["outcomeType"] = outcome.Type.ToString(),
            ["actualDurationTicks"] = outcome.ActualDurationTicks
        };

        if (outcome.ResultData != null)
        {
            foreach (var kvp in outcome.ResultData)
                details[kvp.Key] = kvp.Value;
        }

        var actorSnapshot = _domainPack.GetActorStateSnapshot(actorState);
        foreach (var kvp in actorSnapshot)
            details[$"actor_{kvp.Key}"] = kvp.Value;

        _traceSink.Record(new TraceEvent(
            _currentTick,
            actorId,
            "ActionCompleted",
            details
        ));

        _varietyMemory.RecordAction(actorId.Value, outcome.ActionId.Value, _currentTick);

        if (actorState.CurrentAction?.Kind == ActionKind.JoinScene)
        {
            if (outcome.Type == ActionOutcomeType.Success &&
                actorState.CurrentAction.Parameters is JoinSceneActionParameters joinParams)
            {
                var sceneId = new SceneId(joinParams.SceneId);
                _director.HandleSceneJoin(actorId, sceneId, _currentTick);
                actorState.CurrentScene = sceneId;
            }

            if (actorState.CurrentScene.HasValue)
            {
                var scenes = _director.GetScenes();
                if (scenes.TryGetValue(actorState.CurrentScene.Value, out var scene))
                {
                    var allComplete = scene.JoinedActors.All(aid =>
                    {
                        if (_actors.TryGetValue(aid, out var as2))
                            return as2.CurrentAction == null || as2.Status == ActorStatus.Ready;
                        return false;
                    });

                    if (allComplete && scene.Status == SceneStatus.Running)
                    {
                        _director.CompleteScene(scene.Id, _currentTick);
                        actorState.CurrentScene = null;
                    }
                }
            }
        }

        actorState.Status = ActorStatus.Ready;
        actorState.CurrentAction = null;
    }

    public string Serialize()
    {
        var data = new
        {
            CurrentTick = _currentTick,
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
            _currentTick,
            signal.TargetActor,
            "SignalProcessed",
            new Dictionary<string, object>
            {
                ["signalType"] = signal.Type,
                ["data"] = JsonSerializer.Serialize(signal.Data)
            }
        ));

        ActorState? targetActorState = null;
        if (signal.TargetActor.HasValue && _actors.TryGetValue(signal.TargetActor.Value, out var actorState))
            targetActorState = actorState;

        _domainPack.OnSignal(signal, targetActorState, _worldState, _currentTick);
    }

    private void EmitBeats(List<NarrationBeat> beats, string? defaultActorId)
    {
        foreach (var beat in beats)
        {
            var actorStr = beat.ActorId ?? defaultActorId;
            ActorId? actorId = actorStr != null ? new ActorId(actorStr) : null;

            var details = new Dictionary<string, object>
            {
                ["text"] = beat.Text,
                ["phase"] = beat.Phase.ToString(),
                ["priority"] = beat.Priority
            };
            if (beat.SubjectId != null)
                details["subjectId"] = beat.SubjectId;

            _traceSink.Record(new TraceEvent(_currentTick, actorId, "NarrationBeat", details));
        }
    }
}
