namespace JohnnyLike.Domain.Kit.Dice;

public record SkillCheckRequest(
    int DC,
    int Modifier,
    AdvantageType Advantage,
    string SkillId
)
{
    public Dictionary<string, object> ToDictionary() => new()
    {
        ["dc"] = DC,
        ["modifier"] = Modifier,
        ["advantage"] = Advantage.ToString(),
        ["skillId"] = SkillId,
    };
};
