using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Kit.Dice;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Island.Supply;

namespace JohnnyLike.Domain.Island;

public class IslandDomainPack : IDomainPack
{
    public string DomainName => "Island";

    public IslandDomainPack()
    {
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
        
        // Add resource items
        world.WorldItems.Add(new CoconutTreeItem("palm_tree"));
        world.WorldItems.Add(new DriftwoodPileItem("driftwood_pile"));
        
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
            Satiety = (double)(initialData?.GetValueOrDefault("satiety", 100.0) ?? 100.0),
            Energy = (double)(initialData?.GetValueOrDefault("energy", 100.0) ?? 100.0),
            Morale = (double)(initialData?.GetValueOrDefault("morale", 50.0) ?? 50.0)
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
        var islandActorState = (IslandActorState)actorState;
        var islandWorld = (IslandWorldState)worldState;
        var rngStream = new RandomRngStream(rng);

        // Note: World state time advancement is now handled by TickWorldState in Engine.AdvanceTime
        // No need to call OnTimeAdvanced here

        islandActorState.ActiveBuffs.RemoveAll(b => b.ExpiresAt <= currentTime);

        // Create context for providers
        var ctx = new IslandContext(
            actorId,
            islandActorState,
            islandWorld,
            currentTime,
            rngStream,
            rng,
            resourceAvailability
        );

        // Generate candidates using all registered providers
        var candidates = new List<ActionCandidate>();
        
        // Generate candidates from all items implementing IIslandActionCandidate
        foreach (var item in islandWorld.WorldItems.OfType<IIslandActionCandidate>())
        {
            item.AddCandidates(ctx, candidates);
        }
        
        // Generate candidates from the actor itself (e.g., idle action)
        islandActorState.AddCandidates(ctx, candidates);

        // Post-pass: compute final Score from IntrinsicScore and Quality weights
        var weights = BuildQualityWeights(ctx);
        for (var i = 0; i < candidates.Count; i++)
        {
            candidates[i] = candidates[i] with { Score = ScoreCandidate(candidates[i], weights) };
        }

        return candidates;
    }

    private static Dictionary<QualityType, double> BuildQualityWeights(IslandContext ctx)
    {
        var actor = ctx.Actor;
        var satietyDeficit = (100.0 - actor.Satiety) / 100.0;
        var energyDeficit = (100.0 - actor.Energy) / 100.0;
        var moraleFactor = actor.Morale / 100.0;
        var proactiveTrait = Math.Clamp(((actor.WIS + actor.INT) - 20.0) / 20.0, 0.0, 1.0);

        return new Dictionary<QualityType, double>
        {
            [QualityType.Rest] = energyDeficit * 1.5,
            [QualityType.FoodConsumption] = satietyDeficit * 2.0,
            [QualityType.ForwardPlanning] = proactiveTrait * 0.8 * (0.3 + 0.7 * moraleFactor),
            [QualityType.Fun] = (1.0 - moraleFactor),
            [QualityType.Safety] = 0.3
        };
    }

    private static double ScoreCandidate(ActionCandidate candidate, Dictionary<QualityType, double> weights)
    {
        if (candidate.Qualities == null || candidate.Qualities.Count == 0)
            return candidate.IntrinsicScore;

        var qualitySum = 0.0;
        foreach (var (quality, value) in candidate.Qualities)
        {
            if (weights.TryGetValue(quality, out var weight))
                qualitySum += weight * value;
        }
        return candidate.IntrinsicScore + qualitySum;
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
        var islandActorState = (IslandActorState)actorState;
        var islandWorld = (IslandWorldState)worldState;

        // Note: World state time advancement is now handled by TickWorldState in Engine.AdvanceTime
        // This method only applies action-specific effects and actor passive decay

        // Apply passive actor decay based on action duration
        islandActorState.Satiety -= outcome.ActualDuration * 0.5;
        islandActorState.Energy -= outcome.ActualDuration * 0.3;
        islandActorState.Morale -= outcome.ActualDuration * 0.4;

        if (outcome.Type != ActionOutcomeType.Success)
        {
            return;
        }

        var actionId = outcome.ActionId.Value;

        var effectCtx = new EffectContext
        {
            ActorId = actorId,
            Outcome = outcome,
            Actor = islandActorState,
            World = islandWorld,
            Tier = GetTierFromOutcome(outcome),
            Rng = rng,
            Reservations = resourceAvailability
        };

        // Effect handler should always be provided via ActionCandidate.EffectHandler
        if (effectHandler is Action<EffectContext> handler)
        {
            handler(effectCtx);
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

        var islandActorState = targetActor as IslandActorState;
        if (islandActorState == null)
        {
            return;
        }

        var islandWorld = worldState as IslandWorldState;

        switch (signal.Type)
        {
            case "chat_redeem":
                HandleChatRedeem(signal, islandActorState, currentTime);
                break;
            case "sub":
            case "cheer":
                HandleSubOrCheer(signal, islandActorState, currentTime);
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
        var islandActorState = (IslandActorState)actorState;
        var snapshot = new Dictionary<string, object>
        {
            ["satiety"] = islandActorState.Satiety,
            ["energy"] = islandActorState.Energy,
            ["morale"] = islandActorState.Morale
        };
        
        if (islandActorState.ActiveBuffs.Count > 0)
        {
            snapshot["active_buffs"] = string.Join(", ", 
                islandActorState.ActiveBuffs.Select(b => $"{b.Name}({b.ExpiresAt:F1})"));
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
