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
        actor => actor is IslandActorState island &&
                 island.HasBuffWhere<AlivenessBuff>(b => b.State == AlivenessState.Alive);

    /// <summary>
    /// Requires the actor to be alive and in reasonably good condition to engage in
    /// playful or recreational comfort actions. Passes when:
    /// Satiety &gt; 25, Morale &gt; 35, Health &gt; 50, Energy &gt; 30.
    /// </summary>
    public static Func<ActorState, bool> PlayfulOnly { get; } =
        actor => actor is IslandActorState island &&
                 island.HasBuffWhere<AlivenessBuff>(b => b.State == AlivenessState.Alive) &&
                 island.Satiety > 25 &&
                 island.Morale  > 35 &&
                 island.Health  > 50 &&
                 island.Energy  > 30;

    /// <summary>
    /// Requires the actor to be alive and in a state of despair or suffering.
    /// Passes when: Satiety &lt; 25, OR Morale &lt; 25, OR Health &lt; 40.
    /// </summary>
    public static Func<ActorState, bool> DespairingOnly { get; } =
        actor => actor is IslandActorState island &&
                 island.HasBuffWhere<AlivenessBuff>(b => b.State == AlivenessState.Alive) &&
                 (island.Satiety < 25 || island.Morale < 25 || island.Health < 40);

    /// <summary>
    /// Returns a requirement predicate that passes when the actor has at least one active
    /// buff of type <typeparamref name="T"/>.
    /// </summary>
    public static Func<ActorState, bool> HasBuff<T>() where T : ActiveBuff =>
        actor => actor is IslandActorState island && island.HasBuff<T>();

    /// <summary>
    /// Returns a requirement predicate that passes when the actor has at least one active
    /// buff of type <typeparamref name="T"/> that also satisfies <paramref name="predicate"/>.
    /// </summary>
    public static Func<ActorState, bool> HasBuffWhere<T>(Func<T, bool> predicate) where T : ActiveBuff =>
        actor => actor is IslandActorState island && island.HasBuffWhere(predicate);
}
