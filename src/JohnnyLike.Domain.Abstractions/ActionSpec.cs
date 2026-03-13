namespace JohnnyLike.Domain.Abstractions;

public enum ActionKind
{
    MoveTo,
    Wait,
    Emote,
    Interact,
    Speak,
    LookAt
}

/// <summary>
/// Represents a resource requirement for an action.
/// </summary>
public sealed record ResourceRequirement(
    ResourceId ResourceId,
    Duration? DurationOverride = null
);

public record ActionSpec(
    ActionId Id,
    ActionKind Kind,
    ActionParameters Parameters,
    Duration EstimatedDuration,
    string NarrationDescription,
    Dictionary<string, object>? ResultData = null,
    IReadOnlyList<ResourceRequirement>? ResourceRequirements = null
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
    Duration ActualDuration,
    Dictionary<string, object>? ResultData = null
);
