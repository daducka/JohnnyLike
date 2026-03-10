using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Supply;
using JohnnyLike.Domain.Island.Telemetry;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Items;

public enum TideLevel { Low, High }

public class BeachItem : WorldItem, ITickableWorldItem, IIslandActionCandidate, ISupplyBounty
{
    private static readonly ResourceId BeachResource = new("island:resource:beach");

    // ISupplyBounty — all method logic comes from the interface's default implementations
    public List<SupplyItem> BountySupplies { get; set; } = new()
    {
        new StickSupply(10),
        new WoodSupply(10),
        new RocksSupply(5)
    };

    public Dictionary<string, Dictionary<string, double>> ActiveReservations { get; } = new();

    // Shorthand so internal methods can call ISupplyBounty defaults without explicit casts
    private ISupplyBounty Bounty => this;

    public TideLevel Tide { get; private set; }

    public BeachItem(string id = "beach") : base(id, "beach") { }

    // ITickableWorldItem
    public IEnumerable<string> GetDependencies() => new[] { "calendar" };

    private long _lastTick = 0;

    public List<TraceEvent> Tick(long currentTick, WorldState worldState)
    {
        var world = (IslandWorldState)worldState;
        var dtTicks = currentTick - _lastTick;
        _lastTick = currentTick;
        var dtSeconds = (double)dtTicks / 20.0;

        var calendar = world.GetItem<CalendarItem>("calendar");
        var weather = world.GetItem<WeatherItem>("weather");

        if (calendar == null)
            return new List<TraceEvent>();

        var tidePhase = calendar.HourOfDay % 12;
        var prevTide = Tide;
        Tide = tidePhase >= 6 ? TideLevel.High : TideLevel.Low;

        if (Tide != prevTide)
        {
            var text = Tide == TideLevel.High
                ? "The tide turns, rising from low to high."
                : "The tide pulls back, exposing the lower beach.";
            using (world.Tracer.PushPhase(TracePhase.WorldTick))
                world.Tracer.BeatWorld(text, subjectId: "beach:tide", priority: 20);
        }

        double regenRate = 0.1;

        if (Tide == TideLevel.High)
            regenRate *= 2;

        if (weather?.Temperature == TemperatureBand.Cold)
            regenRate *= 1.2;

        Bounty.AddSupply(regenRate * dtSeconds, () => new StickSupply());
        Bounty.AddSupply(regenRate * dtSeconds, () => new WoodSupply());
        Bounty.AddSupply(regenRate * dtSeconds * 0.5, () => new RocksSupply());

        return new List<TraceEvent>();
    }

    // IIslandActionCandidate
    public void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        // Always offer morale / comfort actions from the beach
        AddHumToSelfCandidate(ctx, output);
        AddPaceBeachCandidate(ctx, output);
        AddCollectShellsCandidate(ctx, output);
        AddSitAndWatchWavesCandidate(ctx, output);
        AddSkipStonesCandidate(ctx, output);

        // Only offer explore_beach when there's enough bounty to get at least a partial result
        var sticks = Bounty.GetQuantity<StickSupply>();
        var wood = Bounty.GetQuantity<WoodSupply>();
        if (sticks < 2.0 || wood < 2.0)
            return;

