using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;

namespace JohnnyLike.Domain.Island.Chat;

[IslandCandidateProvider(50)]
public class ChatCandidateProvider : IIslandCandidateProvider
{
    public void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        // Process pending chat actions first (unless survival critical)
        if (ctx.Actor.PendingChatActions.Count > 0)
        {
            var isSurvivalCritical = ctx.IsSurvivalCritical();
            
            if (!isSurvivalCritical)
            {
                var intent = ctx.Actor.PendingChatActions.Peek();
                
                if (intent.ActionId == "write_name_sand")
                {
                    var name = intent.Data.GetValueOrDefault("viewer_name", "Someone")?.ToString() ?? "Someone";
                    output.Add(new ActionCandidate(
                        new ActionSpec(
                            new ActionId("write_name_sand"),
                            ActionKind.Emote,
                            new()
                            {
                                ["name"] = name,
                                ["location"] = "beach"
                            },
                            8.0
                        ),
                        2.0, // High priority
                        $"Write {name}'s name in sand (chat redeem)"
                    ));
                }
                else if (intent.ActionId == "clap_emote")
                {
                    output.Add(new ActionCandidate(
                        new ActionSpec(
                            new ActionId("clap_emote"),
                            ActionKind.Emote,
                            new()
                            {
                                ["emote"] = "clap"
                            },
                            2.0
                        ),
                        2.0, // High priority
                        "Clap emote (sub/cheer)"
                    ));
                }
            }
        }
    }
}
