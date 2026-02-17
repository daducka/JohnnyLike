using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.SimRunner;

public class FakeExecutor
{
    private readonly Engine.Engine _engine;
    private readonly Dictionary<ActorId, (ActionSpec Action, double StartTime)> _runningActions = new();

    public FakeExecutor(Engine.Engine engine)
    {
        _engine = engine;
    }

    public void Update(double dtSeconds)
    {
        _engine.AdvanceTime(dtSeconds);

        // Complete any finished actions
        var completed = new List<ActorId>();
        foreach (var kvp in _runningActions)
        {
            var elapsed = _engine.CurrentTime - kvp.Value.StartTime;
            if (elapsed >= kvp.Value.Action.EstimatedDuration)
            {
                completed.Add(kvp.Key);
            }
        }

        foreach (var actorId in completed)
        {
            var (action, startTime) = _runningActions[actorId];
            var actualDuration = _engine.CurrentTime - startTime;
            _engine.ReportActionComplete(actorId, new ActionOutcome(
                action.Id,
                ActionOutcomeType.Success,
                actualDuration,
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
                    _runningActions[actorId] = (action, _engine.CurrentTime);
                }
            }
        }
    }
}