        var baseDC = Tide == TideLevel.High ? 12 : 8;
        var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC);

        // Both lambdas capture the same variable so the reservation key survives the
        // Director's resource-release call that happens before ApplyActionEffects.
        BountyCollectionContext? bountyCtx = null;
        ISupplyBounty source = this;
        var actorKey = ctx.ActorId.Value;

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("explore_beach"),
                ActionKind.Interact,
                parameters,
                EngineConstants.TimeToTicks(20.0, 30.0, ctx.Random),
                "explore the beach for useful materials",
                parameters.ToResultData(),
                new List<ResourceRequirement> { new ResourceRequirement(BeachResource) }
            ),
            0.22,
            Reason: $"Explore beach (sticks: {sticks:F0}, wood: {wood:F0}, DC {baseDC}, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})",
            PreAction: new Func<EffectContext, bool>(_ =>
            {
                // Reserve the MAX possible payout (CriticalSuccess upper-bound) so other actors
                // see reduced availability. The actual payout is committed in the EffectHandler.
                var availSticks = source.GetQuantity<StickSupply>();
                var availWood = source.GetQuantity<WoodSupply>();
                if (availSticks < 1.0 || availWood < 1.0) return false;

                source.ReserveSupply<StickSupply>(actorKey, Math.Min(availSticks, 4.0));
                source.ReserveSupply<WoodSupply>(actorKey, Math.Min(availWood, 4.0));
                var availRocks = source.GetQuantity<RocksSupply>();
                if (availRocks > 0)
                    source.ReserveSupply<RocksSupply>(actorKey, Math.Min(availRocks, 2.0));

                bountyCtx = new BountyCollectionContext(source, actorKey);
                return true;
            }),
            EffectHandler: new Action<EffectContext>(effectCtx =>
            {
                if (effectCtx.Tier == null || bountyCtx == null)
                {
                    bountyCtx?.Source.ReleaseReservation(bountyCtx.ReservationKey);
                    return;
                }

                var tier = effectCtx.Tier.Value;
                var src = bountyCtx.Source;
                var key = bountyCtx.ReservationKey;
                var pile = effectCtx.World.SharedSupplyPile;
                if (pile == null) { src.ReleaseReservation(key); return; }

                var actor = effectCtx.ActorId.Value;
                switch (tier)
                {
                    case RollOutcomeTier.CriticalSuccess:
                        src.CommitReservation<StickSupply>(key, 4.0, pile, () => new StickSupply());
                        src.CommitReservation<WoodSupply>(key, 4.0, pile, () => new WoodSupply());
                        src.CommitReservation<RocksSupply>(key, 2.0, pile, () => new RocksSupply());
                        effectCtx.Actor.Morale += 8.0;
                        effectCtx.Actor.Energy -= 8.0;
                        if (effectCtx.Rng.NextDouble() < 0.15)
                        {
                            pile.AddSupply(1.0, () => new CarcassScrapsSupply());
                            effectCtx.SetOutcomeNarration($"{actor} returns from the beach arms full of driftwood, sticks, and rocks — and finds some leftover fish scraps washed ashore.");
                        }
                        else
                        {
                            effectCtx.SetOutcomeNarration($"{actor} returns from the beach arms full of driftwood, sticks, and rocks.");
                        }
                        break;

                    case RollOutcomeTier.Success:
                        src.CommitReservation<StickSupply>(key, 2.0, pile, () => new StickSupply());
                        src.CommitReservation<WoodSupply>(key, 2.0, pile, () => new WoodSupply());
                        src.CommitReservation<RocksSupply>(key, 1.0, pile, () => new RocksSupply());
                        effectCtx.Actor.Morale += 5.0;
                        effectCtx.Actor.Energy -= 10.0;
                        if (effectCtx.Rng.NextDouble() < 0.15)
                        {
                            pile.AddSupply(1.0, () => new CarcassScrapsSupply());
                            effectCtx.SetOutcomeNarration($"{actor} picks through the tideline and gathers a useful armful of materials, also spotting some leftover fish scraps washed ashore.");
                        }
                        else
                        {
                            effectCtx.SetOutcomeNarration($"{actor} picks through the tideline and gathers a useful armful of materials.");
                        }
                        break;

                    case RollOutcomeTier.PartialSuccess:
                        src.CommitReservation<StickSupply>(key, 1.0, pile, () => new StickSupply());
                        src.CommitReservation<WoodSupply>(key, 1.0, pile, () => new WoodSupply());
                        src.ReleaseReservation(key); // return reserved rocks (not committed at this tier)
                        effectCtx.Actor.Morale += 2.0;
                        effectCtx.Actor.Energy -= 12.0;
                        effectCtx.SetOutcomeNarration($"{actor} finds a few sticks and a bit of wood, but not much else.");
                        break;

                    default: // Failure or CriticalFailure: everything returned
                        src.ReleaseReservation(key);
                        effectCtx.Actor.Energy -= tier == RollOutcomeTier.Failure ? 12.0 : 15.0;
                        if (tier == RollOutcomeTier.CriticalFailure)
                            effectCtx.Actor.Morale -= 5.0;
                        effectCtx.SetOutcomeNarration(
                            tier == RollOutcomeTier.CriticalFailure
                                ? $"{actor} searches the beach for an hour and comes back empty-handed and discouraged."
                                : $"{actor} scours the beach but finds nothing useful.");
                        break;
                }
            }),
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Preparation] = 0.6,
                [QualityType.ResourcePreservation] = 0.4
            },
            ActorRequirement: CandidateRequirements.AliveOnly
        ));
    }

    private void AddHumToSelfCandidate(IslandContext ctx, List<ActionCandidate> output)
    {
        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("hum_to_self"),
                ActionKind.Wait,
                EmptyActionParameters.Instance,
                EngineConstants.TimeToTicks(5.0, 8.0, ctx.Random),
                NarrationDescription: "hum quietly to themselves"
            ),
            0.10,
            Reason: "Hum to self",
            EffectHandler: new Action<EffectContext>(effectCtx =>
            {
                var actor = effectCtx.ActorId.Value;
                effectCtx.Actor.Morale += 5.0;
                effectCtx.SetOutcomeNarration($"{actor} hums a soft tune under their breath, finding a little comfort in the rhythm.");
            }),
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Fun]        = 0.20,
                [QualityType.Comfort]    = 0.70,
                [QualityType.Efficiency] = -0.05,
                [QualityType.Safety]     = 0.05
            },
            ActorRequirement: CandidateRequirements.AliveOnly
        ));
    }

    private void AddPaceBeachCandidate(IslandContext ctx, List<ActionCandidate> output)
    {
        var baseDC = 10;
        var parameters = ctx.RollSkillCheck(SkillType.Perception, baseDC);

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("pace_beach"),
                ActionKind.Interact,
                parameters,
                EngineConstants.TimeToTicks(15.0, 20.0, ctx.Random),
                "pace along the beach to clear their mind",
                parameters.ToResultData()
            ),
            0.14,
            Reason: $"Pace beach (Perception DC {baseDC}, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})",
            EffectHandler: new Action<EffectContext>(effectCtx =>
            {
                if (effectCtx.Tier == null)
                    return;

                var tier = effectCtx.Tier.Value;
                var actor = effectCtx.ActorId.Value;
                effectCtx.Actor.Morale += 8.0;
                effectCtx.Actor.Energy -= 5.0;

                var pile = effectCtx.World.SharedSupplyPile;

                switch (tier)
                {
                    case RollOutcomeTier.CriticalSuccess:
                        if (pile != null)
                            pile.AddSupply(1.0, () => new Supply.WoodSupply());
                        effectCtx.SetOutcomeNarration($"{actor} paces the tideline with a clear eye — and spots a solid piece of driftwood half-buried in the sand.");
                        break;

                    case RollOutcomeTier.Success:
                        if (pile != null)
                            pile.AddSupply(1.0, () => new Supply.ShellSupply());
                        effectCtx.SetOutcomeNarration($"{actor} wanders the shore, thoughts slowly settling — and notices a cluster of shells worth picking up.");
                        break;

                    case RollOutcomeTier.PartialSuccess:
                        if (pile != null)
                            pile.AddSupply(0.5, () => new Supply.RopeSupply());
                        effectCtx.SetOutcomeNarration($"{actor} paces the beach restlessly, then pauses — a frayed rope fragment is tangled in the seaweed.");
                        break;

                    default:
                        effectCtx.SetOutcomeNarration($"{actor} paces the length of the beach slowly, letting the sound of the waves quiet their thoughts.");
                        break;
                }
            }),
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Fun]         = 0.25,
                [QualityType.Comfort]     = 0.45,
                [QualityType.Rest]        = -0.10,
                [QualityType.Safety]      = 0.05,
                [QualityType.Preparation] = 0.05
            },
            ActorRequirement: CandidateRequirements.AliveOnly
        ));
    }

    private void AddCollectShellsCandidate(IslandContext ctx, List<ActionCandidate> output)
    {
        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("collect_shells"),
                ActionKind.Interact,
                EmptyActionParameters.Instance,
                EngineConstants.TimeToTicks(10.0, 15.0, ctx.Random),
                NarrationDescription: "collect shells along the shoreline"
            ),
            0.16,
            Reason: "Collect shells",
            EffectHandler: new Action<EffectContext>(effectCtx =>
            {
                var actor = effectCtx.ActorId.Value;
                effectCtx.Actor.Morale += 10.0;

                var pile = effectCtx.World.SharedSupplyPile;
                if (pile != null)
                {
                    pile.AddSupply(2.0, () => new Supply.ShellSupply());
                    effectCtx.SetOutcomeNarration($"{actor} wanders along the waterline, picking up a small handful of shells. There is something satisfying about the search.");
                }
                else
                {
                    effectCtx.SetOutcomeNarration($"{actor} wanders along the waterline, idly collecting shells and letting the activity settle their nerves.");
                }
            }),
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Fun]         = 0.45,
                [QualityType.Comfort]     = 0.20,
                [QualityType.Preparation] = 0.10,
                [QualityType.Efficiency]  = -0.05,
                [QualityType.Safety]      = 0.05
            },
            ActorRequirement: CandidateRequirements.AliveOnly
        ));
    }

    private void AddSitAndWatchWavesCandidate(IslandContext ctx, List<ActionCandidate> output)
    {
        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("sit_and_watch_waves"),
                ActionKind.Wait,
                EmptyActionParameters.Instance,
                EngineConstants.TimeToTicks(10.0, 20.0, ctx.Random),
                NarrationDescription: "sit quietly and watch the waves roll in"
            ),
            0.12,
            Reason: "Sit and watch waves",
            EffectHandler: new Action<EffectContext>(effectCtx =>
            {
                var actor = effectCtx.ActorId.Value;
                effectCtx.Actor.Morale += 12.0;
                effectCtx.SetOutcomeNarration($"{actor} sits at the water's edge and watches the waves roll in and out, feeling the tension slowly ease.");
            }),
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Fun]        = 0.25,
                [QualityType.Comfort]    = 0.80,
                [QualityType.Efficiency] = -0.10,
                [QualityType.Safety]     = 0.10
            },
            ActorRequirement: CandidateRequirements.AliveOnly
        ));
    }

    private void AddSkipStonesCandidate(IslandContext ctx, List<ActionCandidate> output)
    {
        var baseDC = 6;
        var parameters = ctx.RollSkillCheck(SkillType.Athletics, baseDC);

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("skip_stones"),
                ActionKind.Interact,
                parameters,
                EngineConstants.TimeToTicks(8.0, 12.0, ctx.Random),
                "skip stones across the water",
                parameters.ToResultData()
            ),
            0.15,
            Reason: $"Skip stones (DC {baseDC}, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})",
            EffectHandler: new Action<EffectContext>(effectCtx =>
            {
                if (effectCtx.Tier == null)
                    return;

                var tier = effectCtx.Tier.Value;
                var actor = effectCtx.ActorId.Value;
                effectCtx.Actor.Energy -= 3.0;

                switch (tier)
                {
                    case RollOutcomeTier.CriticalSuccess:
                        effectCtx.Actor.Morale += 20.0;
                        effectCtx.SetOutcomeNarration($"{actor} finds the perfect flat stone and sends it dancing across the water — six skips! The small triumph brings a genuine smile.");
                        break;

                    case RollOutcomeTier.Success:
                        effectCtx.Actor.Morale += 12.0;
                        effectCtx.SetOutcomeNarration($"{actor} flicks a stone sidearm and watches it skip cleanly three times before vanishing. Not bad at all.");
                        break;

                    case RollOutcomeTier.PartialSuccess:
                        effectCtx.Actor.Morale += 6.0;
                        effectCtx.SetOutcomeNarration($"{actor} skips a stone once or twice before it plops under. Good enough for a small lift in mood.");
                        break;

                    case RollOutcomeTier.Failure:
                        effectCtx.Actor.Morale += 2.0;
                        effectCtx.SetOutcomeNarration($"{actor} throws a stone that sinks immediately. A short laugh escapes at the futility of it.");
                        break;

                    case RollOutcomeTier.CriticalFailure:
                        effectCtx.SetOutcomeNarration($"{actor} hurls a stone toward the waves, but it plops straight down right at the shoreline. A sheepish shrug follows.");
                        break;
                }
            }),
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Fun]         = 0.60,
                [QualityType.Comfort]     = 0.15,
                [QualityType.Preparation] = 0.05,
                [QualityType.Rest]        = -0.05
            },
            ActorRequirement: CandidateRequirements.AliveOnly
        ));
    }

    public override Dictionary<string, object> SerializeToDict()
    {
        var dict = base.SerializeToDict();
        dict["Tide"] = Tide.ToString();
        dict["BountySupplies"] = BountySupplies.Select(s => s.SerializeToDict()).ToList();
        dict["LastTick"] = _lastTick;
        return dict;
    }

    public override void DeserializeFromDict(Dictionary<string, System.Text.Json.JsonElement> data)
    {
        base.DeserializeFromDict(data);
        if (data.TryGetValue("Tide", out var tideEl))
            Tide = Enum.Parse<TideLevel>(tideEl.GetString()!);
        if (data.TryGetValue("LastTick", out var lt)) _lastTick = lt.GetInt64();
        if (data.TryGetValue("BountySupplies", out var bountyEl))
        {
            var list = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, System.Text.Json.JsonElement>>>(bountyEl.GetRawText());
            if (list != null)
            {
                BountySupplies.Clear();
                foreach (var sd in list)
                {
                    var type = sd["Type"].GetString()!;
                    var id = sd["Id"].GetString()!;
                    SupplyItem? supply = type switch
                    {
                        "supply_stick"  => new StickSupply(id),
                        "supply_wood"   => new WoodSupply(id),
                        "supply_rocks"  => new RocksSupply(id),
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
