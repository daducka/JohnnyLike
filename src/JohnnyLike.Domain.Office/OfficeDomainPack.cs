using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Domain.Office;

public class OfficeDomainPack : IDomainPack
{
    public string DomainName => "Office";

    private readonly List<SceneTemplate> _sceneTemplates;

    public OfficeDomainPack()
    {
        _sceneTemplates = CreateSceneTemplates();
    }

    public WorldState CreateInitialWorldState()
    {
        return new OfficeWorldState();
    }

    public ActorState CreateActorState(ActorId actorId, Dictionary<string, object>? initialData = null)
    {
        var state = new OfficeActorState
        {
            Id = actorId,
            Hunger = initialData?.GetValueOrDefault("hunger", 0.0) as double? ?? 0.0,
            Energy = initialData?.GetValueOrDefault("energy", 100.0) as double? ?? 100.0,
            Social = initialData?.GetValueOrDefault("social", 50.0) as double? ?? 50.0
        };
        return state;
    }

    public List<ActionCandidate> GenerateCandidates(
        ActorId actorId,
        ActorState actorState,
        WorldState worldState,
        double currentTime,
        Random rng,
        IResourceAvailability resourceAvailability)
    {
        var candidates = new List<ActionCandidate>();
        var officeState = (OfficeActorState)actorState;
        var officeWorld = (OfficeWorldState)worldState;

        // EatSnack - higher score when hungry
        if (officeState.Hunger > 30.0)
        {
            var score = 0.5 + (officeState.Hunger / 100.0);
            candidates.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("eat_snack"),
                    ActionKind.Interact,
                    new OfficeInteractionActionParameters("kitchen", "eat"),
                    10.0
                ),
                score,
                "Hungry"
            ));
        }

        // CheckEmail
        candidates.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("check_email"),
                ActionKind.Interact,
                new OfficeInteractionActionParameters($"desk_{actorId.Value.ToLower()}", "type"),
                5.0 + rng.NextDouble() * 5.0
            ),
            0.6,
            "Check email"
        ));

        // Print document
        candidates.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("print_doc"),
                ActionKind.Interact,
                new OfficeInteractionActionParameters("printer", "print"),
                8.0
            ),
            0.5,
            "Print document"
        ));

        // Idle/Wait
        candidates.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("idle"),
                ActionKind.Wait,
                EmptyActionParameters.Instance,
                3.0
            ),
            0.3,
            "Idle"
        ));

        // Chat redeem handling
        if (!string.IsNullOrEmpty(officeState.LastChatRedeem))
        {
            candidates.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId($"chat_redeem_{officeState.LastChatRedeem}"),
                    ActionKind.Emote,
                    new OfficeChatRedeemParameters(officeState.LastChatRedeem),
                    2.0
                ),
                1.5,
                $"Chat redeem: {officeState.LastChatRedeem}"
            ));
        }

        return candidates;
    }

    public void ApplyActionEffects(
        ActorId actorId,
        ActionOutcome outcome,
        ActorState actorState,
        WorldState worldState,
        IRngStream rng,
        IResourceAvailability resourceAvailability,
        object? effectHandler = null)
    {
        var officeState = (OfficeActorState)actorState;
        var officeWorld = (OfficeWorldState)worldState;

        if (outcome.Type == ActionOutcomeType.Success)
        {
            // Update stats based on action
            if (outcome.ActionId.Value.Contains("eat"))
            {
                officeState.Hunger = Math.Max(0, officeState.Hunger - 30.0);
                officeState.Energy = Math.Min(100, officeState.Energy + 10.0);
            }
            else if (outcome.ActionId.Value.Contains("email"))
            {
                officeState.Energy = Math.Max(0, officeState.Energy - 5.0);
                officeState.Hunger = Math.Min(100, officeState.Hunger + 2.0);
            }
            else if (outcome.ActionId.Value.Contains("chat_redeem"))
            {
                officeState.LastChatRedeem = null;
                officeState.Social = Math.Min(100, officeState.Social + 20.0);
            }
            else if (outcome.ActionId.Value.Contains("highfive"))
            {
                officeState.Social = Math.Min(100, officeState.Social + 30.0);
            }
        }

        // Passive stat changes over time
        officeState.Hunger = Math.Min(100, officeState.Hunger + outcome.ActualDuration * 0.5);
        officeState.Energy = Math.Max(0, officeState.Energy - outcome.ActualDuration * 0.3);
    }

    public List<SceneTemplate> GetSceneTemplates()
    {
        return _sceneTemplates;
    }

    public bool ValidateContent(out List<string> errors)
    {
        errors = new List<string>();

        // Check that scene templates reference valid resources
        var validResources = new HashSet<string> { "printer", "kitchen", "desk_jim", "desk_pam", "conference_room" };

        foreach (var template in _sceneTemplates)
        {
            foreach (var resource in template.RequiredResources.Keys)
            {
                if (!validResources.Contains(resource))
                {
                    errors.Add($"Scene {template.SceneType} references invalid resource: {resource}");
                }
            }

            if (template.Roles.Count == 0)
            {
                errors.Add($"Scene {template.SceneType} has no roles");
            }
        }

        return errors.Count == 0;
    }

    public void OnSignal(Signal signal, ActorState? targetActor, WorldState worldState, double currentTime)
    {
        if (targetActor == null)
        {
            return;
        }

        var officeState = targetActor as OfficeActorState;
        if (officeState == null)
        {
            return;
        }

        // Handle chat redeem signals
        if (signal.Type == "chat_redeem" && signal.Data.TryGetValue("emote", out var emote))
        {
            officeState.LastChatRedeem = emote.ToString();
            officeState.LastChatRedeemTime = currentTime;
        }
    }

    public Dictionary<string, object> GetActorStateSnapshot(ActorState actorState)
    {
        var officeState = (OfficeActorState)actorState;
        return new Dictionary<string, object>
        {
            ["hunger"] = officeState.Hunger,
            ["energy"] = officeState.Energy,
            ["social"] = officeState.Social
        };
    }

    public List<TraceEvent> TickWorldState(WorldState worldState, double dtSeconds, IResourceAvailability resourceAvailability)
    {
        // Office domain has no passive world state changes
        return new List<TraceEvent>();
    }

    private List<SceneTemplate> CreateSceneTemplates()
    {
        var templates = new List<SceneTemplate>();

        // High-five at printer scene
        templates.Add(new SceneTemplate(
            "HIGHFIVE_PRINTER",
            new List<SceneRoleSpec>
            {
                new SceneRoleSpec(
                    "initiator",
                    actor => actor.Status == ActorStatus.Ready,
                    new ActionSpec(
                        new ActionId("highfive_init"),
                        ActionKind.Interact,
                        new OfficeInteractionActionParameters("colleague", "highfive"),
                        3.0
                    )
                ),
                new SceneRoleSpec(
                    "receiver",
                    actor => actor.Status == ActorStatus.Ready,
                    new ActionSpec(
                        new ActionId("highfive_recv"),
                        ActionKind.Interact,
                        new OfficeInteractionActionParameters("colleague", "highfive"),
                        3.0
                    )
                )
            },
            new Dictionary<string, object>
            {
                ["printer"] = true
            },
            10.0,  // 10 second join window
            20.0,  // 20 second max duration
            new Dictionary<string, object>
            {
                ["description"] = "Two actors high-five at the printer"
            }
        ));

        return templates;
    }
}
