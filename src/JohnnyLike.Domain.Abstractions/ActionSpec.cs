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
    ActionParameters Parameters,
    double EstimatedDuration
);

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
