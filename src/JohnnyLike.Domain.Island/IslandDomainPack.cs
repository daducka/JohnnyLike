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

        world.AddWorldItem(new CalendarItem("calendar"), "beach");
        world.AddWorldItem(new WeatherItem("weather"), "beach");
        world.AddWorldItem(new BeachItem("beach"), "beach");
        world.AddWorldItem(new OceanItem("ocean"), "beach");
        world.AddWorldItem(new CoconutTreeItem("palm_tree"), "beach");
        world.AddWorldItem(new ShelterItem("main_shelter"), "beach");
        world.AddWorldItem(new StalactiteItem("stalactite"), "cave");

        var supplies = new SupplyPile("shared_supplies", "shared");
        supplies.AddSupply(20.0, () => new WoodSupply());
        world.AddWorldItem(supplies, "beach");

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
        var fishingPole = new FishingPoleItem($"fishing_pole_{actorId.Value}", actorId);
        world.AddWorldItem(fishingPole, "beach");
    }

    public List<ActionCandidate> GenerateCandidates(
        ActorId actorId,
        ActorState actorState,
        WorldState worldState,
        long currentTick,
        Random rng,
        IResourceAvailability resourceAvailability)
    {
        var islandActorState = (IslandActorState)actorState;
        var islandWorld = (IslandWorldState)worldState;
        var rngStream = new RandomRngStream(rng);

        // Note: World state time advancement is now handled by TickWorldState in Engine.AdvanceTime
        // No need to call OnTimeAdvanced here

        islandActorState.ActiveBuffs.RemoveAll(b => b.ExpiresAtTick <= currentTick);

        // Create context for providers
        var ctx = new IslandContext(
            actorId,
            islandActorState,
            islandWorld,
            currentTick,
            rngStream,
            rng,
            resourceAvailability
        );

        // Generate candidates using all registered providers
        var candidates = new List<ActionCandidate>();

        // Iterate items in stable (sorted-by-Id) order for determinism.
        // Tag each candidate with the ProviderItemId for engine-level room filtering and tie-breaking.
        foreach (var item in islandWorld.WorldItems
            .OfType<IIslandActionCandidate>()
            .Cast<WorldItem>()
            .OrderBy(wi => wi.Id)
            .Cast<IIslandActionCandidate>())
        {
            var wi = (WorldItem)item;
            var itemCandidates = new List<ActionCandidate>();
            item.AddCandidates(ctx, itemCandidates);
            foreach (var c in itemCandidates)
                candidates.Add(c with { ProviderItemId = wi.Id });
        }
        
        // Generate candidates from the actor itself (e.g., idle action).
        // Actor-self candidates use the actor's own Id as ProviderItemId.
        // The room filter in the Director treats actor-self candidates (not in the world item list)
        // as always room-agnostic, so they are never filtered out regardless of CurrentRoomId.
        var actorCandidates = new List<ActionCandidate>();
        islandActorState.AddCandidates(ctx, actorCandidates);
        foreach (var c in actorCandidates)
            candidates.Add(c with { ProviderItemId = actorId.Value });

        // Post-pass: compute final Score from IntrinsicScore and Quality weights
        var model = BuildQualityModel(ctx.Actor);
        for (var i = 0; i < candidates.Count; i++)
        {
            candidates[i] = candidates[i] with { Score = ScoreCandidate(candidates[i], model) };
        }

        return candidates;
    }

    /// <summary>
    /// Encapsulates the three scoring influences — Needs, Personality, Mood — as separate
    /// dictionaries so each can be tuned independently.
    /// Effective weight = needAdd[q] + personalityBase[q] * moodMultiplier[q]
    /// </summary>
    /// <param name="NeedAdd">Additive urgency from current deficits (e.g., satiety/energy). Independent of personality.</param>
    /// <param name="PersonalityBase">Stable baseline tendency derived from core stats (e.g., WIS/INT proactive trait).</param>
    /// <param name="MoodMultiplier">Multiplicative modulation from current affect state — suppresses or amplifies personality tendencies.</param>
    private sealed record QualityModel(
        Dictionary<QualityType, double> NeedAdd,
        Dictionary<QualityType, double> PersonalityBase,
        Dictionary<QualityType, double> MoodMultiplier)
    {
        /// <summary>
        /// Computes the effective weight for a quality dimension.
        /// Missing keys default to 0.0, meaning no contribution from that influence.
        /// </summary>
        public double EffectiveWeight(QualityType q)
        {
            NeedAdd.TryGetValue(q, out var need);
            PersonalityBase.TryGetValue(q, out var personality);
            MoodMultiplier.TryGetValue(q, out var mood);
            return need + personality * mood;
        }
    }

    internal static double ScoreByQualities(
        IslandActorState actor,
        double intrinsicScore,
        IReadOnlyDictionary<QualityType, double>? qualities)
    {
        if (qualities == null || qualities.Count == 0)
            return intrinsicScore;

        var model = BuildQualityModel(actor);
        var qualitySum = 0.0;
        foreach (var (quality, value) in qualities)
            qualitySum += model.EffectiveWeight(quality) * value;

        return intrinsicScore + qualitySum;
    }

    private static QualityModel BuildQualityModel(IslandActorState actor)
    {

        // ── Core Abilities ────────────────────────────────────────────────────────
        // Stable actor capabilities: STR, DEX, CON, INT, WIS, CHA
        // Raw values are normalised so that 10 = 0.0 and 20 = 1.0.

        double Norm(int a, int b) => Math.Clamp(((double)(a + b) - 20.0) / 20.0, 0.0, 1.0);

        // ── Actor Stats ───────────────────────────────────────────────────────────
        // Dynamic physiological / psychological state.
        // Each deficit becomes a "pressure" that adds weight to a need quality.

        var hungerPressure  = 100.0 - actor.Satiety;   // → FoodConsumption
        var fatiguePressure = 100.0 - actor.Energy;    // → Rest
        var miseryPressure  = 100.0 - actor.Morale;    // → Comfort
        var injuryPressure  = 100.0 - actor.Health;    // → Safety

        // ── Traits ────────────────────────────────────────────────────────────────
        // Stable personality tendencies derived from pairs of core abilities.
        // Every core ability participates in exactly two traits.

        var planner     = Norm(actor.INT, actor.WIS);   // INT + WIS  → prefers preparation, efficiency
        var craftsman   = Norm(actor.DEX, actor.INT);   // DEX + INT  → prefers crafting, mastery
        var survivor    = Norm(actor.CON, actor.WIS);   // CON + WIS  → prefers safety, sustainability
        var hedonist    = Norm(actor.CHA, actor.CON);   // CHA + CON  → prefers comfort, morale
        var instinctive = Norm(actor.STR, actor.CHA);   // STR + CHA  → prefers immediate reward
        var industrious = Norm(actor.STR, actor.DEX);   // STR + DEX  → prefers building, working

        // ── Qualities ─────────────────────────────────────────────────────────────
        // Labels on actions describing what they provide:
        // FoodConsumption, Rest, Comfort, Fun, Safety,
        // Preparation, Efficiency, Mastery, ResourcePreservation

        // Need pressures drive urgency directly (additive, independent of personality).
        // Scale factors keep pressures comparable across the 0-100 range.
        var needAdd = new Dictionary<QualityType, double>
        {
            [QualityType.FoodConsumption] = hungerPressure  * 0.02,
            [QualityType.Rest]            = fatiguePressure * 0.015,
            [QualityType.Comfort]         = miseryPressure  * 0.01,
            [QualityType.Safety]          = injuryPressure  * 0.01
        };

        // Personality weights come from traits (stable, independent of current state).
        var personalityBase = new Dictionary<QualityType, double>
        {
            [QualityType.Preparation]     = (planner + industrious) * 0.4,
            [QualityType.Efficiency]      = (planner + craftsman)   * 0.3,
            [QualityType.Mastery]         = (craftsman + industrious) * 0.3,
            [QualityType.Comfort]         = hedonist * 0.4,
            [QualityType.Safety]          = survivor * 0.3,
            [QualityType.FoodConsumption] = (instinctive + hedonist) * 0.2,
            [QualityType.Fun]             = 1.0
        };

        // Mood multipliers modulate personality when actor state is critical.
        // They suppress longer-horizon tendencies so survival instinct takes over.
        var moodMultiplier = new Dictionary<QualityType, double>
        {
            [QualityType.Preparation]     = Math.Min(
                                                actor.Satiety < 20.0 ? 0.3 : 1.0,  // starving → eat now, not cook
                                                actor.Health  < 30.0 ? 0.5 : 1.0   // injured → heal, not cook
                                            ),
            [QualityType.Mastery]         = actor.Energy  < 20.0 ? 0.4 : 1.0, // exhausted → rest, not work
            [QualityType.Fun]             = 1.0 - (actor.Morale / 100.0),  // amplified when morale is low
            [QualityType.Efficiency]      = 1.0,
            [QualityType.Comfort]         = 1.0,
            [QualityType.Safety]          = 1.0,
            [QualityType.FoodConsumption] = 1.0
        };

        return new QualityModel(needAdd, personalityBase, moodMultiplier);
    }

    private static double ScoreCandidate(ActionCandidate candidate, QualityModel model)
    {
        if (candidate.Qualities.Count == 0)
            return candidate.IntrinsicScore;

        var qualitySum = 0.0;
        foreach (var (quality, value) in candidate.Qualities)
            qualitySum += model.EffectiveWeight(quality) * value;
        return candidate.IntrinsicScore + qualitySum;
    }

    /// <summary>
    /// Executes the candidate's PreAction at the moment the action is chosen (before the action duration starts).
    /// Casts the handler to <see cref="Func{EffectContext, bool}"/> and invokes it.
    /// Returns false if the supply is missing so the Director can try the next-best candidate.
    /// </summary>
    public bool TryExecutePreAction(
        ActorId actorId,
        ActorState actorState,
        WorldState worldState,
        IRngStream rng,
        IResourceAvailability resourceAvailability,
        object? preActionHandler)
    {
        if (preActionHandler is not Func<EffectContext, bool> preAction)
            return true; // Nothing to execute — always succeeds

        var effectCtx = new EffectContext
        {
            ActorId = actorId,
            // Use a placeholder outcome: PreAction only reads world/actor state, not outcome data
            Outcome = new ActionOutcome(new ActionId("preaction_placeholder"), ActionOutcomeType.Success, 0L),
            Actor = (IslandActorState)actorState,
            World = (IslandWorldState)worldState,
            Tier = null,
            Rng = rng,
            Reservations = resourceAvailability,
            Tracer = worldState.Tracer
        };

        return preAction(effectCtx);
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
        var dtSeconds = (double)outcome.ActualDurationTicks / (double)EngineConstants.TickHz;
        islandActorState.Satiety -= dtSeconds * 0.5;
        islandActorState.Energy -= dtSeconds * 0.3;
        islandActorState.Morale -= dtSeconds * 0.4;

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
            Reservations = resourceAvailability,
            Tracer = islandWorld.Tracer
        };

        // Effect handler should always be provided via ActionCandidate.EffectHandler
        if (effectHandler is Action<EffectContext> handler)
        {
            handler(effectCtx);
        }

        // Propagate outcome narration set by the effect handler into ResultData so the engine
        // can include it in the ActionCompleted trace event.
        if (effectCtx.OutcomeNarration != null && outcome.ResultData != null)
            outcome.ResultData["outcomeNarration"] = effectCtx.OutcomeNarration;
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

    public bool ValidateContent(out List<string> errors)
    {
        errors = new List<string>();
        return true;
    }

    public void OnSignal(Signal signal, ActorState? targetActor, WorldState worldState, long currentTick)
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
                HandleChatRedeem(signal, islandActorState, currentTick);
                break;
            case "sub":
            case "cheer":
                HandleSubOrCheer(signal, islandActorState, currentTick);
                break;
        }
    }

    private void HandleChatRedeem(Signal signal, IslandActorState state, long currentTick)
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
                    EnqueuedAtTick = currentTick
                });
            }
        }
    }

    private void HandleSubOrCheer(Signal signal, IslandActorState state, long currentTick)
    {
        // Add Inspiration buff for subs/cheers (applies to all skills as a general morale boost)
        state.ActiveBuffs.Add(new ActiveBuff
        {
            Name = "Inspiration",
            Type = BuffType.SkillBonus,
            SkillType = null, // null means applies to all skills
            Value = 1,
            ExpiresAtTick = currentTick + 300L * EngineConstants.TickHz // 5 minutes
        });

        // Enqueue clap emote intent
        state.PendingChatActions.Enqueue(new PendingIntent
        {
            ActionId = "clap_emote",
            Type = signal.Type,
            Data = new Dictionary<string, object>(signal.Data),
            EnqueuedAtTick = currentTick
        });
    }

    public Dictionary<string, object> GetActorStateSnapshot(ActorState actorState)
    {
        var islandActorState = (IslandActorState)actorState;
        var snapshot = new Dictionary<string, object>
        {
            ["satiety"] = FormatStat(islandActorState.Satiety, "satiety"),
            ["energy"]  = FormatStat(islandActorState.Energy,  "energy"),
            ["morale"]  = FormatStat(islandActorState.Morale,  "morale"),
            ["health"]  = FormatStat(islandActorState.Health,  "health")
        };
        
        if (islandActorState.ActiveBuffs.Count > 0)
        {
            snapshot["active_buffs"] = string.Join(", ", 
                islandActorState.ActiveBuffs.Select(b => $"{b.Name}({b.ExpiresAtTick})"));
        }
        
        return snapshot;
    }

    /// <summary>
    /// Maps a raw numeric stat value to a qualitative descriptor so that prompts
    /// describe the actor's state in narrative terms rather than raw numbers.
    /// Thresholds (80/50/20) divide the 0–100 range into four roughly equal bands:
    /// excellent (&gt;=80), good (50–79), poor (20–49), critical (&lt;20).
    /// </summary>
    private static string FormatStat(double value, string statName) => statName switch
    {
        "satiety" => value >= 80 ? "full"      : value >= 50 ? "satisfied" : value >= 20 ? "hungry"   : "starving",
        "energy"  => value >= 80 ? "energetic" : value >= 50 ? "alert"     : value >= 20 ? "tired"    : "exhausted",
        "morale"  => value >= 80 ? "cheerful"  : value >= 50 ? "content"   : value >= 20 ? "down"     : "miserable",
        "health"  => value >= 80 ? "healthy"   : value >= 50 ? "wounded"   : value >= 20 ? "injured"  : "critical",
        _         => value.ToString("F0")
    };

    public List<TraceEvent> TickWorldState(WorldState worldState, long currentTick, IResourceAvailability resourceAvailability)
    {
        var islandWorld = (IslandWorldState)worldState;
        return islandWorld.OnTickAdvanced(currentTick, resourceAvailability);
    }
}
