using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.SimRunner;

public class FakeExecutor
{
    private readonly Engine.Engine _engine;
    private readonly Dictionary<ActorId, (ActionSpec Action, long StartTick)> _runningActions = new();

    public FakeExecutor(Engine.Engine engine)
    {
        _engine = engine;
    }

    public void AdvanceTicks(long ticks)
    {
        _engine.AdvanceTicks(ticks);

        // Complete any finished actions
        var completed = new List<ActorId>();
        foreach (var kvp in _runningActions)
        {
            var elapsed = _engine.CurrentTick - kvp.Value.StartTick;
            if (elapsed >= kvp.Value.Action.EstimatedDurationTicks)
            {
                completed.Add(kvp.Key);
            }
        }

        foreach (var actorId in completed)
        {
            var (action, startTick) = _runningActions[actorId];
            var actualDurationTicks = _engine.CurrentTick - startTick;

            _engine.ReportActionComplete(actorId, new ActionOutcome(
                action.Id,
                ActionOutcomeType.Success,
                actualDurationTicks,
                action.ResultData
            ));
            _runningActions.Remove(actorId);
        }

        // Assign new actions to ready actors
        foreach (var kvp in _engine.Actors)
        {
            var actorId = kvp.Key;
            var actorState = kvp.Value;

            if (actorState.Status == ActorStatus.Ready && !_runningActions.ContainsKey(actorId))
            {
                if (_engine.TryGetNextAction(actorId, out var action) && action != null)
                {
                    _runningActions[actorId] = (action, _engine.CurrentTick);
                }
            }
        }
    }

    /// <summary>Backward-compat helper: converts seconds to ticks.</summary>
    public void Update(double dtSeconds) => AdvanceTicks((long)(dtSeconds * Engine.Engine.TickHz));
}
