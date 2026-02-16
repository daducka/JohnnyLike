using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Kit.Dice;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Items;
using System.Reflection;

namespace JohnnyLike.Domain.Island;

public class IslandDomainPack : IDomainPack
{
    private readonly List<IIslandCandidateProvider> _providers;
    private readonly Dictionary<string, IIslandCandidateProvider> _effectHandlers;
    private static List<IIslandCandidateProvider>? _cachedProviders;
    private static Dictionary<string, IIslandCandidateProvider>? _cachedEffectHandlers;
    private static readonly object _lock = new object();

    public string DomainName => "Island";

    public IslandDomainPack()
    {
        // Use cached providers if available, otherwise discover and cache
        if (_cachedProviders == null)
        {
            lock (_lock)
            {
                if (_cachedProviders == null)
                {
                    (_cachedProviders, _cachedEffectHandlers) = DiscoverProviders();
                }
            }
        }
        
        _providers = _cachedProviders;
        _effectHandlers = _cachedEffectHandlers!;
    }

    private static (List<IIslandCandidateProvider>, Dictionary<string, IIslandCandidateProvider>) DiscoverProviders()
    {
        // Discover types with attributes
        var typesWithAttrs = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.GetCustomAttribute<IslandCandidateProviderAttribute>() != null)
            .Where(t => !t.IsAbstract && typeof(IIslandCandidateProvider).IsAssignableFrom(t))
            .Select(t => new {
                Type = t,
                Attr = t.GetCustomAttribute<IslandCandidateProviderAttribute>()!
            })
            .OrderBy(x => x.Attr.Order)
            .ToList();

        // Create provider instances once
        var providers = new List<IIslandCandidateProvider>();
        var effectHandlers = new Dictionary<string, IIslandCandidateProvider>();

        foreach (var item in typesWithAttrs)
        {
            var provider = (IIslandCandidateProvider)Activator.CreateInstance(item.Type)!;
            providers.Add(provider);

            // Build action ID -> provider lookup
            foreach (var actionId in item.Attr.ActionIds)
            {
                effectHandlers[actionId] = provider;
            }
        }

