using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Kit.Dice;
using System.Text.Json;

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

    public double Satiety { get => _satiety; set => _satiety = Math.Clamp(value, 0.0, 100.0); }
    public double Energy   { get => _energy;  set => _energy  = Math.Clamp(value, 0.0, 100.0); }
    public double Morale   { get => _morale;  set => _morale  = Math.Clamp(value, 0.0, 100.0); }

    public double LastPlaneSightingTime { get; set; } = double.NegativeInfinity;
    public double LastMermaidEncounterTime { get; set; } = double.NegativeInfinity;

    public List<ActiveBuff> ActiveBuffs { get; set; } = new();
    public Queue<PendingIntent> PendingChatActions { get; set; } = new();

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
            CurrentScene = CurrentScene?.Value,
            LastDecisionTime,
            STR,
            DEX,
            CON,
            INT,
            WIS,
            CHA,
            Satiety,
            Energy,
            Morale,
            LastPlaneSightingTime,
            LastMermaidEncounterTime,
            ActiveBuffs,
            PendingChatActions = PendingChatActions.ToList()
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
        
        LastDecisionTime = data["LastDecisionTime"].GetDouble();
        STR = data["STR"].GetInt32();
        DEX = data["DEX"].GetInt32();
        CON = data["CON"].GetInt32();
        INT = data["INT"].GetInt32();
        WIS = data["WIS"].GetInt32();
        CHA = data["CHA"].GetInt32();
        Satiety = data["Satiety"].GetDouble();
        Energy = data["Energy"].GetDouble();
        Morale = data["Morale"].GetDouble();

        if (data.TryGetValue("LastPlaneSightingTime", out var lastPlane))
        {
            // Handle both number and string representations (e.g., "-Infinity")
            if (lastPlane.ValueKind == JsonValueKind.String)
            {
                var strVal = lastPlane.GetString();
                LastPlaneSightingTime = strVal switch
                {
                    "-Infinity" => double.NegativeInfinity,
                    "Infinity" => double.PositiveInfinity,
                    "NaN" => double.NaN,
                    _ => double.Parse(strVal!)
                };
            }
            else
            {
                LastPlaneSightingTime = lastPlane.GetDouble();
            }
        }

        if (data.TryGetValue("LastMermaidEncounterTime", out var lastMermaid))
        {
            // Handle both number and string representations (e.g., "-Infinity")
            if (lastMermaid.ValueKind == JsonValueKind.String)
            {
                var strVal = lastMermaid.GetString();
                LastMermaidEncounterTime = strVal switch
                {
                    "-Infinity" => double.NegativeInfinity,
                    "Infinity" => double.PositiveInfinity,
                    "NaN" => double.NaN,
                    _ => double.Parse(strVal!)
                };
            }
            else
            {
                LastMermaidEncounterTime = lastMermaid.GetDouble();
            }
        }

        if (data.TryGetValue("ActiveBuffs", out var buffs))
        {
            ActiveBuffs = JsonSerializer.Deserialize<List<ActiveBuff>>(buffs.GetRawText(), options) ?? new();
        }

        if (data.TryGetValue("PendingChatActions", out var actions))
        {
            var list = JsonSerializer.Deserialize<List<PendingIntent>>(actions.GetRawText(), options) ?? new();
            PendingChatActions = new Queue<PendingIntent>(list);
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
                5.0
            ),
            0.3,
            "Idle"
        ));
        
        // Sleep under tree
        AddSleepCandidate(ctx, output);
        
        // Swim
        AddSwimCandidate(ctx, output);
        
        // Build sand castle
        AddBuildSandCastleCandidate(ctx, output);
    }

    private void AddBuildSandCastleCandidate(IslandContext ctx, List<ActionCandidate> output)
    {
        // Only provide candidate if no sand castle already exists
        var existingSandCastle = ctx.World.WorldItems.OfType<Items.SandCastleItem>().FirstOrDefault();
        if (existingSandCastle != null)
            return;

        var baseDC = 8;

        var tideStat = ctx.World.GetStat<Stats.TideStat>("tide");
        if (tideStat?.TideLevel == TideLevel.High)
            baseDC += 4;

        var parameters = ctx.RollSkillCheck(SkillType.Performance, baseDC);
        var baseScore = 0.3;

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("build_sand_castle"),
                ActionKind.Interact,
                parameters,
                20.0 + ctx.Random.NextDouble() * 10.0,
                parameters.ToResultData(),
                new List<ResourceRequirement> { new ResourceRequirement(new ResourceId("island:resource:beach:sandcastle_spot")) }
            ),
            baseScore,
            $"Build sand castle (DC {baseDC}, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})",
            EffectHandler: new Action<EffectContext>(effectCtx =>
            {
                if (effectCtx.Tier == null)
                    return;

                var tier = effectCtx.Tier.Value;

                switch (tier)
                {
                    case RollOutcomeTier.CriticalSuccess:
                        effectCtx.Actor.Morale += 55.0;
                        // Create sand castle
                        effectCtx.World.WorldItems.Add(new Items.SandCastleItem());
                        break;

                    case RollOutcomeTier.Success:
                        effectCtx.Actor.Morale += 35.0;
                        // Create sand castle
                        effectCtx.World.WorldItems.Add(new Items.SandCastleItem());
                        break;

                    case RollOutcomeTier.PartialSuccess:
                        effectCtx.Actor.Morale += 15.0;
                        // Create sand castle
                        effectCtx.World.WorldItems.Add(new Items.SandCastleItem());
                        break;

                    case RollOutcomeTier.Failure:
                        effectCtx.Actor.Morale += 5.0;
                        break;

                    case RollOutcomeTier.CriticalFailure:
                        effectCtx.Actor.Morale -= 5.0;
                        break;
                }
            }),
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Fun] = 1.0
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
                            8.0
                        ),
                        2.0, // High priority
                        $"Write {name}'s name in sand (chat redeem)",
                        EffectHandler: new Action<EffectContext>(effectCtx =>
                        {
                            // Dequeue the completed chat action intent
                            if (effectCtx.Actor.PendingChatActions.Count > 0)
                            {
                                effectCtx.Actor.PendingChatActions.Dequeue();
                            }
                            
                            effectCtx.Actor.Morale += 25.0;
                        })
                    ));
                }
                else if (intent.ActionId == "clap_emote")
                {
                    output.Add(new ActionCandidate(
                        new ActionSpec(
                            new ActionId("clap_emote"),
                            ActionKind.Emote,
                            new EmoteActionParameters("clap"),
                            2.0
                        ),
                        2.0, // High priority
                        "Clap emote (sub/cheer)",
                        EffectHandler: new Action<EffectContext>(effectCtx =>
                        {
                            // Dequeue the completed chat action intent
                            if (effectCtx.Actor.PendingChatActions.Count > 0)
                            {
                                effectCtx.Actor.PendingChatActions.Dequeue();
                            }
                            
                            effectCtx.Actor.Morale += 3.0;
                        })
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
                30.0 + ctx.Rng.NextDouble() * 10.0
            ),
            0.35,
            "Sleep under tree",
            EffectHandler: new Action<EffectContext>(effectCtx =>
            {
                effectCtx.Actor.Energy += 40.0;
                effectCtx.Actor.Morale += 5.0;
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

        var weatherStat = ctx.World.GetStat<Stats.WeatherStat>("weather");
        if (weatherStat?.Weather == Weather.Windy)
            baseDC += 3;
        else if (weatherStat?.Weather == Weather.Rainy)
            baseDC += 1;

        var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC);
        var baseScore = 0.35 + (Morale < 30 ? 0.2 : 0.0);

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("swim"),
                ActionKind.Interact,
                parameters,
                15.0 + ctx.Random.NextDouble() * 5.0,
                parameters.ToResultData(),
                new List<ResourceRequirement> { new ResourceRequirement(new ResourceId("island:resource:water")) }
            ),
            baseScore,
            $"Swim (DC {baseDC}, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})",
            EffectHandler: new Action<EffectContext>(effectCtx =>
            {
                if (effectCtx.Tier == null)
                    return;

                var tier = effectCtx.Tier.Value;

                switch (tier)
                {
                    case RollOutcomeTier.CriticalSuccess:
                        effectCtx.Actor.Morale += 20.0;
                        effectCtx.Actor.Energy -= 5.0;
                        effectCtx.Actor.Morale += 15.0;
                        
                        // Spawn treasure chest if not already present
                        if (effectCtx.World.TreasureChest == null)
                        {
                            var chest = new Items.TreasureChestItem
                            {
                                IsOpened = false,
                                Health = 100.0,
                                Position = "shore"
                            };
                            effectCtx.World.WorldItems.Add(chest);
                            
                            if (effectCtx.Outcome.ResultData != null)
                            {
                                effectCtx.Outcome.ResultData["variant_id"] = "swim_crit_success_treasure";
                                effectCtx.Outcome.ResultData["encounter_type"] = "treasure_chest";
                            }
                        }
                        break;

                    case RollOutcomeTier.Success:
                        effectCtx.Actor.Morale += 10.0;
                        effectCtx.Actor.Energy -= 10.0;
                        effectCtx.Actor.Morale += 10.0;
                        break;

                    case RollOutcomeTier.PartialSuccess:
                        effectCtx.Actor.Morale += 3.0;
                        effectCtx.Actor.Energy -= 15.0;
                        effectCtx.Actor.Morale += 5.0;
                        break;

                    case RollOutcomeTier.Failure:
                        effectCtx.Actor.Energy -= 15.0;
                        effectCtx.Actor.Morale -= 5.0;
                        break;

                    case RollOutcomeTier.CriticalFailure:
                        effectCtx.Actor.Energy -= 25.0;
                        effectCtx.Actor.Morale -= 15.0;
                        
                        // Spawn shark if not already present
                        if (effectCtx.World.Shark == null)
                        {
                            var duration = 60.0 + effectCtx.Rng.NextDouble() * 120.0;
                            var shark = new Items.SharkItem
                            {
                                ExpiresAt = effectCtx.World.CurrentTime + duration
                            };
                            
                            // Try to reserve the water resource
                            var waterResource = new ResourceId("island:resource:water");
                            var utilityId = $"world_item:shark:{shark.Id}";
                            var reserved = effectCtx.Reservations.TryReserve(waterResource, utilityId, shark.ExpiresAt);
                            
                            if (reserved)
                            {
                                shark.ReservedResourceId = waterResource;
                                effectCtx.World.WorldItems.Add(shark);
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
            })
        ));
    }
}

public enum BuffType
{
    SkillBonus,
    Advantage
}

public class ActiveBuff
{
    public string Name { get; set; } = "";
    public BuffType Type { get; set; }
    public SkillType? SkillType { get; set; }
    public int Value { get; set; }
    public double ExpiresAt { get; set; }
}

public class PendingIntent
{
    public string ActionId { get; set; } = "";
    public string Type { get; set; } = "";
    public Dictionary<string, object> Data { get; set; } = new();
    public double EnqueuedAt { get; set; }
}
