using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island;

public class IslandDomainPack : IDomainPack
{
    public string DomainName => "Island";

    private readonly Dictionary<ActorId, double> _lastPlaneSighting = new();
    private readonly Dictionary<ActorId, double> _lastMermaidEncounter = new();

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
        var candidates = new List<ActionCandidate>();
        var islandState = (IslandActorState)actorState;
        var islandWorld = (IslandWorldState)worldState;
        var rngStream = new RandomRngStream(rng);

        islandWorld.OnTimeAdvanced(currentTime, 0.0);

        islandState.ActiveBuffs.RemoveAll(b => b.ExpiresAt <= currentTime);

        AddFishingCandidate(actorId, islandState, islandWorld, currentTime, rngStream, candidates);
        AddCoconutCandidate(actorId, islandState, islandWorld, currentTime, rngStream, candidates);
        AddSandCastleCandidate(actorId, islandState, islandWorld, currentTime, rngStream, candidates);
        AddSwimCandidate(actorId, islandState, islandWorld, currentTime, rngStream, candidates);
        AddSleepCandidate(actorId, islandState, islandWorld, currentTime, rngStream, candidates);

        AddVignetteEvents(actorId, islandState, islandWorld, currentTime, rngStream, candidates, rng);

        if (candidates.Count == 0)
        {
            candidates.Add(new ActionCandidate(
                new ActionSpec(
                    new ActionId("idle"),
                    ActionKind.Wait,
                    new Dictionary<string, object>(),
                    5.0
                ),
                0.3,
                "Idle"
            ));
        }

