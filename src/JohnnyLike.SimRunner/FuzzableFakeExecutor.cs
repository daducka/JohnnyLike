using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.SimRunner;

public class FuzzableFakeExecutor
{
    private readonly Engine.Engine _engine;
    private readonly FuzzConfig _config;
    private readonly Random _rng;
    private readonly Dictionary<ActorId, (ActionSpec Action, long StartTick, long AdjustedDurationTicks)> _runningActions = new();
    private readonly Dictionary<ActorId, long> _noShowUntil = new();
    private readonly Dictionary<ActorId, bool> _busyLocked = new();

    public FuzzableFakeExecutor(Engine.Engine engine, FuzzConfig config, Random rng)
    {
        _engine = engine;
        _config = config;
        _rng = rng;
    }

    public void AdvanceTicks(long ticks)
    {
        _engine.AdvanceTicks(ticks);
        DoUpdate();
    }

    public void Update(double dtSeconds) => AdvanceTicks((long)(dtSeconds * Engine.Engine.TickHz));

    private void DoUpdate()
    {

        // Randomly inject no-shows and busy locks
        InjectFuzzBehaviors();

        // Complete any finished actions
        var completed = new List<ActorId>();
        foreach (var kvp in _runningActions)
        {
            var elapsed = _engine.CurrentTick - kvp.Value.StartTick;
            if (elapsed >= kvp.Value.AdjustedDurationTicks)
            {
                completed.Add(kvp.Key);
            }
        }

        foreach (var actorId in completed)
        {
            var (action, startTick, adjustedDurationTicks) = _runningActions[actorId];
            
            // Determine outcome with task failure rate
            var outcomeType = _rng.NextDouble() < _config.TaskFailureRate 
                ? ActionOutcomeType.Failed 
                : ActionOutcomeType.Success;

            _engine.ReportActionComplete(actorId, new ActionOutcome(
                action.Id,
                outcomeType,
                _engine.CurrentTick - startTick,
                action.ResultData
            ));
            _runningActions.Remove(actorId);

            // Clear busy lock if this was a busy lock task
            if (_busyLocked.ContainsKey(actorId) && action.Id.Value.Contains("busylock"))
            {
                _busyLocked.Remove(actorId);
            }
        }

        // Assign new actions to ready actors
        foreach (var kvp in _engine.Actors)
        {
            var actorId = kvp.Key;
            var actorState = kvp.Value;

            // Skip if actor is in no-show period
            if (_noShowUntil.TryGetValue(actorId, out var noShowTime) && _engine.CurrentTick < noShowTime)
            {
                continue;
            }
            else if (_noShowUntil.ContainsKey(actorId))
            {
                _noShowUntil.Remove(actorId);
            }

            if (actorState.Status == ActorStatus.Ready && !_runningActions.ContainsKey(actorId))
            {
                if (_engine.TryGetNextAction(actorId, out var action) && action != null)
                {
                    // Apply jitter to duration
                    var jitter = 1.0 + (_rng.NextDouble() * 2.0 - 1.0) * (_config.ActionDurationJitterPct / 100.0);
                    var adjustedDurationTicks = (long)(action.EstimatedDurationTicks * jitter);

                    // Add travel time jitter for MoveTo actions
                    if (action.Kind == ActionKind.MoveTo)
                    {
                        var travelJitter = 1.0 + (_rng.NextDouble() * 2.0 - 1.0) * (_config.TravelTimeJitterPct / 100.0);
                        adjustedDurationTicks = (long)(adjustedDurationTicks * travelJitter);
                    }

                    _runningActions[actorId] = (action, _engine.CurrentTick, adjustedDurationTicks);
                }
            }
        }
    }

    private void InjectFuzzBehaviors()
    {
        foreach (var actor in _engine.Actors)
        {
            var actorId = actor.Key;
            var actorState = actor.Value;

            // Skip if already has a behavior injected
            if (_noShowUntil.ContainsKey(actorId) || _busyLocked.ContainsKey(actorId) || _runningActions.ContainsKey(actorId))
            {
                continue;
            }

            // Randomly inject no-show
            if (actorState.Status == ActorStatus.Ready && _rng.NextDouble() < _config.NoShowProbability * 0.01)
            {
                var noShowDuration = (long)(_rng.Next(10, 60) * Engine.Engine.TickHz);
                _noShowUntil[actorId] = _engine.CurrentTick + noShowDuration;

                // Create a "no-show" idle action
                var idleAction = new ActionSpec(
                    new ActionId($"noshow_{actorId.Value}"),
                    ActionKind.Wait,
                    new ReasonActionParameters("unavailable"),
                    noShowDuration
                );

                _runningActions[actorId] = (idleAction, _engine.CurrentTick, noShowDuration);
                continue;
            }

            // Randomly inject busy lock
            if (actorState.Status == ActorStatus.Ready && _rng.NextDouble() < _config.BusyLockProbability * 0.01)
            {
                var busyDuration = (long)(_rng.Next(30, 120) * Engine.Engine.TickHz);
                _busyLocked[actorId] = true;

                var busyAction = new ActionSpec(
                    new ActionId($"busylock_{actorId.Value}"),
                    ActionKind.Wait,
                    new ReasonActionParameters("busy"),
                    busyDuration
                );

                _runningActions[actorId] = (busyAction, _engine.CurrentTick, busyDuration);
            }
        }
    }
}
