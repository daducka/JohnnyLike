using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Metabolism;
using JohnnyLike.Domain.Island.Recipes;
using JohnnyLike.Domain.Island.Telemetry;
using JohnnyLike.Domain.Island.Vitality;
using JohnnyLike.Domain.Kit.Dice;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JohnnyLike.Domain.Island;

/// <summary>
/// Humanoid/person-specific actor state. Inherits the generic living-actor base
/// (<see cref="LivingActorState"/>) and adds humanoid-specific fields: decision
/// pragmatism, softmax tuning, chat/pending intents, and recipe knowledge.
/// </summary>
public class IslandActorState : LivingActorState, IIslandActionCandidate
{
    public long LastPlaneSightingTick { get; set; } = -1L;
    public long LastMermaidEncounterTick { get; set; } = -1L;

    /// <summary>
    /// Controls how often the actor exploits the best-scored action versus exploring
    /// via softmax sampling. Range [0,1]: 1.0 = fully pragmatic (best-first), 0.0 = fully spontaneous.
    /// </summary>
    public double DecisionPragmatism { get; set; } = 1.0;

    /// <summary>
    /// Softmax temperature used at maximum spontaneity (DecisionPragmatism = 0).
    /// Higher values produce a flatter probability distribution. Default: 20.0.
    /// </summary>
    public double SoftmaxTHigh { get; set; } = 20.0;

    /// <summary>
    /// Softmax temperature used at minimum spontaneity (DecisionPragmatism approaching 1).
    /// Only applies when the explore branch is taken (rng.NextDouble() &gt;= DecisionPragmatism).
    /// Lower values concentrate probability on higher-scored candidates. Default: 2.0.
    /// </summary>
    public double SoftmaxTLow { get; set; } = 2.0;

    public Queue<PendingIntent> PendingChatActions { get; set; } = new();
    /// <summary>
    /// IDs of recipes this actor knows. Each actor can have a different set of known recipes.
    /// </summary>
    public HashSet<string> KnownRecipeIds { get; set; } = new();

    public override string Serialize()
    {
        var options = new JsonSerializerOptions
        {
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
        };
        
        return JsonSerializer.Serialize(new
        {
            Id = Id.Value,
            Status,
            CurrentAction = CurrentAction?.Id.Value,
            LastDecisionTick,
            PresenceState,
            STR,
            DEX,
            CON,
            INT,
            WIS,
            CHA,
            Satiety,
            Energy,
            Morale,
            Health,
            LastPlaneSightingTick,
            LastMermaidEncounterTick,
            DecisionPragmatism,
            SoftmaxTLow,
            SoftmaxTHigh,
            ActiveBuffs,
            PendingChatActions = PendingChatActions.ToList(),
            KnownRecipeIds = KnownRecipeIds.ToList()
        }, options);
    }

    public override void Deserialize(string json)
    {
        var options = new JsonSerializerOptions
        {
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
        };
        
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, options);
        if (data == null) return;

        Id = new ActorId(data["Id"].GetString()!);
        
        // Status can be either a string (enum name) or number (enum value)
        if (data["Status"].ValueKind == JsonValueKind.String)
        {
            Status = Enum.Parse<ActorStatus>(data["Status"].GetString()!);
        }
        else
        {
            Status = (ActorStatus)data["Status"].GetInt32();
        }
        
        LastDecisionTick = data.TryGetValue("LastDecisionTick", out var ldt) ? ldt.GetInt64() : 0L;

        if (data.TryGetValue("PresenceState", out var presenceElement))
        {
            PresenceState = presenceElement.ValueKind == JsonValueKind.String
                ? Enum.Parse<PresenceState>(presenceElement.GetString()!)
                : (PresenceState)presenceElement.GetInt32();
        }

        STR = data["STR"].GetInt32();
        DEX = data["DEX"].GetInt32();
        CON = data["CON"].GetInt32();
        INT = data["INT"].GetInt32();
        WIS = data["WIS"].GetInt32();
        CHA = data["CHA"].GetInt32();
        Satiety = data["Satiety"].GetDouble();
        Energy = data["Energy"].GetDouble();
        Morale = data["Morale"].GetDouble();
        if (data.TryGetValue("Health", out var health))
            Health = health.GetDouble();