        return (providers, effectHandlers);
    }

    public WorldState CreateInitialWorldState()
    {
        var world = new IslandWorldState();
        world.WorldItems.Add(new CampfireItem("main_campfire"));
        world.WorldItems.Add(new ShelterItem("main_shelter"));
        return world;
    }

    public ActorState CreateActorState(ActorId actorId, Dictionary<string, object>? initialData = null)
    {
        var state = new IslandActorState
        {
            Id = actorId,
            STR = (int)(initialData?.GetValueOrDefault("STR", 10) ?? 10),
            DEX = (int)(initialData?.GetValueOrDefault("DEX", 10) ?? 10),
            CON = (int)(initialData?.GetValueOrDefault("CON", 10) ?? 10),
            INT = (int)(initialData?.GetValueOrDefault("INT", 10) ?? 10),
            WIS = (int)(initialData?.GetValueOrDefault("WIS", 10) ?? 10),
            CHA = (int)(initialData?.GetValueOrDefault("CHA", 10) ?? 10),
            Hunger = (double)(initialData?.GetValueOrDefault("hunger", 0.0) ?? 0.0),
            Energy = (double)(initialData?.GetValueOrDefault("energy", 100.0) ?? 100.0),
            Morale = (double)(initialData?.GetValueOrDefault("morale", 50.0) ?? 50.0),
            Boredom = (double)(initialData?.GetValueOrDefault("boredom", 0.0) ?? 0.0)
        };
        return state;
    }

    public List<ActionCandidate> GenerateCandidates(
        ActorId actorId,
        ActorState actorState,
        WorldState worldState,
        double currentTime,
        Random rng)
    {
        var islandState = (IslandActorState)actorState;
        var islandWorld = (IslandWorldState)worldState;
        var rngStream = new RandomRngStream(rng);

        islandWorld.OnTimeAdvanced(currentTime, 0.0);

        islandState.ActiveBuffs.RemoveAll(b => b.ExpiresAt <= currentTime);

        // Create context for providers
        var ctx = new IslandContext(
            actorId,
            islandState,
            islandWorld,
            currentTime,
            rngStream,
            rng
        );

        // Generate candidates using all registered providers
        var candidates = new List<ActionCandidate>();
        foreach (var provider in _providers)
        {
            provider.AddCandidates(ctx, candidates);
        }

        return candidates;
    }

    public void ApplyActionEffects(
        ActorId actorId,
        ActionOutcome outcome,
        ActorState actorState,
        WorldState worldState,
        IRngStream rng)
    {
        var islandState = (IslandActorState)actorState;
        var islandWorld = (IslandWorldState)worldState;

        var newCurrentTime = islandWorld.CurrentTime + outcome.ActualDuration;
        islandWorld.OnTimeAdvanced(newCurrentTime, outcome.ActualDuration);

        islandState.Hunger = Math.Min(100.0, islandState.Hunger + outcome.ActualDuration * 0.5);
        islandState.Energy = Math.Max(0.0, islandState.Energy - outcome.ActualDuration * 0.3);
        islandState.Boredom = Math.Min(100.0, islandState.Boredom + outcome.ActualDuration * 0.4);

        if (outcome.Type != ActionOutcomeType.Success)
        {
            return;
        }

        var actionId = outcome.ActionId.Value;

        // If tier is already provided in ResultData (from tests or external resolvers), use it
        if (outcome.ResultData != null && outcome.ResultData.ContainsKey("tier"))
        {
            // Tier already resolved, no action needed
        }
        // Sleep doesn't require a skill check
        else if (actionId == "sleep_under_tree")
        {
            // No skill check needed for sleep
        }
        // Resolve skill check for actions with DC parameters
        else
        {
            var tier = RollOutcomeTier.Success;
            
            if (actorState.CurrentAction != null)
            {
                var parameters = actorState.CurrentAction.Parameters;
                
                // Handle skill check actions (SkillCheckActionParameters or VignetteActionParameters)
                if (parameters is SkillCheckActionParameters skillCheckParams)
                {
                    var skillId = actionId.Contains("fish") ? "Fishing" :
                                 actionId.Contains("coconut") ? "Survival" :
                                 actionId.Contains("castle") ? "Performance" :
                                 actionId.Contains("swim") ? "Survival" :
                                 "Unknown";

                    var request = new SkillCheckRequest(skillCheckParams.DC, skillCheckParams.Modifier, skillCheckParams.Advantage, skillId);
                    var result = SkillCheckResolver.Resolve(rng, request);
                    tier = result.OutcomeTier;

                    if (outcome.ResultData == null)
                    {
                        outcome = outcome with { ResultData = new Dictionary<string, object>() };
                    }
                    outcome.ResultData["dc"] = skillCheckParams.DC;
                    outcome.ResultData["modifier"] = skillCheckParams.Modifier;
                    outcome.ResultData["advantage"] = skillCheckParams.Advantage.ToString();
                    outcome.ResultData["roll"] = result.Roll;
                    outcome.ResultData["total"] = result.Total;
                    outcome.ResultData["tier"] = tier.ToString();
                }
                else if (parameters is VignetteActionParameters vignetteParams)
                {
                    var skillId = actionId.Contains("plane") || actionId.Contains("mermaid") ? "Perception" : "Unknown";

                    var request = new SkillCheckRequest(vignetteParams.DC, vignetteParams.Modifier, vignetteParams.Advantage, skillId);
                    var result = SkillCheckResolver.Resolve(rng, request);
                    tier = result.OutcomeTier;

                    if (outcome.ResultData == null)
                    {
                        outcome = outcome with { ResultData = new Dictionary<string, object>() };
                    }
                    outcome.ResultData["dc"] = vignetteParams.DC;
                    outcome.ResultData["modifier"] = vignetteParams.Modifier;
                    outcome.ResultData["advantage"] = vignetteParams.Advantage.ToString();
                    outcome.ResultData["roll"] = result.Roll;
                    outcome.ResultData["total"] = result.Total;
                    outcome.ResultData["tier"] = tier.ToString();
                }
                else
                {
                    // No skill check, just mark as success
                    if (outcome.ResultData == null)
                    {
                        outcome = outcome with { ResultData = new Dictionary<string, object>() };
                    }
                    outcome.ResultData["tier"] = tier.ToString();
                }
            }
            else
            {
                if (outcome.ResultData == null)
                {
                    outcome = outcome with { ResultData = new Dictionary<string, object>() };
                }
                outcome.ResultData["tier"] = tier.ToString();
            }
        }

        // NEW: Use dictionary lookup instead of if/else chain
        if (_effectHandlers.TryGetValue(actionId, out var handler))
        {
            var effectCtx = new EffectContext
            {
                ActorId = actorId,
                Outcome = outcome,
                Actor = islandState,
                World = islandWorld,
                Tier = GetTierFromOutcome(outcome)
            };
            
            handler.ApplyEffects(effectCtx);
        }
        // Handle special cases that don't have providers (write_name_sand, clap_emote)
        else if (actionId == "write_name_sand" || actionId == "clap_emote")
        {
            // Dequeue the completed chat action intent
            if (islandState.PendingChatActions.Count > 0)
            {
                islandState.PendingChatActions.Dequeue();
            }
            
            // Apply effects for chat-triggered actions
            if (actionId == "write_name_sand")
            {
                islandState.Morale = Math.Min(100.0, islandState.Morale + 10.0);
                islandState.Boredom = Math.Max(0.0, islandState.Boredom - 15.0);
            }
            else if (actionId == "clap_emote")
            {
                islandState.Morale = Math.Min(100.0, islandState.Morale + 5.0);
            }
        }
    }

    private RollOutcomeTier? GetTierFromOutcome(ActionOutcome outcome)
    {
        if (outcome.ResultData?.TryGetValue("tier", out var tierObj) == true)
        {
            // Try direct cast first (if already enum), fallback to parse (for string values)
            if (tierObj is RollOutcomeTier tier)
                return tier;
            
            if (tierObj is string tierStr && Enum.TryParse<RollOutcomeTier>(tierStr, out var parsedTier))
                return parsedTier;
        }
        return null;
    }

    public List<SceneTemplate> GetSceneTemplates()
    {
        return new List<SceneTemplate>();
    }

    public bool ValidateContent(out List<string> errors)
    {
        errors = new List<string>();
        return true;
    }

    public void OnSignal(Signal signal, ActorState? targetActor, WorldState worldState, double currentTime)
    {
        if (targetActor == null)
        {
            return;
        }

        var islandState = targetActor as IslandActorState;
        if (islandState == null)
        {
            return;
        }

        var islandWorld = worldState as IslandWorldState;

        switch (signal.Type)
        {
            case "chat_redeem":
                HandleChatRedeem(signal, islandState, currentTime);
                break;
            case "sub":
            case "cheer":
                HandleSubOrCheer(signal, islandState, currentTime);
                break;
        }
    }

    private void HandleChatRedeem(Signal signal, IslandActorState state, double currentTime)
    {
        if (signal.Data.TryGetValue("redeem_name", out var redeemName))
        {
            var redeemStr = redeemName.ToString();
            
            if (redeemStr == "write_name_sand")
            {
                // Enqueue intent to write name in sand
                state.PendingChatActions.Enqueue(new PendingIntent
                {
                    ActionId = "write_name_sand",
                    Type = "chat_redeem",
                    Data = new Dictionary<string, object>(signal.Data),
                    EnqueuedAt = currentTime
                });
            }
        }
    }

    private void HandleSubOrCheer(Signal signal, IslandActorState state, double currentTime)
    {
        // Add Inspiration buff for subs/cheers (applies to all skills as a general morale boost)
        state.ActiveBuffs.Add(new ActiveBuff
        {
            Name = "Inspiration",
            Type = BuffType.SkillBonus,
            SkillId = "", // Empty means applies to all skills
            Value = 1,
            ExpiresAt = currentTime + 300.0 // 5 minutes
        });

        // Enqueue clap emote intent
        state.PendingChatActions.Enqueue(new PendingIntent
        {
            ActionId = "clap_emote",
            Type = signal.Type,
            Data = new Dictionary<string, object>(signal.Data),
            EnqueuedAt = currentTime
        });
    }

    public Dictionary<string, object> GetActorStateSnapshot(ActorState actorState)
    {
        var islandState = (IslandActorState)actorState;
        var snapshot = new Dictionary<string, object>
        {
            ["hunger"] = islandState.Hunger,
            ["energy"] = islandState.Energy,
            ["morale"] = islandState.Morale,
            ["boredom"] = islandState.Boredom
        };
        
        if (islandState.ActiveBuffs.Count > 0)
        {
            snapshot["active_buffs"] = string.Join(", ", 
                islandState.ActiveBuffs.Select(b => $"{b.Name}({b.ExpiresAt:F1})"));
        }
        
        return snapshot;
    }
}
