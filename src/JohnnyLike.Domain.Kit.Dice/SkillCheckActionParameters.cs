using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Domain.Kit.Dice;

/// <summary>
/// Parameters for actions that require a skill check.
/// Generic and reusable across all domain packs.
/// </summary>
public record SkillCheckActionParameters(
    SkillCheckRequest Request,
    SkillCheckResult Result,
    string Location
) : ActionParameters
{
    public override Dictionary<string, object> ToDictionary() => new()
    {
        ["request"] = Request.ToDictionary(),
        ["result"] = Result.ToDictionary(),
        ["location"] = Location
    };
}
