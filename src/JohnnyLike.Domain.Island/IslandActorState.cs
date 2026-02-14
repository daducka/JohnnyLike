using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Kit.Dice;
using System.Text.Json;

namespace JohnnyLike.Domain.Island;

public class IslandActorState : ActorState
{
    public int STR { get; set; } = 10;
    public int DEX { get; set; } = 10;
    public int CON { get; set; } = 10;
    public int INT { get; set; } = 10;
    public int WIS { get; set; } = 10;
    public int CHA { get; set; } = 10;

    public int FishingSkill => DndMath.AbilityModifier(DEX) + DndMath.AbilityModifier(WIS);
    public int SurvivalSkill => DndMath.AbilityModifier(WIS) + DndMath.AbilityModifier(STR);
    public int PerceptionSkill => DndMath.AbilityModifier(WIS);
    public int PerformanceSkill => DndMath.AbilityModifier(CHA);

    public double Hunger { get; set; } = 0.0;
    public double Energy { get; set; } = 100.0;
    public double Morale { get; set; } = 50.0;
    public double Boredom { get; set; } = 0.0;

    public List<ActiveBuff> ActiveBuffs { get; set; } = new();

    public int GetSkillModifier(string skillId)
    {
        var baseModifier = skillId switch
        {
            "Fishing" => FishingSkill,
            "Survival" => SurvivalSkill,
            "Perception" => PerceptionSkill,
            "Performance" => PerformanceSkill,
            _ => 0
        };

        var buffModifier = ActiveBuffs
            .Where(b => b.SkillId == skillId && b.Type == BuffType.SkillBonus)
            .Sum(b => b.Value);

        return baseModifier + buffModifier;
    }

    public AdvantageType GetAdvantage(string skillId)
    {
        var hasBuff = ActiveBuffs.Any(b => b.SkillId == skillId && b.Type == BuffType.Advantage);
        return hasBuff ? AdvantageType.Advantage : AdvantageType.Normal;
    }

    public override string Serialize()
    {
        return JsonSerializer.Serialize(new
        {
            Id = Id.Value,
            Status,
            CurrentAction = CurrentAction?.Id.Value,
            CurrentScene = CurrentScene?.Value,
            LastDecisionTime,
            STR,
            DEX,
            CON,
            INT,
            WIS,
            CHA,
            Hunger,
            Energy,
            Morale,
            Boredom,
            ActiveBuffs
        });
    }

    public override void Deserialize(string json)
    {
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        if (data == null) return;

        Id = new ActorId(data["Id"].GetString()!);
        Status = Enum.Parse<ActorStatus>(data["Status"].GetString()!);
        LastDecisionTime = data["LastDecisionTime"].GetDouble();
        STR = data["STR"].GetInt32();
        DEX = data["DEX"].GetInt32();
        CON = data["CON"].GetInt32();
        INT = data["INT"].GetInt32();
        WIS = data["WIS"].GetInt32();
        CHA = data["CHA"].GetInt32();
        Hunger = data["Hunger"].GetDouble();
        Energy = data["Energy"].GetDouble();
        Morale = data["Morale"].GetDouble();
        Boredom = data["Boredom"].GetDouble();

        if (data.TryGetValue("ActiveBuffs", out var buffs))
        {
            ActiveBuffs = JsonSerializer.Deserialize<List<ActiveBuff>>(buffs.GetRawText()) ?? new();
        }
    }
}

public enum BuffType
{
    SkillBonus,
    Advantage
}

public class ActiveBuff
{
    public string Name { get; set; } = "";
    public BuffType Type { get; set; }
    public string SkillId { get; set; } = "";
    public int Value { get; set; }
    public double ExpiresAt { get; set; }
}
