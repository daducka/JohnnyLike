using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Candidates;

[IslandCandidateProvider(410, "swim")]
public class SwimCandidateProvider : IIslandCandidateProvider
{
    private static readonly ResourceId WaterResource = new("island:resource:water");

    public void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        if (ctx.Actor.Energy < 20.0)
            return;

        var baseDC = 10;

        if (ctx.World.Weather == Weather.Windy)
            baseDC += 3;
        else if (ctx.World.Weather == Weather.Rainy)
            baseDC += 1;

        // Roll skill check at candidate generation time
        var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC);

        var baseScore = 0.35 + (ctx.Actor.Morale < 30 ? 0.2 : 0.0);

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("swim"),
                ActionKind.Interact,
                parameters,
                15.0 + ctx.Random.NextDouble() * 5.0,
                parameters.ToResultData(),
                new List<ResourceRequirement> { new ResourceRequirement(WaterResource) }
            ),
            baseScore,
            $"Swim (DC {baseDC}, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})"
        ));
    }

    public void ApplyEffects(EffectContext ctx)
    {
        if (ctx.Tier == null)
            return;

        var tier = ctx.Tier.Value;

        switch (tier)
        {
            case RollOutcomeTier.CriticalSuccess:
                ctx.Actor.Morale = Math.Min(100.0, ctx.Actor.Morale + 20.0);
                ctx.Actor.Energy = Math.Max(0.0, ctx.Actor.Energy - 5.0);
                ctx.Actor.Boredom = Math.Max(0.0, ctx.Actor.Boredom - 15.0);
                
                // Spawn treasure chest if not already present
                if (ctx.World.TreasureChest == null)
                {
                    var chest = new TreasureChestItem();
                    chest.IsOpened = false;
                    chest.Health = 100.0;
                    chest.Position = "shore";
                    ctx.World.WorldItems.Add(chest);
                    
                    if (ctx.Outcome.ResultData != null)
                    {
                        ctx.Outcome.ResultData["variant_id"] = "swim_crit_success_treasure";
                        ctx.Outcome.ResultData["encounter_type"] = "treasure_chest";
                    }
                }
                break;

            case RollOutcomeTier.Success:
                ctx.Actor.Morale = Math.Min(100.0, ctx.Actor.Morale + 10.0);
                ctx.Actor.Energy = Math.Max(0.0, ctx.Actor.Energy - 10.0);
                ctx.Actor.Boredom = Math.Max(0.0, ctx.Actor.Boredom - 10.0);
                break;

            case RollOutcomeTier.PartialSuccess:
                ctx.Actor.Morale = Math.Min(100.0, ctx.Actor.Morale + 3.0);
                ctx.Actor.Energy = Math.Max(0.0, ctx.Actor.Energy - 15.0);
                break;

            case RollOutcomeTier.Failure:
                ctx.Actor.Energy = Math.Max(0.0, ctx.Actor.Energy - 15.0);
                break;

            case RollOutcomeTier.CriticalFailure:
                ctx.Actor.Energy = Math.Max(0.0, ctx.Actor.Energy - 25.0);
                ctx.Actor.Morale = Math.Max(0.0, ctx.Actor.Morale - 15.0);
                
                // Spawn shark if not already present
                if (ctx.World.Shark == null)
                {
                    var duration = 60.0 + ctx.Rng.NextDouble() * 120.0; // 60-180 seconds
                    var shark = new SharkItem();
                    shark.ExpiresAt = ctx.World.CurrentTime + duration;
                    
                    // Try to reserve the water resource for the shark's lifetime
                    var utilityId = $"world_item:shark:{shark.Id}";
                    var reserved = ctx.Reservations.TryReserve(WaterResource, utilityId, shark.ExpiresAt);
                    
                    if (reserved)
                    {
                        // Successfully reserved - add shark to world
                        shark.ReservedResourceId = WaterResource;
                        ctx.World.WorldItems.Add(shark);
                        
                        // Additional morale penalty for shark encounter
                        ctx.Actor.Morale = Math.Max(0.0, ctx.Actor.Morale - 15.0);
                        
                        if (ctx.Outcome.ResultData != null)
                        {
                            ctx.Outcome.ResultData["variant_id"] = "swim_crit_failure_shark";
                            ctx.Outcome.ResultData["encounter_type"] = "shark";
                            ctx.Outcome.ResultData["shark_duration"] = duration;
                        }
                    }
                    // If reservation failed (water already reserved), shark is not spawned
                    // This could happen if multiple actors fail swimming simultaneously
                }
                break;
        }
    }
}
