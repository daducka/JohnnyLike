using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Metabolism;
using JohnnyLike.Domain.Island.Recipes;
using JohnnyLike.Domain.Island.Telemetry;
using JohnnyLike.Domain.Kit.Dice;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JohnnyLike.Domain.Island;

public enum SkillType
{
    Fishing,
    Survival,
    Perception,
    Performance,
    Athletics
}

public class IslandActorState : ActorState, IIslandActionCandidate
{
    public int STR { get; set; } = 10;
    public int DEX { get; set; } = 10;
    public int CON { get; set; } = 10;
    public int INT { get; set; } = 10;
    public int WIS { get; set; } = 10;
    public int CHA { get; set; } = 10;

    public int FishingSkill => DndMath.AbilityModifier(DEX) + DndMath.AbilityModifier(WIS);
    public int SurvivalSkill => DndMath.AbilityModifier(WIS) + DndMath.AbilityModifier(STR);
    public int PerceptionSkill => DndMath.AbilityModifier(WIS);
    public int PerformanceSkill => DndMath.AbilityModifier(CHA);
    public int AthleticsSkill => DndMath.AbilityModifier(STR);

    private double _satiety = 100.0;
    private double _energy = 100.0;
    private double _morale = 50.0;
    private double _health = 100.0;

    public double Satiety { get => _satiety; set => _satiety = Math.Clamp(value, 0.0, 100.0); }
    public double Energy   { get => _energy;  set => _energy  = Math.Clamp(value, 0.0, 100.0); }
    public double Morale   { get => _morale;  set => _morale  = Math.Clamp(value, 0.0, 100.0); }
    public double Health   { get => _health;  set => _health  = Math.Clamp(value, 0.0, 100.0); }

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

    public List<ActiveBuff> ActiveBuffs { get; set; } = new();
    public Queue<PendingIntent> PendingChatActions { get; set; } = new();
    /// <summary>
    /// IDs of recipes this actor knows. Each actor can have a different set of known recipes.
    /// </summary>
    public HashSet<string> KnownRecipeIds { get; set; } = new();

    public int GetSkillModifier(SkillType skillType)
    {
        var baseModifier = skillType switch
        {
            SkillType.Fishing => FishingSkill,
            SkillType.Survival => SurvivalSkill,
            SkillType.Perception => PerceptionSkill,
            SkillType.Performance => PerformanceSkill,
            SkillType.Athletics => AthleticsSkill,
            _ => 0
        };

        var buffModifier = ActiveBuffs
            .Where(b => (b.SkillType == skillType || b.SkillType == null) && b.Type == BuffType.SkillBonus)
            .Sum(b => b.Value);

        return baseModifier + buffModifier;
    }

    public AdvantageType GetAdvantage(SkillType skillType)
    {
        var hasBuff = ActiveBuffs.Any(b => b.SkillType == skillType && b.Type == BuffType.Advantage);
        return hasBuff ? AdvantageType.Advantage : AdvantageType.Normal;
    }

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
    /// Actors can provide their own action candidates including idle, sleep, swim, build sand castle, and chat actions.
    /// </summary>
    public void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        // Process pending chat actions first (unless survival critical)
        AddChatCandidates(ctx, output);
        
