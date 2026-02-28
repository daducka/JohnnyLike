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
    long? DurationTicksOverride = null
);

public record ActionSpec(
    ActionId Id,
    ActionKind Kind,
    ActionParameters Parameters,
    long EstimatedDurationTicks,
    Dictionary<string, object>? ResultData = null,
    IReadOnlyList<ResourceRequirement>? ResourceRequirements = null,
    string? NarrationDescription = null
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
    long ActualDurationTicks,
    Dictionary<string, object>? ResultData = null
);
