namespace JohnnyLike.Domain.Kit.Dice;

public record SkillCheckResult(
    int Roll,
    int Total,
    RollOutcomeTier OutcomeTier,
    bool IsSuccess,
    double EstimatedSuccessChance
)
{
    /// <summary>
    /// Dictionary containing detailed result data for serialization and logging.
    /// Includes: dc, modifier, advantage, skillId, roll, total, tier
    /// </summary>
    public Dictionary<string, object>? ResultData { get; init; }
}
