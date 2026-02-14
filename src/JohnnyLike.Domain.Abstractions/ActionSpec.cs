namespace JohnnyLike.Domain.Abstractions;

public enum ActionKind
{
    MoveTo,
    Wait,
    Emote,
    Interact,
    Speak,
    LookAt,
    JoinScene
}

public record ActionSpec(
    ActionId Id,
    ActionKind Kind,
    Dictionary<string, object> Parameters,
    double EstimatedDuration
)
{
    public T? GetParameter<T>(string key) where T : class
    {
        return Parameters.TryGetValue(key, out var value) && value is T typed ? typed : null;
    }

    public T GetParameterValue<T>(string key, T defaultValue = default!) where T : struct
    {
        return Parameters.TryGetValue(key, out var value) && value is T typed ? typed : defaultValue;
    }
}

public enum ActionOutcomeType
{
    Success,
    Failed,
    TimedOut,
    Cancelled
}

public record ActionOutcome(
    ActionId ActionId,
    ActionOutcomeType Type,
    double ActualDuration,
    Dictionary<string, object>? ResultData = null
);
