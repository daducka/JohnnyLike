using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Items;

namespace JohnnyLike.Domain.Island;

/// <summary>
/// Shared helpers for mortality state transitions: collapse (alive → downed),
/// revive (downed → alive), and death (downed → dead).
///
/// Centralizes the transition logic so that any living entity on the island can
/// reuse the same workflow rather than duplicating it inline per actor class.
/// </summary>
public static class MortalityWorkflow
{
    /// <summary>
    /// Transitions an actor from <see cref="AlivenessState.Alive"/> to
    /// <see cref="AlivenessState.Downed"/> when health reaches zero.
    /// Resets save counters and emits the collapse narration beat.
    /// </summary>
    public static void Collapse(
        AlivenessBuff aliveness,
        string actorName,
        IEventTracer tracer)
    {
        aliveness.State              = AlivenessState.Downed;
        aliveness.DeathSaveSuccesses = 0;
        aliveness.DeathSaveFailures  = 0;
        tracer.Beat(
            $"[Aliveness] {actorName} has collapsed and is fighting to stay conscious",
            actorId: actorName,
            priority: 70);
    }

    /// <summary>
    /// Revives a <see cref="AlivenessState.Downed"/> actor: transitions to
    /// <see cref="AlivenessState.Alive"/>, restores health to <paramref name="reviveHealth"/>,
    /// resets save counters, emits a stabilization trace beat, and sets the outcome narration.
    /// </summary>
    public static void Revive(
        AlivenessBuff aliveness,
        LivingActorState actor,
        double reviveHealth,
        string actorName,
        EffectContext ctx,
        string narration)
    {
        aliveness.State              = AlivenessState.Alive;
        aliveness.DeathSaveSuccesses = 0;
        aliveness.DeathSaveFailures  = 0;
        actor.Health = reviveHealth;
        ctx.World.Tracer.Beat(
            $"[Aliveness] {actorName} stabilized and regained consciousness",
            actorId: actorName, priority: 80);
        ctx.SetOutcomeNarration(narration);
    }

    /// <summary>
    /// Finalizes the death of a <see cref="AlivenessState.Downed"/> actor: transitions to
    /// <see cref="AlivenessState.Dead"/>, emits death trace beats, sets the outcome narration,
    /// and spawns a <see cref="CorpseItem"/> in the actor's current room.
    /// </summary>
    public static void Die(
        AlivenessBuff aliveness,
        LivingActorState actor,
        string actorName,
        EffectContext ctx,
        string narration)
    {
        aliveness.State = AlivenessState.Dead;
        ctx.World.Tracer.Beat(
            $"[Aliveness] {actorName} has died",
            actorId: actorName, priority: 90);
        ctx.SetOutcomeNarration(narration);

        var corpseId = $"corpse_{actor.Id.Value.ToLowerInvariant()}";
        var corpse   = new CorpseItem(corpseId) { ActorName = actorName };
        var roomId   = actor.CurrentRoomId;
        ctx.World.AddWorldItem(corpse, roomId);
        ctx.World.Tracer.Beat(
            $"[Aliveness] The remains of {actorName} now lie at {roomId}",
            actorId: actorName, priority: 70);
    }
}
