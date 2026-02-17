using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Candidates;

public class IslandContext
{
    public IslandWorldState World { get; }
    public IslandActorState Actor { get; }
    public ActorId ActorId { get; }
    public double NowSeconds { get; }
    public IRngStream Rng { get; }
    public Random Random { get; }

    public IslandContext(
        ActorId actorId,
        IslandActorState actor,
        IslandWorldState world,
        double nowSeconds,
        IRngStream rng,
        Random random)
    {
        ActorId = actorId;
        Actor = actor;
        World = world;
        NowSeconds = nowSeconds;
        Rng = rng;
        Random = random;
    }

    // Helper methods for scoring
    public double Clamp(double value, double min, double max)
    {
        return Math.Max(min, Math.Min(max, value));
    }

    public bool IsSurvivalCritical()
    {
        return Actor.Hunger > 80.0 || Actor.Energy < 15.0;
    }

    /// <summary>
    /// Helper to roll a skill check and return both parameters and result data.
    /// Encapsulates the common pattern of skill check resolution at candidate generation time.
    /// </summary>
    public (SkillCheckActionParameters Parameters, Dictionary<string, object> ResultData, SkillCheckResult Result) RollSkillCheck(
        string skillId,
        int baseDC,
        string location)
    {
        var modifier = Actor.GetSkillModifier(skillId);
        var advantage = Actor.GetAdvantage(skillId);

        var request = new SkillCheckRequest(baseDC, modifier, advantage, skillId);
        var result = SkillCheckResolver.Resolve(Rng, request);

        var parameters = new SkillCheckActionParameters(baseDC, modifier, advantage, location, skillId);

        return (parameters, result.ToResultData(), result);
    }
}
