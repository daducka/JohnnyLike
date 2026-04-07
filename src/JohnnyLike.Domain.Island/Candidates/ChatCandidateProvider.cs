using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Domain.Island.Candidates;

/// <summary>
/// Shared provider for chat-triggered and expressive/emote action candidates.
/// Centralizes all pending-chat-intent candidate generation so the logic is reusable
/// across actor types and is not embedded directly in <see cref="HumanActorState"/>.
/// </summary>
public static class ChatCandidateProvider
{
    /// <summary>
    /// Adds candidates driven by <paramref name="actor"/>'s pending chat actions
    /// (e.g. viewer redeems, subs, cheers) to <paramref name="output"/>.
    /// Candidates are only emitted when the actor is not in a survival-critical state.
    /// </summary>
    public static void AddCandidates(HumanActorState actor, IslandContext ctx, List<ActionCandidate> output)
    {
        if (actor.PendingChatActions.Count == 0)
            return;

        if (ctx.IsSurvivalCritical())
            return;

        var intent = actor.PendingChatActions.Peek();

        if (intent.ActionId == "write_name_sand")
        {
            var name = intent.Data.GetValueOrDefault("viewer_name", "Someone")?.ToString() ?? "Someone";
            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("write_name_sand"),
                    ActionKind.Emote,
                    new EmoteActionParameters("write_name", name, "beach"),
                    Duration.Seconds(8.0),
                    NarrationDescription: "write name in the sand"
                ),
                1.1,
                Reason: $"Write {name}'s name in sand (chat redeem)",
                EffectHandler: new Action<EffectContext>(effectCtx =>
                {
                    if (effectCtx.Actor is HumanActorState humanActor &&
                        humanActor.PendingChatActions.Count > 0)
                        humanActor.PendingChatActions.Dequeue();

                    effectCtx.Actor.Morale += 10.0;
                }),
                Qualities: new Dictionary<QualityType, double>
                {
                    [QualityType.Fun]     = 0.8,
                    [QualityType.Comfort] = 0.2
                },
                ActorRequirement: actor => CandidateRequirements.IsHuman(actor) && CandidateRequirements.AliveOnly(actor)
            ));
        }
        else if (intent.ActionId == "clap_emote")
        {
            output.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("clap_emote"),
                    ActionKind.Emote,
                    new EmoteActionParameters("clap"),
                    Duration.Seconds(2.0),
                    NarrationDescription: "clap"
                ),
                1.0,
                Reason: "Clap emote (sub/cheer)",
                EffectHandler: new Action<EffectContext>(effectCtx =>
                {
                    if (effectCtx.Actor is HumanActorState humanActor &&
                        humanActor.PendingChatActions.Count > 0)
                        humanActor.PendingChatActions.Dequeue();

                    effectCtx.Actor.Morale += 3.0;
                }),
                Qualities: new Dictionary<QualityType, double>
                {
                    [QualityType.Fun]     = 0.8,
                    [QualityType.Comfort] = 0.2
                },
                ActorRequirement: actor => CandidateRequirements.IsHuman(actor) && CandidateRequirements.AliveOnly(actor)
            ));
        }
    }
}
