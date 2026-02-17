using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Domain.Kit.Dice;

/// <summary>
/// Parameters for actions that require a skill check.
/// Generic and reusable across all domain packs.
/// </summary>
public record SkillCheckActionParameters(
    int DC,
    int Modifier,
    AdvantageType Advantage,
    string Location,
    string SkillId
) : ActionParameters
{
    public override Dictionary<string, object> ToDictionary() => new()
    {
        ["dc"] = DC,
        ["modifier"] = Modifier,
        ["advantage"] = Advantage.ToString(),
        ["location"] = Location,
        ["skillId"] = SkillId
    };
}
