using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Domain.Kit.Dice;

public record SkillCheckResult(
    int Roll,
    int Total,
    RollOutcomeTier OutcomeTier,
    bool IsSuccess,
    double EstimatedSuccessChance
)
{
    public Dictionary<string, object> ToDictionary()
    {
        return new Dictionary<string, object>()
        {
            ["roll"] = Roll,
            ["total"] = Total,
            ["outcomeTier"] = OutcomeTier.ToString(),
            ["isSuccess"] = IsSuccess,
            ["estimatedSuccessChance"] = EstimatedSuccessChance,
        };
    }
}
