namespace JohnnyLike.Domain.Kit.Dice;

public record SkillCheckResult(
    int Roll,
    int Total,
    RollOutcomeTier OutcomeTier,
    bool IsSuccess,
    double EstimatedSuccessChance,
    int DC,
    int Modifier,
    AdvantageType Advantage,
    string SkillId
)
{
    /// <summary>
    /// Converts the skill check result to a dictionary for serialization and logging.
    /// </summary>
    public Dictionary<string, object> ToResultData() => new()
    {
        ["dc"] = DC,
        ["modifier"] = Modifier,
        ["advantage"] = Advantage.ToString(),
        ["skillId"] = SkillId,
        ["roll"] = Roll,
        ["total"] = Total,
        ["tier"] = OutcomeTier.ToString()
    };
}