        if (data.TryGetValue("LastPlaneSightingTick", out var lastPlane))
            LastPlaneSightingTick = lastPlane.GetInt64();

        if (data.TryGetValue("LastMermaidEncounterTick", out var lastMermaid))
            LastMermaidEncounterTick = lastMermaid.GetInt64();

        if (data.TryGetValue("DecisionPragmatism", out var pragmatism))
            DecisionPragmatism = pragmatism.GetDouble();

        if (data.TryGetValue("SoftmaxTLow", out var tLow))
            SoftmaxTLow = tLow.GetDouble();

        if (data.TryGetValue("SoftmaxTHigh", out var tHigh))
            SoftmaxTHigh = tHigh.GetDouble();

        if (data.TryGetValue("ActiveBuffs", out var buffs))
        {
            ActiveBuffs = JsonSerializer.Deserialize<List<ActiveBuff>>(buffs.GetRawText(), options) ?? new();
        }

        if (data.TryGetValue("PendingChatActions", out var actions))
        {
            var list = JsonSerializer.Deserialize<List<PendingIntent>>(actions.GetRawText(), options) ?? new();
            PendingChatActions = new Queue<PendingIntent>(list);
        }

        if (data.TryGetValue("KnownRecipeIds", out var recipeIds))
        {
            var list = JsonSerializer.Deserialize<List<string>>(recipeIds.GetRawText(), options) ?? new();
            KnownRecipeIds = new HashSet<string>(list);
        }
    }

    /// <summary>
    /// Actors provide intrinsic action candidates: idle, known recipes, downed/dead state actions.
    /// Environment-derived candidates (swim, sleep_under_tree, build_sand_castle, think_about_supplies)
    /// are contributed by their respective world items via <see cref="IIslandActionCandidate"/>.
    /// Chat and expressive candidates are contributed by <see cref="Candidates.ChatCandidateProvider"/>.
    /// </summary>
    public void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        // Chat/emote candidates from the shared provider (viewer redeems, subs, cheers)
        Candidates.ChatCandidateProvider.AddCandidates(this, ctx, output);

        // Idle must ALWAYS be a candidate with a low baseline score
        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("idle"),
                ActionKind.Wait,
                EmptyActionParameters.Instance,
                Duration.Seconds(5.0), // Idle is a brief 5-second pause, not a sustained activity
                NarrationDescription: "wait and rest for a moment"
            ),
            0.12,
            Reason: "Idle",
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Rest]       = 0.6,
                [QualityType.Comfort]    = 0.2,
                [QualityType.Efficiency] = -0.5
            },
            ActorRequirement: CandidateRequirements.AliveOnly
        ));
        
        // Known recipes
        foreach (var recipeId in KnownRecipeIds)
        {
            if (!IslandRecipeRegistry.All.TryGetValue(recipeId, out var recipe))
                continue; // skip stale or removed recipe IDs

            RecipeCandidateBuilder.AddCandidate(recipe, ctx, output);
        }

        // Downed-state candidates (limited actions while fighting for survival)
        AddDownedCandidates(ctx, output);

        // Dead-state placeholder (actor lies still; allows future corpse-interaction candidates)
        AddDeadCandidates(ctx, output);
    }

    // ── Death save constants ───────────────────────────────────────────────────
    /// <summary>Random roll value at or above which a death save is a success.</summary>
    public const double DeathSaveSuccessThreshold = 0.6;
    /// <summary>Random roll value at or below which a death save is a failure.</summary>
    public const double DeathSaveFailureThreshold = 0.4;
    /// <summary>Number of saves/failures required to resolve the downed state.</summary>
    public const int DeathSaveResolutionCount = 3;
    /// <summary>Health the actor revives with after 3 successful death saves.</summary>
    public const double ReviveHealth = 10.0;

    private void AddDownedCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        var actorName = Id.Value;

        // ── death_save (highest priority — rolls a death save) ─────────────────
        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("death_save"),
                ActionKind.Wait,
                EmptyActionParameters.Instance,
                Duration.Minutes(1.0),
                NarrationDescription: "struggle to hold on"
            ),
            0.9,
            Reason: "Death save (downed)",
            EffectHandler: new Action<EffectContext>(effectCtx =>
            {
                var island    = effectCtx.Actor;
                var aliveness = island.TryGetBuff<AlivenessBuff>();
                if (aliveness == null || aliveness.State != AlivenessState.Downed)
                    return;

                // DnD-style death saving throw: DC 10 Constitution check.
                // nat-1 → critical failure (2 failures); nat-20 → instant stabilize; ≥DC → success; <DC → failure.
                var conModifier = island.GetSkillModifier(SkillType.Constitution);
                var conAdvantage = island.GetAdvantage(SkillType.Constitution);
                var saveRequest = new SkillCheckRequest(10, conModifier, conAdvantage, "Constitution");
                var saveResult  = SkillCheckResolver.Resolve(effectCtx.Rng, saveRequest);
                var saveTier    = saveResult.OutcomeTier;

                effectCtx.World.Tracer.Beat(
                    $"[DeathSave] {actorName} constitution check: rolled {saveResult.Total} (DC 10, {saveTier})",
                    actorId: actorName, priority: 50);

                if (saveTier == RollOutcomeTier.CriticalSuccess)
                {
                    // Nat-20 or margin ≥ 5: instant stabilize
                    MortalityWorkflow.Revive(
                        aliveness, island, ReviveHealth, actorName, effectCtx,
                        $"{actorName} gasps — eyes wide, chest heaving. Something in them refuses to let go.");
                }
                else if (saveTier == RollOutcomeTier.Success)
                {
                    aliveness.DeathSaveSuccesses++;
                    effectCtx.World.Tracer.Beat(
                        $"[DeathSave] {actorName} rolled success ({aliveness.DeathSaveSuccesses}/{DeathSaveResolutionCount})",
                        actorId: actorName, priority: 60);

                    if (aliveness.DeathSaveSuccesses >= DeathSaveResolutionCount)
                    {
                        MortalityWorkflow.Revive(
                            aliveness, island, ReviveHealth, actorName, effectCtx,
                            $"{actorName} gasps and drags themselves back from the brink.");
                    }
                }
                else if (saveTier == RollOutcomeTier.PartialSuccess)
                {
                    // Barely failing — no change, just hanging on
                    effectCtx.World.Tracer.Beat(
                        $"[DeathSave] {actorName} is barely holding on (no change)",
                        actorId: actorName, priority: 50);
                }
                else if (saveTier == RollOutcomeTier.Failure)
                {
                    aliveness.DeathSaveFailures++;
                    effectCtx.World.Tracer.Beat(
                        $"[DeathSave] {actorName} rolled failure ({aliveness.DeathSaveFailures}/{DeathSaveResolutionCount})",
                        actorId: actorName, priority: 60);

                    if (aliveness.DeathSaveFailures >= DeathSaveResolutionCount)
                    {
                        MortalityWorkflow.Die(
                            aliveness, island, actorName, effectCtx,
                            $"{actorName}'s eyes go glassy. Their chest stops moving. " +
                            $"The only sound left is the wind and the waves — indifferent, endless. " +
                            $"{actorName} is gone.");
                    }
                }
                else // CriticalFailure (nat-1) — counts as 2 failures in DnD
                {
                    aliveness.DeathSaveFailures += 2;
                    effectCtx.World.Tracer.Beat(
                        $"[DeathSave] {actorName} critically failed ({aliveness.DeathSaveFailures}/{DeathSaveResolutionCount})",
                        actorId: actorName, priority: 65);

                    if (aliveness.DeathSaveFailures >= DeathSaveResolutionCount)
                    {
                        MortalityWorkflow.Die(
                            aliveness, island, actorName, effectCtx,
                            $"{actorName}'s eyes go glassy. Their chest stops moving. " +
                            $"The only sound left is the wind and the waves — indifferent, endless. " +
                            $"{actorName} is gone.");
                    }
                }
            }),
            Qualities: new Dictionary<QualityType, double>(),
            ActorRequirement: CandidateRequirements.DownedOnly
        ));

        // ── whimper ────────────────────────────────────────────────────────────
        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("whimper"),
                ActionKind.Emote,
                EmptyActionParameters.Instance,
                Duration.Minutes(1.0),
                NarrationDescription: "whimper weakly"
            ),
            0.1,
            Reason: "Whimper (collapsed)",
            EffectHandler: new Action<EffectContext>(effectCtx =>
            {
                var name = effectCtx.ActorId.Value;
                var actor = effectCtx.Actor;
                var modifier = actor.GetSkillModifier(SkillType.Performance);
                var advantage = actor.GetAdvantage(SkillType.Performance);
                var result = SkillCheckResolver.Resolve(
                    effectCtx.Rng, new SkillCheckRequest(10, modifier, advantage, "Performance"));
                switch (result.OutcomeTier)
                {
                    case RollOutcomeTier.CriticalSuccess:
                    case RollOutcomeTier.Success:
                        effectCtx.SetOutcomeNarration(
                            $"{name} manages a raw, anguished cry — barely audible over the waves.");
                        break;
                    case RollOutcomeTier.PartialSuccess:
                        effectCtx.SetOutcomeNarration(
                            $"{name} whimpers softly, lips moving without words.");
                        break;
                    default:
                        effectCtx.SetOutcomeNarration(
                            $"{name} lets out a hollow moan, each breath shallow and rattling.");
                        break;
                }
            }),
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Safety] = 0.5
            },
            ActorRequirement: CandidateRequirements.DownedOnly
        ));

        // ── stare_blankly ──────────────────────────────────────────────────────
        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("stare_blankly"),
                ActionKind.Emote,
                EmptyActionParameters.Instance,
                Duration.Minutes(8.0),
                NarrationDescription: "stare blankly at the sky"
            ),
            0.1,
            Reason: "Stare blankly (collapsed)",
            EffectHandler: new Action<EffectContext>(effectCtx =>
            {
                var name = effectCtx.ActorId.Value;
                var actor = effectCtx.Actor;
                var modifier = actor.GetSkillModifier(SkillType.Perception);
                var advantage = actor.GetAdvantage(SkillType.Perception);
                var result = SkillCheckResolver.Resolve(
                    effectCtx.Rng, new SkillCheckRequest(12, modifier, advantage, "Perception"));
                switch (result.OutcomeTier)
                {
                    case RollOutcomeTier.CriticalSuccess:
                        effectCtx.SetOutcomeNarration(
                            $"{name} stares up at the clouds, eyes tracking a bird drifting overhead — " +
                            $"a small, distant sign of life.");
                        break;
                    case RollOutcomeTier.Success:
                        effectCtx.SetOutcomeNarration(
                            $"{name} stares blankly at the sky, expression unreadable, mind somewhere far away.");
                        break;
                    default:
                        effectCtx.SetOutcomeNarration(
                            $"{name}'s gaze is fixed on nothing. The sky reflects in eyes that barely seem to see it.");
                        break;
                }
            }),
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Safety] = 0.5
            },
            ActorRequirement: CandidateRequirements.DownedOnly
        ));

        // ── crawl_weakly ───────────────────────────────────────────────────────
        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("crawl_weakly"),
                ActionKind.Emote,
                EmptyActionParameters.Instance,
                Duration.Seconds(10.0),
                NarrationDescription: "crawl weakly along the ground"
            ),
            0.1,
            Reason: "Crawl weakly (collapsed)",
            EffectHandler: new Action<EffectContext>(effectCtx =>
            {
                var name = effectCtx.ActorId.Value;
                var actor = effectCtx.Actor;
                var modifier = actor.GetSkillModifier(SkillType.Athletics);
                var advantage = actor.GetAdvantage(SkillType.Athletics);
                var result = SkillCheckResolver.Resolve(
                    effectCtx.Rng, new SkillCheckRequest(12, modifier, advantage, "Athletics"));
                switch (result.OutcomeTier)
                {
                    case RollOutcomeTier.CriticalSuccess:
                        effectCtx.SetOutcomeNarration(
                            $"{name} drags themselves a few inches forward, teeth gritted, " +
                            $"fingers digging into the dirt — refusing to give up.");
                        break;
                    case RollOutcomeTier.Success:
                        effectCtx.SetOutcomeNarration(
                            $"{name} manages to shift their weight and inch forward, " +
                            $"panting, before the effort becomes too much.");
                        break;
                    case RollOutcomeTier.PartialSuccess:
                        effectCtx.SetOutcomeNarration(
                            $"{name} tries to crawl but their arms buckle. " +
                            $"They collapse back to the ground, panting.");
                        break;
                    default:
                        effectCtx.SetOutcomeNarration(
                            $"{name} twitches and writhes weakly, limbs barely responding, " +
                            $"unable to move more than a trembling inch.");
                        break;
                }
            }),
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Safety] = 0.5
            },
            ActorRequirement: CandidateRequirements.DownedOnly
        ));
    }

    private void AddDeadCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        // A minimal placeholder action so the engine can gracefully handle dead actors.
        // Dead actors "choose" this action indefinitely, effectively stopping all activity.
        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("lie_still"),
                ActionKind.Wait,
                EmptyActionParameters.Instance,
                Duration.Minutes(60.0),
                NarrationDescription: "lie still"
            ),
            0.0,
            Reason: "Lie still (dead)",
            Qualities: new Dictionary<QualityType, double>(),
            ActorRequirement: CandidateRequirements.DeadOnly
        ));
    }
}

