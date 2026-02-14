namespace JohnnyLike.Domain.Kit.Dice;

public record SkillCheckResult(
    int Roll,
    int Total,
    RollOutcomeTier OutcomeTier,
    bool IsSuccess,
    double EstimatedSuccessChance
);
