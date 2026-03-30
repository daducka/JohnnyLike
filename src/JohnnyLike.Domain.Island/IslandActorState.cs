using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Island.Metabolism;
using JohnnyLike.Domain.Island.Recipes;
using JohnnyLike.Domain.Island.Telemetry;
using JohnnyLike.Domain.Island.Vitality;
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
    Athletics,
    Constitution
}

public class IslandActorState : ActorState, IIslandActionCandidate
{
    public int STR { get; set; } = 10;
    public int DEX { get; set; } = 10;
    public int CON { get; set; } = 10;
    public int INT { get; set; } = 10;
    public int WIS { get; set; } = 10;
    public int CHA { get; set; } = 10;

    public int FishingSkill      => DndMath.AbilityModifier(DEX) + DndMath.AbilityModifier(WIS);
    public int SurvivalSkill     => DndMath.AbilityModifier(WIS) + DndMath.AbilityModifier(STR);
    public int PerceptionSkill   => DndMath.AbilityModifier(WIS);
    public int PerformanceSkill  => DndMath.AbilityModifier(CHA);
    public int AthleticsSkill    => DndMath.AbilityModifier(STR);
    public int ConstitutionSkill => DndMath.AbilityModifier(CON);

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
            SkillType.Fishing      => FishingSkill,
            SkillType.Survival     => SurvivalSkill,
            SkillType.Perception   => PerceptionSkill,
            SkillType.Performance  => PerformanceSkill,
            SkillType.Athletics    => AthleticsSkill,
            SkillType.Constitution => ConstitutionSkill,
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

    /// <summary>Returns <c>true</c> if the actor currently has an active buff of type <typeparamref name="T"/>.</summary>
    public bool HasBuff<T>() where T : ActiveBuff
        => ActiveBuffs.OfType<T>().Any();

    /// <summary>
    /// Returns the first active buff of type <typeparamref name="T"/>, or <c>null</c> if none is present.
    /// </summary>
    public T? TryGetBuff<T>() where T : ActiveBuff
        => ActiveBuffs.OfType<T>().FirstOrDefault();

    /// <summary>
    /// Returns <c>true</c> if the actor has an active buff of type <typeparamref name="T"/>
    /// that also satisfies <paramref name="predicate"/>.
    /// </summary>
    public bool HasBuffWhere<T>(Func<T, bool> predicate) where T : ActiveBuff
        => ActiveBuffs.OfType<T>().Any(predicate);

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

        // Downed-state candidates (limited actions while fighting for survival)
        AddDownedCandidates(ctx, output);

