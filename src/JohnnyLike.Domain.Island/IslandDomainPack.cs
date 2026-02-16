using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Kit.Dice;
using JohnnyLike.Domain.Island.Candidates;
using System.Reflection;

namespace JohnnyLike.Domain.Island;

public class IslandDomainPack : IDomainPack
{
    private readonly List<IIslandCandidateProvider> _providers;
    private static List<IIslandCandidateProvider>? _cachedProviders;
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
                    _cachedProviders = DiscoverProviders();
                }
            }
        }
        
        _providers = _cachedProviders;
    }

    private static List<IIslandCandidateProvider> DiscoverProviders()
    {
        // Discover and instantiate providers ONLY by attribute
        return Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.GetCustomAttribute<IslandCandidateProviderAttribute>() != null)
            .Where(t => !t.IsAbstract && typeof(IIslandCandidateProvider).IsAssignableFrom(t))
            .Select(t => new {
                Type = t,
                Attr = t.GetCustomAttribute<IslandCandidateProviderAttribute>()!
            })
            .OrderBy(x => x.Attr.Order)
            .Select(x => (IIslandCandidateProvider)Activator.CreateInstance(x.Type)!)
            .ToList();
    }

    public WorldState CreateInitialWorldState()
    {
        return new IslandWorldState();
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

        if (actionId == "fish_for_food")
        {
            ApplyFishingEffects(actorId, outcome, islandState, islandWorld);
        }
        else if (actionId == "shake_tree_coconut")
        {
            ApplyCoconutEffects(actorId, outcome, islandState, islandWorld);
        }
        else if (actionId == "build_sand_castle")
        {
            ApplySandCastleEffects(actorId, outcome, islandState, islandWorld);
        }
        else if (actionId == "swim")
        {
            ApplySwimEffects(actorId, outcome, islandState, islandWorld);
        }
        else if (actionId == "sleep_under_tree")
        {
            ApplySleepEffects(actorId, outcome, islandState, islandWorld);
        }
        else if (actionId == "plane_sighting")
        {
            ApplyPlaneSightingEffects(actorId, outcome, islandState, islandWorld);
        }
        else if (actionId == "mermaid_encounter")
        {
            ApplyMermaidEncounterEffects(actorId, outcome, islandState, islandWorld);
        }
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

    private void ApplyFishingEffects(ActorId actorId, ActionOutcome outcome, IslandActorState state, IslandWorldState world)
    {
        if (outcome.ResultData == null || !outcome.ResultData.TryGetValue("tier", out var tierObj))
            return;

        var tier = Enum.Parse<RollOutcomeTier>(tierObj.ToString()!);

        switch (tier)
        {
            case RollOutcomeTier.CriticalSuccess:
                state.Hunger = Math.Max(0.0, state.Hunger - 50.0);
                world.FishAvailable = Math.Max(0.0, world.FishAvailable - 30.0);
                state.Morale = Math.Min(100.0, state.Morale + 15.0);
                break;

            case RollOutcomeTier.Success:
                state.Hunger = Math.Max(0.0, state.Hunger - 30.0);
                world.FishAvailable = Math.Max(0.0, world.FishAvailable - 15.0);
                state.Morale = Math.Min(100.0, state.Morale + 5.0);
                break;

            case RollOutcomeTier.PartialSuccess:
                state.Morale = Math.Min(100.0, state.Morale + 5.0);
                break;

            case RollOutcomeTier.Failure:
                break;

            case RollOutcomeTier.CriticalFailure:
                state.Morale = Math.Max(0.0, state.Morale - 10.0);
                break;
        }

        state.Boredom = Math.Max(0.0, state.Boredom - 10.0);
    }

    private void ApplyCoconutEffects(ActorId actorId, ActionOutcome outcome, IslandActorState state, IslandWorldState world)
    {
        if (outcome.ResultData == null || !outcome.ResultData.TryGetValue("tier", out var tierObj))
            return;

        var tier = Enum.Parse<RollOutcomeTier>(tierObj.ToString()!);

        switch (tier)
        {
            case RollOutcomeTier.CriticalSuccess:
                state.Hunger = Math.Max(0.0, state.Hunger - 25.0);
                world.CoconutsAvailable = Math.Max(0, world.CoconutsAvailable - 1);
                state.Energy = Math.Min(100.0, state.Energy + 15.0);
                break;

            case RollOutcomeTier.Success:
                state.Hunger = Math.Max(0.0, state.Hunger - 15.0);
                world.CoconutsAvailable = Math.Max(0, world.CoconutsAvailable - 1);
                state.Energy = Math.Min(100.0, state.Energy + 10.0);
                break;

            case RollOutcomeTier.PartialSuccess:
                state.Morale = Math.Min(100.0, state.Morale + 2.0);
                break;

            case RollOutcomeTier.Failure:
                break;

            case RollOutcomeTier.CriticalFailure:
                state.Morale = Math.Max(0.0, state.Morale - 5.0);
                break;
        }
    }

    private void ApplySandCastleEffects(ActorId actorId, ActionOutcome outcome, IslandActorState state, IslandWorldState world)
    {
        if (outcome.ResultData == null || !outcome.ResultData.TryGetValue("tier", out var tierObj))
            return;

        var tier = Enum.Parse<RollOutcomeTier>(tierObj.ToString()!);

        switch (tier)
        {
            case RollOutcomeTier.CriticalSuccess:
                state.Morale = Math.Min(100.0, state.Morale + 25.0);
                state.Boredom = Math.Max(0.0, state.Boredom - 30.0);
                break;

            case RollOutcomeTier.Success:
                state.Morale = Math.Min(100.0, state.Morale + 15.0);
                state.Boredom = Math.Max(0.0, state.Boredom - 20.0);
                break;

            case RollOutcomeTier.PartialSuccess:
                state.Morale = Math.Min(100.0, state.Morale + 5.0);
                state.Boredom = Math.Max(0.0, state.Boredom - 10.0);
                break;

            case RollOutcomeTier.Failure:
                state.Boredom = Math.Max(0.0, state.Boredom - 5.0);
                break;

            case RollOutcomeTier.CriticalFailure:
                state.Morale = Math.Max(0.0, state.Morale - 5.0);
                break;
        }
    }

    private void ApplySwimEffects(ActorId actorId, ActionOutcome outcome, IslandActorState state, IslandWorldState world)
    {
        if (outcome.ResultData == null || !outcome.ResultData.TryGetValue("tier", out var tierObj))
            return;

        var tier = Enum.Parse<RollOutcomeTier>(tierObj.ToString()!);

        switch (tier)
        {
            case RollOutcomeTier.CriticalSuccess:
                state.Morale = Math.Min(100.0, state.Morale + 20.0);
                state.Energy = Math.Max(0.0, state.Energy - 5.0);
                state.Boredom = Math.Max(0.0, state.Boredom - 15.0);
                break;

            case RollOutcomeTier.Success:
                state.Morale = Math.Min(100.0, state.Morale + 10.0);
                state.Energy = Math.Max(0.0, state.Energy - 10.0);
                state.Boredom = Math.Max(0.0, state.Boredom - 10.0);
                break;

            case RollOutcomeTier.PartialSuccess:
                state.Morale = Math.Min(100.0, state.Morale + 3.0);
                state.Energy = Math.Max(0.0, state.Energy - 15.0);
                break;

            case RollOutcomeTier.Failure:
                state.Energy = Math.Max(0.0, state.Energy - 15.0);
                break;

            case RollOutcomeTier.CriticalFailure:
                state.Energy = Math.Max(0.0, state.Energy - 25.0);
                state.Morale = Math.Max(0.0, state.Morale - 10.0);
                break;
        }
    }

    private void ApplySleepEffects(ActorId actorId, ActionOutcome outcome, IslandActorState state, IslandWorldState world)
    {
        state.Energy = Math.Min(100.0, state.Energy + 40.0);
        state.Boredom = Math.Max(0.0, state.Boredom - 5.0);
    }

    private void ApplyPlaneSightingEffects(ActorId actorId, ActionOutcome outcome, IslandActorState state, IslandWorldState world)
    {
        state.LastPlaneSightingTime = world.CurrentTime;

        if (outcome.ResultData == null || !outcome.ResultData.TryGetValue("tier", out var tierObj))
            return;

        var tier = Enum.Parse<RollOutcomeTier>(tierObj.ToString()!);

        if (tier >= RollOutcomeTier.Success)
        {
            state.Morale = Math.Min(100.0, state.Morale + 30.0);
        }

        if (tier == RollOutcomeTier.CriticalSuccess)
        {
            state.ActiveBuffs.Add(new ActiveBuff
            {
                Name = "Luck",
                Type = BuffType.SkillBonus,
                SkillId = "",
                Value = 2,
                ExpiresAt = world.CurrentTime + 300.0
            });
        }
    }

    private void ApplyMermaidEncounterEffects(ActorId actorId, ActionOutcome outcome, IslandActorState state, IslandWorldState world)
    {
        state.LastMermaidEncounterTime = world.CurrentTime;

        if (outcome.ResultData == null || !outcome.ResultData.TryGetValue("tier", out var tierObj))
            return;

        var tier = Enum.Parse<RollOutcomeTier>(tierObj.ToString()!);

        if (tier >= RollOutcomeTier.Success)
        {
            state.Morale = Math.Min(100.0, state.Morale + 40.0);
        }

        if (tier == RollOutcomeTier.CriticalSuccess)
        {
            state.ActiveBuffs.Add(new ActiveBuff
            {
                Name = "Mermaid's Blessing",
                Type = BuffType.Advantage,
                SkillId = "Fishing",
                Value = 0,
                ExpiresAt = world.CurrentTime + 600.0
            });
        }
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
}
