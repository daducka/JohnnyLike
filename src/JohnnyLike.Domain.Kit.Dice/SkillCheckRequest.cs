namespace JohnnyLike.Domain.Kit.Dice;

public record SkillCheckRequest(
    int DC,
    int Modifier,
    AdvantageType Advantage,
    string SkillId,
    string? Context = null
);
