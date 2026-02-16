using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Domain.Office;

/// <summary>
/// Parameters for office interaction actions with target and action type.
/// </summary>
public record OfficeInteractionActionParameters(
    string Target,
    string Action
) : ActionParameters
{
    public override Dictionary<string, object> ToDictionary() => new()
    {
        ["target"] = Target,
        ["action"] = Action
    };
}

/// <summary>
/// Parameters for chat redeem emote actions.
/// </summary>
public record OfficeChatRedeemParameters(
    string Emote
) : ActionParameters
{
    public override Dictionary<string, object> ToDictionary() => new()
    {
        ["emote"] = Emote
    };
}
