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
    
    /// <summary>
    /// Creates a combined dictionary with all skill check data for ActionSpec.ResultData.
    /// Merges request and result data into a flat structure.
    /// </summary>
    public Dictionary<string, object> ToResultData()
    {
        var dict = new Dictionary<string, object>
        {
            ["dc"] = Request.DC,
            ["modifier"] = Request.Modifier,
            ["advantage"] = Request.Advantage.ToString(),
            ["skillId"] = Request.SkillId,
            ["roll"] = Result.Roll,
            ["total"] = Result.Total,
            ["tier"] = Result.OutcomeTier.ToString()
        };
        return dict;
    }
}