        // Idle must ALWAYS be a candidate with a low baseline score
        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("idle"),
                ActionKind.Wait,
                EmptyActionParameters.Instance,
                100L,
                NarrationDescription: "wait and rest for a moment"
            ),
            0.3,
            Reason: "Idle",
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Rest]       = 0.6,
                [QualityType.Comfort]    = 0.3,
                [QualityType.Efficiency] = -0.5
            }
        ));
        
        // Sleep under tree
        AddSleepCandidate(ctx, output);
        
        // Swim
        AddSwimCandidate(ctx, output);
        
        // Build sand castle
        AddBuildSandCastleCandidate(ctx, output);

        // Think about supplies (triggers recipe discovery)
        AddThinkAboutSuppliesCandidate(ctx, output);

        // Known recipes
        foreach (var recipeId in KnownRecipeIds)
        {
            if (!IslandRecipeRegistry.All.TryGetValue(recipeId, out var recipe))
                continue; // skip stale or removed recipe IDs

            RecipeCandidateBuilder.AddCandidate(recipe, ctx, output);
        }
    }

    private void AddBuildSandCastleCandidate(IslandContext ctx, List<ActionCandidate> output)
    {
        // Only provide candidate if no sand castle already exists
        var existingSandCastle = ctx.World.WorldItems.OfType<Items.SandCastleItem>().FirstOrDefault();
        if (existingSandCastle != null)
            return;

        var baseDC = 8;

        var beach = ctx.World.GetItem<Items.BeachItem>("beach");
        if (beach?.Tide == Items.TideLevel.High)
            baseDC += 4;

        var parameters = ctx.RollSkillCheck(SkillType.Performance, baseDC);
        var baseScore = 0.2;

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("build_sand_castle"),
                ActionKind.Interact,
                parameters,
                EngineConstants.TimeToTicks(20.0, 30.0, ctx.Random),
                "build a sand castle on the beach",
                parameters.ToResultData(),
                new List<ResourceRequirement> { new ResourceRequirement(new ResourceId("island:resource:beach:sandcastle_spot")) }
            ),
            baseScore,
            Reason: $"Build sand castle (DC {baseDC}, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})",
            EffectHandler: new Action<EffectContext>(effectCtx =>
            {
                if (effectCtx.Tier == null)
                    return;

                var tier = effectCtx.Tier.Value;
                var actor = effectCtx.ActorId.Value;

                switch (tier)
                {
                    case RollOutcomeTier.CriticalSuccess:
                        effectCtx.Actor.Morale += 25.0;
                        // Create sand castle
                        effectCtx.World.AddWorldItem(new Items.SandCastleItem(), effectCtx.Actor.CurrentRoomId);
                        effectCtx.SetOutcomeNarration($"{actor} sculpts an impressive sand castle complete with towers and a moat.");
                        break;

                    case RollOutcomeTier.Success:
                        effectCtx.Actor.Morale += 15.0;
                        // Create sand castle
                        effectCtx.World.AddWorldItem(new Items.SandCastleItem(), effectCtx.Actor.CurrentRoomId);
                        effectCtx.SetOutcomeNarration($"{actor} pats the last handful of sand into place and steps back to admire the castle.");
                        break;

                    case RollOutcomeTier.PartialSuccess:
                        effectCtx.Actor.Morale += 5.0;
                        // Create sand castle
                        effectCtx.World.AddWorldItem(new Items.SandCastleItem(), effectCtx.Actor.CurrentRoomId);
                        effectCtx.SetOutcomeNarration($"{actor} manages a lopsided but recognisable sand castle.");
                        break;

                    case RollOutcomeTier.Failure:
                        effectCtx.SetOutcomeNarration($"{actor}'s sand castle collapses before it can take shape.");
                        break;

                    case RollOutcomeTier.CriticalFailure:
                        effectCtx.Actor.Morale -= 5.0;
                        effectCtx.SetOutcomeNarration($"{actor} kicks the sand in frustration as the whole thing crumbles.");
                        break;
                }
            }),
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Fun] = 1.0
            }
        ));
    }

    private void AddThinkAboutSuppliesCandidate(IslandContext ctx, List<ActionCandidate> output)
    {
        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("think_about_supplies"),
                ActionKind.Wait,
                EmptyActionParameters.Instance,
                EngineConstants.TimeToTicks(10.0, 15.0, ctx.Random),
                NarrationDescription: "think about available supplies"
            ),
            0.2,
            Reason: "Think about supplies",
            EffectHandler: new Action<EffectContext>(effectCtx =>
            {
                // Use effect-time Rng (not the candidate-generation ctx) for deterministic rolls.
                RecipeDiscoverySystem.TryDiscover(
                    effectCtx.Actor, effectCtx.World, effectCtx.Rng,
                    DiscoveryTrigger.ThinkAboutSupplies,
                    actorId: effectCtx.ActorId.Value);
            }),
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Preparation] = 0.5,
                [QualityType.Efficiency]  = 0.5
            }
        ));
    }

    private void AddChatCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        if (PendingChatActions.Count > 0)
        {
            var isSurvivalCritical = ctx.IsSurvivalCritical();
            
            if (!isSurvivalCritical)
            {
                var intent = PendingChatActions.Peek();
                
                if (intent.ActionId == "write_name_sand")
                {
                    var name = intent.Data.GetValueOrDefault("viewer_name", "Someone")?.ToString() ?? "Someone";
                    output.Add(new ActionCandidate(
                        new ActionSpec(
                            new ActionId("write_name_sand"),
                            ActionKind.Emote,
                            new EmoteActionParameters("write_name", name, "beach"),
                            160L,
                            NarrationDescription: "write name in the sand"
                        ),
                        2.0, // High priority
                        Reason: $"Write {name}'s name in sand (chat redeem)",
                        EffectHandler: new Action<EffectContext>(effectCtx =>
                        {
                            // Dequeue the completed chat action intent
                            if (effectCtx.Actor.PendingChatActions.Count > 0)
                            {
                                effectCtx.Actor.PendingChatActions.Dequeue();
                            }
                            
                            effectCtx.Actor.Morale += 10.0;
                        }),
                        Qualities: new Dictionary<QualityType, double>
                        {
                            [QualityType.Fun]    = 0.8,
                            [QualityType.Comfort] = 0.2
                        }
                    ));
                }
                else if (intent.ActionId == "clap_emote")
                {
                    output.Add(new ActionCandidate(
                        new ActionSpec(
                            new ActionId("clap_emote"),
                            ActionKind.Emote,
                            new EmoteActionParameters("clap"),
                            40L,
                            NarrationDescription: "clap"
                        ),
                        2.0, // High priority
                        Reason: "Clap emote (sub/cheer)",
                        EffectHandler: new Action<EffectContext>(effectCtx =>
                        {
                            // Dequeue the completed chat action intent
                            if (effectCtx.Actor.PendingChatActions.Count > 0)
                            {
                                effectCtx.Actor.PendingChatActions.Dequeue();
                            }
                            
                            effectCtx.Actor.Morale += 3.0;
                        }),
                        Qualities: new Dictionary<QualityType, double>
                        {
                            [QualityType.Fun]    = 0.8,
                            [QualityType.Comfort] = 0.2
                        }
                    ));
                }
            }
        }
    }

    private void AddSleepCandidate(IslandContext ctx, List<ActionCandidate> output)
    {
        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("sleep_under_tree"),
                ActionKind.Interact,
                new LocationActionParameters("tree"),
                600L + (long)(ctx.Rng.NextDouble() * 200),
                NarrationDescription: "take a nap under the shade of a tree"
            ),
            0.35,
            Reason: "Sleep under tree",
            PreAction: (Func<EffectContext, bool>)(effectCtx =>
            {
                // Switch to Sleeping intensity so the MetabolicBuff recovers Energy during the nap.
                var metabolicBuff = effectCtx.Actor.ActiveBuffs.OfType<MetabolicBuff>().FirstOrDefault();
                if (metabolicBuff != null)
                    metabolicBuff.Intensity = MetabolicIntensity.Sleeping;
                return true;
            }),
            EffectHandler: new Action<EffectContext>(effectCtx =>
            {
                // Energy recovery during sleep is handled by MetabolicBuff.OnTick (Sleeping intensity).
                var actor = effectCtx.ActorId.Value;
                effectCtx.SetOutcomeNarration($"{actor} stirs awake, feeling well-rested.");
            }),
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Rest] = 1.0,
                [QualityType.Safety] = 0.2
            }
        ));
    }

    private void AddSwimCandidate(IslandContext ctx, List<ActionCandidate> output)
    {
        if (Energy < 20.0)
            return;

        var baseDC = 10;

        var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC);

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("swim"),
                ActionKind.Interact,
                parameters,
                EngineConstants.TimeToTicks(15.0, 20.0, ctx.Random),
                "swim in the ocean",
                parameters.ToResultData(),
                new List<ResourceRequirement> { new ResourceRequirement(new ResourceId("island:resource:water")) }
            ),
            0.4,
            Reason: $"Swim (DC {baseDC}, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})",
            PreAction: (Func<EffectContext, bool>)(effectCtx =>
            {
                // Switch to Heavy intensity so the MetabolicBuff drains Energy appropriately during swimming.
                var metabolicBuff = effectCtx.Actor.ActiveBuffs.OfType<MetabolicBuff>().FirstOrDefault();
                if (metabolicBuff != null)
                    metabolicBuff.Intensity = MetabolicIntensity.Heavy;
                return true;
            }),
            EffectHandler: new Action<EffectContext>(effectCtx =>
            {
                if (effectCtx.Tier == null)
                    return;

                var tier = effectCtx.Tier.Value;
                var actor = effectCtx.ActorId.Value;

                switch (tier)
                {
                    case RollOutcomeTier.CriticalSuccess:
                        // Heavy-intensity swim energy drain is handled by the metabolism time-step in ApplyActionEffects.
                        // Tier only affects Morale and special encounters.
                        effectCtx.Actor.Morale += 20.0;
                        effectCtx.SetOutcomeNarration($"{actor} glides through the water effortlessly, feeling exhilarated.");
                        
                        // Spawn treasure chest if not already present
                        if (effectCtx.World.TreasureChest == null)
                        {
                            var chest = new Items.TreasureChestItem
                            {
                                IsOpened = false,
                                Health = 100.0,
                                Position = "shore"
                            };
                            effectCtx.World.AddWorldItem(chest, effectCtx.Actor.CurrentRoomId);
                            
                            if (effectCtx.Outcome.ResultData != null)
                            {
                                effectCtx.Outcome.ResultData["variant_id"] = "swim_crit_success_treasure";
                                effectCtx.Outcome.ResultData["encounter_type"] = "treasure_chest";
                            }
                        }
                        break;

                    case RollOutcomeTier.Success:
                        effectCtx.Actor.Morale += 10.0;
                        effectCtx.SetOutcomeNarration($"{actor} has a pleasant swim, washing off the island grime.");
                        break;

                    case RollOutcomeTier.PartialSuccess:
                        effectCtx.Actor.Morale += 3.0;
                        effectCtx.SetOutcomeNarration($"{actor} manages to stay afloat but struggles against the current.");
                        break;

                    case RollOutcomeTier.Failure:
                        effectCtx.Actor.Morale -= 5.0;
                        effectCtx.SetOutcomeNarration($"{actor} is pushed back by the waves, exhausted and discouraged.");
                        break;

                    case RollOutcomeTier.CriticalFailure:
                        effectCtx.Actor.Morale -= 15.0;
                        effectCtx.SetOutcomeNarration($"{actor} barely makes it back to shore, heart pounding.");
                        
                        // Spawn shark if not already present
                        if (effectCtx.World.Shark == null)
                        {
                            var duration = 60.0 + effectCtx.Rng.NextDouble() * 120.0;
                            var shark = new Items.SharkItem
                            {
                                ExpiresAtTick = effectCtx.World.CurrentTick + (long)(duration * 20)
                            };
                            
                            // Try to reserve the water resource
                            var waterResource = new ResourceId("island:resource:water");
                            var utilityId = $"world_item:shark:{shark.Id}";
                            var reserved = effectCtx.Reservations.TryReserve(waterResource, utilityId, shark.ExpiresAtTick);
                            
                            if (reserved)
                            {
                                shark.ReservedResourceId = waterResource;
                                effectCtx.World.AddWorldItem(shark, effectCtx.Actor.CurrentRoomId);
                                effectCtx.Actor.Morale -= 15.0;
                                
                                if (effectCtx.Outcome.ResultData != null)
                                {
                                    effectCtx.Outcome.ResultData["variant_id"] = "swim_crit_failure_shark";
                                    effectCtx.Outcome.ResultData["encounter_type"] = "shark";
                                    effectCtx.Outcome.ResultData["shark_duration"] = duration;
                                }
                            }
                        }
                        break;
                }
            }),
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Fun]    = 0.8,
                [QualityType.Comfort] = 0.3,
                [QualityType.Safety] = -0.5
            }
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
    Metabolic
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
public class ActiveBuff
{
    public string Name { get; set; } = "";
    public BuffType Type { get; set; }
    public SkillType? SkillType { get; set; }
    public int Value { get; set; }
    public long ExpiresAtTick { get; set; }
}

public class PendingIntent
{
    public string ActionId { get; set; } = "";
    public string Type { get; set; } = "";
    public Dictionary<string, object> Data { get; set; } = new();
    public long EnqueuedAtTick { get; set; }
}