public enum BuffType
{
    SkillBonus,
    Advantage,
    RainProtection,
    /// <summary>Continuous metabolic effect (basal burn, activity drain, sleep recovery).
    /// Carried as a <see cref="MetabolicBuff"/> instance that implements <see cref="ITickableBuff"/>.</summary>
    Metabolic,
    /// <summary>Tracks whether the actor is alive, downed, or dead.
    /// Carried as an <see cref="AlivenessBuff"/> instance.</summary>
    Aliveness,
    /// <summary>Continuous vitality/health effect: health deterioration from starvation/exhaustion/psyche strain
    /// and slow recovery under stable conditions.
    /// Carried as a <see cref="VitalityBuff"/> instance that implements <see cref="ITickableBuff"/>.</summary>
    Vitality
}

/// <summary>
/// Base class for all actor buffs.
/// Non-tickable buffs (skill bonuses, advantage markers) use <c>ExpiresAtTick</c> for removal.
/// Tickable buffs (e.g., <see cref="MetabolicBuff"/>) implement <see cref="ITickableBuff"/> and
/// set <c>ExpiresAtTick = long.MaxValue</c> so they are never auto-removed.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "buffKind",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor,
    IgnoreUnrecognizedTypeDiscriminators = true)]
[JsonDerivedType(typeof(ActiveBuff), typeDiscriminator: "base")]
[JsonDerivedType(typeof(MetabolicBuff), typeDiscriminator: "metabolic")]
[JsonDerivedType(typeof(AlivenessBuff), typeDiscriminator: "aliveness")]
[JsonDerivedType(typeof(VitalityBuff),  typeDiscriminator: "vitality")]
public class ActiveBuff
{
    public string Name { get; set; } = "";
    public BuffType Type { get; set; }
    public SkillType? SkillType { get; set; }
    public int Value { get; set; }
    public long ExpiresAtTick { get; set; }

    /// <summary>
    /// Returns a human-readable description of this buff including its state and duration.
    /// Derived classes should override this to expose their specific state fields.
    /// </summary>
    /// <param name="currentTick">The current engine tick, used to compute remaining duration.</param>
    public virtual string Describe(long currentTick)
    {
        var parts = new List<string>();
        if (Value != 0)
            parts.Add($"value={Value}");
        if (ExpiresAtTick == long.MaxValue)
            parts.Add("permanent");
        else
        {
            var remainingTicks = ExpiresAtTick - currentTick;
            if (remainingTicks <= 0)
                parts.Add("expired");
            else
            {
                var remainingSecs = (int)Math.Ceiling(remainingTicks / (double)EngineConstants.TickHz);
                parts.Add($"remaining={remainingSecs}s");
            }
        }
        return $"{Name}({string.Join(", ", parts)})";
    }
}

public class PendingIntent
{
    public string ActionId { get; set; } = "";
    public string Type { get; set; } = "";
    public Dictionary<string, object> Data { get; set; } = new();
    public long EnqueuedAtTick { get; set; }
}
