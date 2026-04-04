using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Metabolism;
using JohnnyLike.Domain.Island.Supply;
using JohnnyLike.Domain.Kit.Dice;
using System.Text.Json;

namespace JohnnyLike.Domain.Island.Items;

public class OceanItem : WorldItem, ITickableWorldItem, IIslandActionCandidate, ISupplyBounty
{
    public List<SupplyItem> BountySupplies { get; set; } = new() { new FishSupply(100) };
    public Dictionary<string, Dictionary<string, double>> ActiveReservations { get; } = new();
    private ISupplyBounty Bounty => this;
    public double FishRegenRatePerMinute { get; set; } = 5.0;
    private long _lastTick = 0;

    public OceanItem(string id = "ocean") : base(id, "ocean") { }

    public IEnumerable<string> GetDependencies() => new[] { "calendar" };

    public List<TraceEvent> Tick(long currentTick, WorldState worldState)
    {
        var dtTicks = currentTick - _lastTick;
        _lastTick = currentTick;
        var dtSeconds = (double)dtTicks / 20.0;

        var fish = Bounty.GetSupply<FishSupply>();
        if (fish != null)
        {
            var oldAmount = fish.Quantity;
            fish.Quantity = Math.Min(100.0, fish.Quantity + FishRegenRatePerMinute * (dtSeconds / 60.0));

            if (fish.Quantity - oldAmount >= 1.0)
            {
                return new List<TraceEvent>
                {
                    new TraceEvent(currentTick, null, "FishRegenerated", new Dictionary<string, object>
                    {
                        ["oldAvailable"] = Math.Round(oldAmount, 2),
                        ["newAvailable"] = Math.Round(fish.Quantity, 2),
                        ["regenerated"]  = Math.Round(fish.Quantity - oldAmount, 2)
                    })
                };
            }
        }
        return new List<TraceEvent>();
    }

    private static readonly ResourceId WaterResource = new("island:resource:water");

    // IIslandActionCandidate
    public void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
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
                new List<ResourceRequirement> { new ResourceRequirement(WaterResource) }
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
                        effectCtx.Actor.Morale += 20.0;
                        effectCtx.SetOutcomeNarration($"{actor} glides through the water effortlessly, feeling exhilarated.");

                        // Spawn treasure chest if not already present
                        if (effectCtx.World.TreasureChest == null)
                        {
                            var chest = new TreasureChestItem
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
                            var shark = new SharkItem
                            {
                                ExpiresAtTick = effectCtx.World.CurrentTick + (long)(duration * 20)
                            };

                            var utilityId = $"world_item:shark:{shark.Id}";
                            var reserved = effectCtx.Reservations.TryReserve(WaterResource, utilityId, shark.ExpiresAtTick);

                            if (reserved)
                            {
                                shark.ReservedResourceId = WaterResource;
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
                [QualityType.Fun]     = 0.8,
                [QualityType.Comfort] = 0.3,
                [QualityType.Safety]  = -0.5
            },
            ActorRequirement: CandidateRequirements.PlayfulOnly
        ));
    }

    public override Dictionary<string, object> SerializeToDict()
    {
        var dict = base.SerializeToDict();
        dict["FishRegenRatePerMinute"] = FishRegenRatePerMinute;
        dict["BountySupplies"] = BountySupplies.Select(s => s.SerializeToDict()).ToList();
        dict["LastTick"] = _lastTick;
        return dict;
    }

    public override void DeserializeFromDict(Dictionary<string, JsonElement> data)
    {
        base.DeserializeFromDict(data);
        if (data.TryGetValue("FishRegenRatePerMinute", out var rate))
            FishRegenRatePerMinute = rate.GetDouble();
        if (data.TryGetValue("LastTick", out var lt)) _lastTick = lt.GetInt64();
        if (data.TryGetValue("BountySupplies", out var bountyEl))
        {
            var list = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(bountyEl.GetRawText());
            if (list != null)
            {
                BountySupplies.Clear();
                foreach (var sd in list)
                {
                    var type = sd["Type"].GetString()!;
                    var id = sd["Id"].GetString()!;
                    SupplyItem? supply = type switch
                    {
                        "supply_fish" => new FishSupply(id),
                        _ => null
                    };
                    if (supply != null)
                    {
                        supply.DeserializeFromDict(sd);
                        BountySupplies.Add(supply);
                    }
                }
            }
        }
    }
}