        // Dead-state placeholder (actor lies still; allows future corpse-interaction candidates)
        AddDeadCandidates(ctx, output);
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
        var baseScore = 0.08;

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("build_sand_castle"),
                ActionKind.Interact,
                parameters,
                Duration.Minutes(20.0, 30.0, ctx.Random),
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
            },
            ActorRequirement: CandidateRequirements.PlayfulOnly
        ));
    }

    private void AddThinkAboutSuppliesCandidate(IslandContext ctx, List<ActionCandidate> output)
    {
        var tuning   = ctx.TuningProfile.Categories.ThinkAboutSupplies;
        var qualities = ComputeThinkAboutSuppliesQualities(this, ctx.World, tuning, ctx.QualityEffectiveWeight);

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("think_about_supplies"),
                ActionKind.Wait,
                EmptyActionParameters.Instance,
                Duration.Minutes(10.0, 15.0, ctx.Random),
                NarrationDescription: "think about available supplies"
            ),
            0.08,
            Reason: "Think about supplies",
            EffectHandler: new Action<EffectContext>(effectCtx =>
            {
                // Use effect-time Rng (not the candidate-generation ctx) for deterministic rolls.
                RecipeDiscoverySystem.TryDiscover(
                    effectCtx.Actor, effectCtx.World, effectCtx.Rng,
                    DiscoveryTrigger.ThinkAboutSupplies,
                    actorId: effectCtx.ActorId.Value,
                    sourceActionId: "think_about_supplies");
            }),
            Qualities: qualities,
            ActorRequirement: CandidateRequirements.AliveOnly
        ));
    }

    /// <summary>
    /// Computes dynamic action qualities for <c>think_about_supplies</c> based on which
    /// recipes the actor can currently discover.  Uses a weighted top-N blend so that a
    /// single highly relevant survival recipe is not drowned out by many mediocre ones.
    /// Falls back to a small default when no meaningful discovery opportunity exists.
    /// When the actor is starving and discoverable recipes would not materially help with
    /// food or safety, qualities are further suppressed so the action loses priority.
    /// </summary>
    /// <param name="tuning">Tuning parameters from the active <see cref="ThinkAboutSuppliesTuning"/>.</param>
    /// <param name="effectiveWeight">
    /// Optional function returning the current effective quality weight for a given quality type.
    /// When provided, recipes are ranked by <c>baseChance × Σ(qualityValue × effectiveWeight)</c>
    /// so state-relevant discoveries (e.g. food recipes when hungry) score higher naturally.
    /// Falls back to <c>baseChance × Σ(qualityValue)</c> when null.
    /// </param>
    private static Dictionary<QualityType, double> ComputeThinkAboutSuppliesQualities(
        IslandActorState actor,
        IslandWorldState world,
        ThinkAboutSuppliesTuning tuning,
        Func<QualityType, double>? effectiveWeight = null)
    {
        // Collect discoverable recipes for this trigger and actor state.
        var discoverable = new List<(double weight, IReadOnlyDictionary<QualityType, double> qualities)>();

        foreach (var (id, recipe) in IslandRecipeRegistry.All)
        {
            if (recipe.Discovery == null || recipe.Discovery.Trigger != DiscoveryTrigger.ThinkAboutSupplies)
                continue;

            if (actor.KnownRecipeIds.Contains(id))
                continue;

            if (!recipe.Discovery.CanDiscover(actor, world))
                continue;

            discoverable.Add((recipe.Discovery.BaseChance, recipe.Qualities));
        }

        if (discoverable.Count == 0)
        {
            // No discoverable recipes — use a small fallback so the action stays in the
            // pool but cannot dominate over more urgent survival options.
            return new Dictionary<QualityType, double>
            {
                [QualityType.Preparation] = tuning.FallbackPreparation,
                [QualityType.Efficiency]  = tuning.FallbackEfficiency
            };
        }

        // Sort by state-aware weighted usefulness and take top-N.
        // When effective quality weights are available (normal path), rank by
        //   baseChance × Σ(qualityValue × effectiveWeight(quality))
        // so recipes whose qualities align with current actor needs (e.g. food when hungry,
        // safety when injured) naturally rank higher.  Falls back to raw quality sum otherwise.
        // This prevents many low-value recipes from diluting a single high-value one.
        double RecipeScore((double weight, IReadOnlyDictionary<QualityType, double> qualities) r)
            => effectiveWeight != null
                ? r.weight * r.qualities.Sum(kvp => kvp.Value * effectiveWeight(kvp.Key))
                : r.weight * r.qualities.Values.Sum();

        var topRecipes = discoverable
            .OrderByDescending(RecipeScore)
            .Take(tuning.TopN)
            .ToList();

        // Compute a weight-normalised blend of the top recipes' qualities.
        var result = new Dictionary<QualityType, double>(Enum.GetValues<QualityType>().Length);
        var totalWeight = topRecipes.Sum(r => r.weight);

        foreach (var (weight, qualities) in topRecipes)
        {
            foreach (var (q, v) in qualities)
            {
                result.TryGetValue(q, out var existing);
                result[q] = existing + v * weight / totalWeight;
            }
        }

        // Survival distress suppression: when the actor is starving and none of the
        // discoverable recipes materially help with food access or safety, reduce the
        // qualities significantly so direct food actions take priority.
        if (actor.Satiety < tuning.StarvationThreshold)
        {
            bool hasSurvivalRelevantRecipe = topRecipes.Any(r =>
                r.qualities.ContainsKey(QualityType.FoodConsumption) ||
                r.qualities.ContainsKey(QualityType.FoodAcquisition) ||
                r.qualities.ContainsKey(QualityType.Safety));

            if (!hasSurvivalRelevantRecipe)
            {
                foreach (var key in result.Keys.ToList())
                    result[key] *= tuning.StarvationSuppression;
            }
        }

        return result;
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
                            Duration.Seconds(8.0), // Quick emote animation (~8 s in real time)
                            NarrationDescription: "write name in the sand"
                        ),
                        1.1, // High priority — chat redemptions override normal actions
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
                        },
                        ActorRequirement: CandidateRequirements.AliveOnly
                    ));
                }
                else if (intent.ActionId == "clap_emote")
                {
                    output.Add(new ActionCandidate(
                        new ActionSpec(
                            new ActionId("clap_emote"),
                            ActionKind.Emote,
                            new EmoteActionParameters("clap"),
                            Duration.Seconds(2.0), // Quick clap emote animation (~2 s in real time)
                            NarrationDescription: "clap"
                        ),
                        1.0, // High priority — chat redemptions override normal actions
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
                        },
                        ActorRequirement: CandidateRequirements.AliveOnly
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
                Duration.Minutes(30.0, 40.0, ctx.Random),
                NarrationDescription: "take a nap under the shade of a tree"
            ),
            0.14,
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
            },
            ActorRequirement: CandidateRequirements.AliveOnly
        ));
    }

    private void AddSwimCandidate(IslandContext ctx, List<ActionCandidate> output)
    {
        var baseDC = 10;

        var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC);

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("swim"),
                ActionKind.Interact,
                parameters,
                Duration.Minutes(15.0, 20.0, ctx.Random),
                "swim in the ocean",
                parameters.ToResultData(),
                new List<ResourceRequirement> { new ResourceRequirement(new ResourceId("island:resource:water")) }
            ),
            0.18,
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
            },
            ActorRequirement: CandidateRequirements.PlayfulOnly
        ));
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
                    aliveness.State              = AlivenessState.Alive;
                    aliveness.DeathSaveSuccesses = 0;
                    aliveness.DeathSaveFailures  = 0;
                    island.Health = ReviveHealth;
                    effectCtx.World.Tracer.Beat(
                        $"[Aliveness] {actorName} stabilized and regained consciousness",
                        actorId: actorName, priority: 80);
                    effectCtx.SetOutcomeNarration(
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
                        aliveness.State              = AlivenessState.Alive;
                        aliveness.DeathSaveSuccesses = 0;
                        aliveness.DeathSaveFailures  = 0;
                        island.Health = ReviveHealth;
                        effectCtx.World.Tracer.Beat(
                            $"[Aliveness] {actorName} stabilized and regained consciousness",
                            actorId: actorName, priority: 80);
                        effectCtx.SetOutcomeNarration($"{actorName} gasps and drags themselves back from the brink.");
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
                        aliveness.State = AlivenessState.Dead;
                        effectCtx.World.Tracer.Beat(
                            $"[Aliveness] {actorName} has died",
                            actorId: actorName, priority: 90);
                        effectCtx.SetOutcomeNarration(
                            $"{actorName}'s eyes go glassy. Their chest stops moving. " +
                            $"The only sound left is the wind and the waves — indifferent, endless. " +
                            $"{actorName} is gone.");

                        var corpseId = $"corpse_{actorName.ToLowerInvariant()}";
                        var corpse = new CorpseItem(corpseId) { ActorName = actorName };
                        var roomId  = island.CurrentRoomId;
                        effectCtx.World.AddWorldItem(corpse, roomId);
                        effectCtx.World.Tracer.Beat(
                            $"[Aliveness] The remains of {actorName} now lie at {roomId}",
                            actorId: actorName, priority: 70);
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
                        aliveness.State = AlivenessState.Dead;
                        effectCtx.World.Tracer.Beat(
                            $"[Aliveness] {actorName} has died",
                            actorId: actorName, priority: 90);
                        effectCtx.SetOutcomeNarration(
                            $"{actorName}'s eyes go glassy. Their chest stops moving. " +
                            $"The only sound left is the wind and the waves — indifferent, endless. " +
                            $"{actorName} is gone.");

                        var corpseId = $"corpse_{actorName.ToLowerInvariant()}";
                        var corpse = new CorpseItem(corpseId) { ActorName = actorName };
                        var roomId  = island.CurrentRoomId;
                        effectCtx.World.AddWorldItem(corpse, roomId);
                        effectCtx.World.Tracer.Beat(
                            $"[Aliveness] The remains of {actorName} now lie at {roomId}",
                            actorId: actorName, priority: 70);
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
