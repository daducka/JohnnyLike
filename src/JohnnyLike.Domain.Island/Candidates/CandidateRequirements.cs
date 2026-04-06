using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Domain.Island.Candidates;

/// <summary>
/// Reusable actor requirement predicates for use in <see cref="ActionCandidate.ActorRequirement"/>.
/// These predicates are evaluated before scoring; candidates whose requirement returns
/// <c>false</c> are omitted from the filtered candidate set.
/// </summary>
public static class CandidateRequirements
{
    /// <summary>
    /// Requires the actor to have an <see cref="AlivenessBuff"/> with
    /// <see cref="AlivenessState.Alive"/>.
    /// All standard island actions should use this requirement.
    /// </summary>
    public static Func<ActorState, bool> AliveOnly { get; } =
        actor => actor is LivingActorState living &&
                 living.HasBuffWhere<AlivenessBuff>(b => b.State == AlivenessState.Alive);

    /// <summary>
    /// Requires the actor to have an <see cref="AlivenessBuff"/> with
    /// <see cref="AlivenessState.Downed"/>.
    /// Actions that downed actors can perform while fighting for survival should use this.
    /// </summary>
    public static Func<ActorState, bool> DownedOnly { get; } =
        actor => actor is LivingActorState living &&
                 living.HasBuffWhere<AlivenessBuff>(b => b.State == AlivenessState.Downed);

    /// <summary>
    /// Requires the actor to have an <see cref="AlivenessBuff"/> with
    /// <see cref="AlivenessState.Dead"/>.
    /// Reserved for future corpse-interaction candidates.
    /// </summary>
    public static Func<ActorState, bool> DeadOnly { get; } =
        actor => actor is LivingActorState living &&
                 living.HasBuffWhere<AlivenessBuff>(b => b.State == AlivenessState.Dead);

    /// <summary>
    /// Requires the actor to be alive and in reasonably good condition to engage in
    /// playful or recreational comfort actions. Passes when:
    /// Satiety &gt; 25, Morale &gt; 35, Health &gt; 50, Energy &gt; 30.
    /// </summary>
    public static Func<ActorState, bool> PlayfulOnly { get; } =
        actor => actor is LivingActorState living &&
                 living.HasBuffWhere<AlivenessBuff>(b => b.State == AlivenessState.Alive) &&
                 living.Satiety > 25 &&
                 living.Morale  > 35 &&
                 living.Health  > 50 &&
                 living.Energy  > 30;

    /// <summary>
    /// Requires the actor to be alive and in a state of despair or suffering.
    /// Passes when: Satiety &lt; 25, OR Morale &lt; 25, OR Health &lt; 40.
    /// </summary>
    public static Func<ActorState, bool> DespairingOnly { get; } =
        actor => actor is LivingActorState living &&
                 living.HasBuffWhere<AlivenessBuff>(b => b.State == AlivenessState.Alive) &&
                 (living.Satiety < 25 || living.Morale < 25 || living.Health < 40);

    /// <summary>
    /// Returns a requirement predicate that passes when the actor has at least one active
    /// buff of type <typeparamref name="T"/>.
    /// </summary>
    public static Func<ActorState, bool> HasBuff<T>() where T : ActiveBuff =>
        actor => actor is LivingActorState living && living.HasBuff<T>();

    /// <summary>
    /// Returns a requirement predicate that passes when the actor has at least one active
    /// buff of type <typeparamref name="T"/> that also satisfies <paramref name="predicate"/>.
    /// </summary>
    public static Func<ActorState, bool> HasBuffWhere<T>(Func<T, bool> predicate) where T : ActiveBuff =>
        actor => actor is LivingActorState living && living.HasBuffWhere(predicate);
}
