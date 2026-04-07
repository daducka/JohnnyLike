using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island;

/// <summary>
/// Skill types used for DnD-style skill checks on living actors.
/// </summary>
public enum SkillType
{
    Fishing,
    Survival,
    Perception,
    Performance,
    Athletics,
    Constitution
}

/// <summary>
/// Generic base class for all living actors in the island simulation.
/// Owns DnD-style core stats, physiological vitals, active buffs,
/// aliveness state (via <see cref="AlivenessBuff"/>), presence state, and skill-check helpers.
///
/// Humanoid-specific state (decision pragmatism, recipe knowledge, chat, softmax tuning)
/// lives in the derived <see cref="HumanActorState"/> class.
/// </summary>
public abstract class LivingActorState : ActorState
{
    // ── DnD-style ability scores ──────────────────────────────────────────────
    public int STR { get; set; } = 10;
    public int DEX { get; set; } = 10;
    public int CON { get; set; } = 10;
    public int INT { get; set; } = 10;
    public int WIS { get; set; } = 10;
    public int CHA { get; set; } = 10;

    // ── Derived skill modifiers ───────────────────────────────────────────────
    public int FishingSkill      => DndMath.AbilityModifier(DEX) + DndMath.AbilityModifier(WIS);
    public int SurvivalSkill     => DndMath.AbilityModifier(WIS) + DndMath.AbilityModifier(STR);
    public int PerceptionSkill   => DndMath.AbilityModifier(WIS);
    public int PerformanceSkill  => DndMath.AbilityModifier(CHA);
    public int AthleticsSkill    => DndMath.AbilityModifier(STR);
    public int ConstitutionSkill => DndMath.AbilityModifier(CON);

    // ── Physiological vitals ──────────────────────────────────────────────────
    private double _satiety = 100.0;
    private double _energy  = 100.0;
    private double _morale  = 50.0;
    private double _health  = 100.0;

    public double Satiety { get => _satiety; set => _satiety = Math.Clamp(value, 0.0, 100.0); }
    public double Energy   { get => _energy;  set => _energy  = Math.Clamp(value, 0.0, 100.0); }
    public double Morale   { get => _morale;  set => _morale  = Math.Clamp(value, 0.0, 100.0); }
    public double Health   { get => _health;  set => _health  = Math.Clamp(value, 0.0, 100.0); }

    // ── Active buffs ──────────────────────────────────────────────────────────
    public List<ActiveBuff> ActiveBuffs { get; set; } = new();

    // ── Buff helpers ──────────────────────────────────────────────────────────

    /// <summary>Returns <c>true</c> if the actor currently has an active buff of type <typeparamref name="T"/>.</summary>
    public bool HasBuff<T>() where T : ActiveBuff
        => ActiveBuffs.OfType<T>().Any();

    /// <summary>
    /// Returns the first active buff of type <typeparamref name="T"/>, or <c>null</c> if none is present.
    /// </summary>
    public T? TryGetBuff<T>() where T : ActiveBuff
        => ActiveBuffs.OfType<T>().FirstOrDefault();

    /// <summary>
    /// Returns <c>true</c> if the actor has an active buff of type <typeparamref name="T"/>
    /// that also satisfies <paramref name="predicate"/>.
    /// </summary>
    public bool HasBuffWhere<T>(Func<T, bool> predicate) where T : ActiveBuff
        => ActiveBuffs.OfType<T>().Any(predicate);

    // ── Skill-check helpers ───────────────────────────────────────────────────

    public int GetSkillModifier(SkillType skillType)
    {
        var baseModifier = skillType switch
        {
            SkillType.Fishing      => FishingSkill,
            SkillType.Survival     => SurvivalSkill,
            SkillType.Perception   => PerceptionSkill,
            SkillType.Performance  => PerformanceSkill,
            SkillType.Athletics    => AthleticsSkill,
            SkillType.Constitution => ConstitutionSkill,
            _ => 0
        };

        var buffModifier = ActiveBuffs
            .Where(b => (b.SkillType == skillType || b.SkillType == null) && b.Type == BuffType.SkillBonus)
            .Sum(b => b.Value);

        return baseModifier + buffModifier;
    }

    public AdvantageType GetAdvantage(SkillType skillType)
    {
        var hasBuff = ActiveBuffs.Any(b => b.SkillType == skillType && b.Type == BuffType.Advantage);
        return hasBuff ? AdvantageType.Advantage : AdvantageType.Normal;
    }

    // ── Lifecycle hooks ───────────────────────────────────────────────────────

    /// <summary>
    /// Called each world tick for actors with <see cref="PresenceState.Stashed"/> presence.
    /// Override in derived classes to implement lightweight offstage bookkeeping.
    /// The default implementation is a no-op.
    /// </summary>
    public virtual void OnStashTick(long currentTick) { }
}