        return candidates;
    }

    private void AddFishingCandidate(
        ActorId actorId,
        IslandActorState state,
        IslandWorldState world,
        double currentTime,
        IRngStream rng,
        List<ActionCandidate> candidates)
    {
        if (world.FishAvailable < 5.0)
            return;

        var baseDC = 10;
        
        var timeOfDay = world.TimeOfDay;
        if (timeOfDay < 0.25 || timeOfDay > 0.75)
            baseDC += 3;

        if (world.Weather == Weather.Rainy)
            baseDC += 2;
        else if (world.Weather == Weather.Windy)
            baseDC += 1;

        if (world.FishAvailable < 20.0)
            baseDC += 3;
        else if (world.FishAvailable < 50.0)
            baseDC += 1;

        if (state.Energy < 30.0)
            baseDC += 2;

        var modifier = state.GetSkillModifier("Fishing");
        var advantage = state.GetAdvantage("Fishing");

        var request = new SkillCheckRequest(baseDC, modifier, advantage, "Fishing", "Fishing for food");
        var estimatedChance = DndMath.EstimateSuccessChanceD20(baseDC, modifier, advantage);

        var baseScore = 0.5 + (state.Hunger / 100.0);
        if (state.Hunger > 70.0 || state.Energy < 20.0)
        {
            baseScore = 1.0;
        }
        else
        {
            baseScore *= estimatedChance;
        }

        candidates.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("fish_for_food"),
                ActionKind.Interact,
                new Dictionary<string, object>
                {
                    ["dc"] = baseDC,
                    ["modifier"] = modifier,
                    ["advantage"] = advantage.ToString(),
                    ["location"] = "shore"
                },
                15.0 + rng.NextDouble() * 5.0
            ),
            baseScore,
            $"Fishing (DC {baseDC}, {estimatedChance:P0} chance)"
        ));
    }

    private void AddCoconutCandidate(
        ActorId actorId,
        IslandActorState state,
        IslandWorldState world,
        double currentTime,
        IRngStream rng,
        List<ActionCandidate> candidates)
    {
        if (world.CoconutsAvailable < 1)
            return;

        var baseDC = 12;

        if (world.CoconutsAvailable >= 5)
            baseDC -= 2;
        else if (world.CoconutsAvailable <= 2)
            baseDC += 2;

        if (world.Weather == Weather.Windy)
            baseDC -= 1;

        var modifier = state.GetSkillModifier("Survival");
        var advantage = state.GetAdvantage("Survival");

        var estimatedChance = DndMath.EstimateSuccessChanceD20(baseDC, modifier, advantage);

        var baseScore = 0.4 + (state.Hunger / 150.0);
        if (state.Hunger > 70.0)
        {
            baseScore = 0.9;
        }
        else
        {
            baseScore *= estimatedChance;
        }

        candidates.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("shake_tree_coconut"),
                ActionKind.Interact,
                new Dictionary<string, object>
                {
                    ["dc"] = baseDC,
                    ["modifier"] = modifier,
                    ["advantage"] = advantage.ToString(),
                    ["location"] = "palm_tree"
                },
                10.0 + rng.NextDouble() * 5.0
            ),
            baseScore,
            $"Get coconut (DC {baseDC}, {estimatedChance:P0} chance)"
        ));
    }

    private void AddSandCastleCandidate(
        ActorId actorId,
        IslandActorState state,
        IslandWorldState world,
        double currentTime,
        IRngStream rng,
        List<ActionCandidate> candidates)
    {
        var baseDC = 8;

        if (world.TideLevel == TideLevel.High)
            baseDC += 4;

        var modifier = state.GetSkillModifier("Performance");
        var advantage = state.GetAdvantage("Performance");

        var estimatedChance = DndMath.EstimateSuccessChanceD20(baseDC, modifier, advantage);

        var baseScore = 0.3 + (state.Boredom / 100.0);
        baseScore *= estimatedChance;

        candidates.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("build_sand_castle"),
                ActionKind.Interact,
                new Dictionary<string, object>
                {
                    ["dc"] = baseDC,
                    ["modifier"] = modifier,
                    ["advantage"] = advantage.ToString(),
                    ["location"] = "beach"
                },
                20.0 + rng.NextDouble() * 10.0
            ),
            baseScore,
            $"Build sand castle (DC {baseDC}, {estimatedChance:P0} chance)"
        ));
    }

    private void AddSwimCandidate(
        ActorId actorId,
        IslandActorState state,
        IslandWorldState world,
        double currentTime,
        IRngStream rng,
        List<ActionCandidate> candidates)
    {
        if (state.Energy < 20.0)
            return;

        var baseDC = 10;

        if (world.Weather == Weather.Windy)
            baseDC += 3;
        else if (world.Weather == Weather.Rainy)
            baseDC += 1;

        var modifier = state.GetSkillModifier("Survival");
        var advantage = state.GetAdvantage("Survival");

        var estimatedChance = DndMath.EstimateSuccessChanceD20(baseDC, modifier, advantage);

        var baseScore = 0.35 + (state.Morale < 30 ? 0.2 : 0.0);
        baseScore *= estimatedChance;

        candidates.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("swim"),
                ActionKind.Interact,
                new Dictionary<string, object>
                {
                    ["dc"] = baseDC,
                    ["modifier"] = modifier,
                    ["advantage"] = advantage.ToString(),
                    ["location"] = "water"
                },
                15.0 + rng.NextDouble() * 5.0
            ),
            baseScore,
            $"Swim (DC {baseDC}, {estimatedChance:P0} chance)"
        ));
    }

    private void AddSleepCandidate(
        ActorId actorId,
        IslandActorState state,
        IslandWorldState world,
        double currentTime,
        IRngStream rng,
        List<ActionCandidate> candidates)
    {
        var baseScore = 0.4;
        if (state.Energy < 30.0)
            baseScore = 1.2;
        else if (state.Energy < 50.0)
            baseScore = 0.8;

        candidates.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("sleep_under_tree"),
                ActionKind.Interact,
                new Dictionary<string, object>
                {
                    ["location"] = "tree"
                },
                30.0 + rng.NextDouble() * 10.0
            ),
            baseScore,
            "Sleep under tree"
        ));
    }

    private void AddVignetteEvents(
        ActorId actorId,
        IslandActorState state,
        IslandWorldState world,
        double currentTime,
        IRngStream rng,
        List<ActionCandidate> candidates,
        Random random)
    {
        if (!_lastPlaneSighting.TryGetValue(actorId, out var lastPlane) || currentTime - lastPlane > 600.0)
        {
            if (random.NextDouble() < 0.05)
            {
                var baseDC = 15;
                var modifier = state.GetSkillModifier("Perception");
                var advantage = state.GetAdvantage("Perception");
                var estimatedChance = DndMath.EstimateSuccessChanceD20(baseDC, modifier, advantage);

                candidates.Add(new ActionCandidate(
                    new ActionSpec(
                        new ActionId("plane_sighting"),
                        ActionKind.Interact,
                        new Dictionary<string, object>
                        {
                            ["dc"] = baseDC,
                            ["modifier"] = modifier,
                            ["advantage"] = advantage.ToString(),
                            ["vignette"] = true
                        },
                        10.0
                    ),
                    0.2 * estimatedChance,
                    "Plane sighting vignette"
                ));
            }
        }

        if (world.TimeOfDay > 0.75 || world.TimeOfDay < 0.25)
        {
            if (!_lastMermaidEncounter.TryGetValue(actorId, out var lastMermaid) || currentTime - lastMermaid > 1200.0)
            {
                if (random.NextDouble() < 0.02)
                {
                    var baseDC = 18;
                    var modifier = state.GetSkillModifier("Perception");
                    var advantage = state.GetAdvantage("Perception");
                    var estimatedChance = DndMath.EstimateSuccessChanceD20(baseDC, modifier, advantage);

                    candidates.Add(new ActionCandidate(
                        new ActionSpec(
                            new ActionId("mermaid_encounter"),
                            ActionKind.Interact,
                            new Dictionary<string, object>
                            {
                                ["dc"] = baseDC,
                                ["modifier"] = modifier,
                                ["advantage"] = advantage.ToString(),
                                ["vignette"] = true
                            },
                            15.0
                        ),
                        0.15 * estimatedChance,
                        "Mermaid encounter vignette"
                    ));
                }
            }
        }
    }

    public void ApplyActionEffects(
        ActorId actorId,
        ActionOutcome outcome,
        ActorState actorState,
        WorldState worldState)
    {
        var islandState = (IslandActorState)actorState;
        var islandWorld = (IslandWorldState)worldState;

        islandWorld.OnTimeAdvanced(outcome.ActualDuration, outcome.ActualDuration);

        islandState.Hunger = Math.Min(100.0, islandState.Hunger + outcome.ActualDuration * 0.5);
        islandState.Energy = Math.Max(0.0, islandState.Energy - outcome.ActualDuration * 0.3);
        islandState.Boredom = Math.Min(100.0, islandState.Boredom + outcome.ActualDuration * 0.4);

        if (outcome.Type != ActionOutcomeType.Success)
        {
            return;
        }

        var actionId = outcome.ActionId.Value;

        if (outcome.ResultData != null && outcome.ResultData.ContainsKey("tier"))
        {
        }
        else if (actionId == "sleep_under_tree")
        {
        }
        else
        {
            var seed = actionId.GetHashCode() ^ actorId.Value.GetHashCode();
            var rngStream = new RandomRngStream(new Random(seed));
            
            var tier = RollOutcomeTier.Success;
            
            if (actorState.CurrentAction != null && actorState.CurrentAction.Parameters.ContainsKey("dc"))
            {
                var dc = (int)actorState.CurrentAction.Parameters["dc"];
                var modifier = (int)actorState.CurrentAction.Parameters["modifier"];
                var advantage = Enum.Parse<AdvantageType>(actorState.CurrentAction.Parameters["advantage"].ToString()!);
                var skillId = actionId.Contains("fish") ? "Fishing" :
                             actionId.Contains("coconut") ? "Survival" :
                             actionId.Contains("castle") ? "Performance" :
                             actionId.Contains("swim") ? "Survival" :
                             actionId.Contains("plane") || actionId.Contains("mermaid") ? "Perception" :
                             "Unknown";

                var request = new SkillCheckRequest(dc, modifier, advantage, skillId);
                var result = SkillCheckResolver.Resolve(rngStream, request);
                tier = result.OutcomeTier;
            }

            if (outcome.ResultData == null)
            {
                outcome = outcome with { ResultData = new Dictionary<string, object>() };
            }
            outcome.ResultData["tier"] = tier.ToString();
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
        _lastPlaneSighting[actorId] = outcome.ActualDuration;

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
                ExpiresAt = outcome.ActualDuration + 300.0
            });
        }
    }

    private void ApplyMermaidEncounterEffects(ActorId actorId, ActionOutcome outcome, IslandActorState state, IslandWorldState world)
    {
        _lastMermaidEncounter[actorId] = outcome.ActualDuration;

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
                ExpiresAt = outcome.ActualDuration + 600.0
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
}
