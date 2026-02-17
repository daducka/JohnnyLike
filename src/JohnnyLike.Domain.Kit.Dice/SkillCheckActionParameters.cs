using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Domain.Kit.Dice;

/// <summary>
/// Parameters for actions that require a skill check.
/// Generic and reusable across all domain packs.
/// </summary>
public record SkillCheckActionParameters(
    SkillCheckRequest Request,
    SkillCheckResult Result,
    string? Location = null
) : ActionParameters
{
    public override Dictionary<string, object> ToDictionary()
    {
        var dict = new Dictionary<string, object>
        {
            ["request"] = Request.ToDictionary(),
            ["result"] = Result.ToDictionary()
        };
        
        if (Location != null)
        {
            dict["location"] = Location;
        }
        
        return dict;
    }
    
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
