using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Kit.Dice;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Island.Supply;
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
        
        // Initialize shared supply pile with some starting wood
        var sharedSupplies = new SupplyPile("shared_supplies", "shared");
        sharedSupplies.AddSupply("wood", 20.0, id => new WoodSupply(id));
        world.WorldItems.Add(sharedSupplies);
        
        // Initialize WorldStats
        world.WorldStats.Add(new Stats.TimeOfDayStat());
        world.WorldStats.Add(new Stats.WeatherStat());
        world.WorldStats.Add(new Stats.TideStat());
        world.WorldStats.Add(new Stats.FishPopulationStat());
        world.WorldStats.Add(new Stats.CoconutAvailabilityStat());
        world.WorldStats.Add(new Stats.DriftwoodAvailabilityStat());
        
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
    
    /// <summary>
    /// Initialize actor-specific items in the world (e.g., exclusive tools like fishing poles).
    /// This should be called after an actor is added to the engine.
    /// </summary>
    public void InitializeActorItems(ActorId actorId, IslandWorldState world)
    {
        // Create a fishing pole for this actor
        var fishingPole = new FishingPoleItem($"fishing_pole_{actorId.Value}", actorId);
        world.WorldItems.Add(fishingPole);
    }

    public List<ActionCandidate> GenerateCandidates(
        ActorId actorId,
        ActorState actorState,
        WorldState worldState,
        double currentTime,
        Random rng,
        IResourceAvailability resourceAvailability)
    {
        var islandState = (IslandActorState)actorState;
        var islandWorld = (IslandWorldState)worldState;
        var rngStream = new RandomRngStream(rng);

        // Note: World state time advancement is now handled by TickWorldState in Engine.AdvanceTime
        // No need to call OnTimeAdvanced here

        islandState.ActiveBuffs.RemoveAll(b => b.ExpiresAt <= currentTime);

        // Create context for providers
        var ctx = new IslandContext(
            actorId,
            islandState,
            islandWorld,
            currentTime,
            rngStream,
            rng,
            resourceAvailability
        );

        // Generate candidates using all registered providers
        var candidates = new List<ActionCandidate>();
        foreach (var provider in _providers)
        {
            provider.AddCandidates(ctx, candidates);
        }
        
        // Generate candidates from ToolItems in the world
        foreach (var item in islandWorld.WorldItems.OfType<ToolItem>())
        {
            item.AddCandidates(ctx, candidates);
        }

        return candidates;
    }

    public void ApplyActionEffects(
        ActorId actorId,
        ActionOutcome outcome,
        ActorState actorState,
        WorldState worldState,
        IRngStream rng,
        IResourceAvailability resourceAvailability)
    {
        var islandState = (IslandActorState)actorState;
        var islandWorld = (IslandWorldState)worldState;

        // Note: World state time advancement is now handled by TickWorldState in Engine.AdvanceTime
        // This method only applies action-specific effects and actor passive decay

        // Apply passive actor decay based on action duration
        islandState.Hunger = Math.Min(100.0, islandState.Hunger + outcome.ActualDuration * 0.5);
        islandState.Energy = Math.Max(0.0, islandState.Energy - outcome.ActualDuration * 0.3);
        islandState.Boredom = Math.Min(100.0, islandState.Boredom + outcome.ActualDuration * 0.4);

        if (outcome.Type != ActionOutcomeType.Success)
        {
            return;
        }

        var actionId = outcome.ActionId.Value;

        var effectCtx = new EffectContext
        {
            ActorId = actorId,
            Outcome = outcome,
            Actor = islandState,
            World = islandWorld,
            Tier = GetTierFromOutcome(outcome),
            Rng = rng,
            Reservations = resourceAvailability
        };

        // Try to apply effects via ToolItem (they check actionId internally)
        foreach (var item in islandWorld.WorldItems.OfType<ToolItem>())
        {
            item.ApplyEffects(effectCtx);
        }

        // Also call legacy provider-based effect handlers (for non-tool actions)
        if (_effectHandlers.TryGetValue(actionId, out var handler))
        {
            handler.ApplyEffects(effectCtx);
        }
    }

    private RollOutcomeTier? GetTierFromOutcome(ActionOutcome outcome)
    {
        if (outcome.ResultData?.TryGetValue("tier", out var tierObj) == true)
        {
            // Check if already enum, fallback to parse for string values
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
            SkillType = null, // null means applies to all skills
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

    public List<TraceEvent> TickWorldState(WorldState worldState, double dtSeconds, IResourceAvailability resourceAvailability)
    {
        var islandWorld = (IslandWorldState)worldState;
        var newCurrentTime = islandWorld.CurrentTime + dtSeconds;
        
        // OnTimeAdvanced now returns all trace events (from stats and items)
        var traceEvents = islandWorld.OnTimeAdvanced(newCurrentTime, dtSeconds, resourceAvailability);
        
        return traceEvents;
    }
}
