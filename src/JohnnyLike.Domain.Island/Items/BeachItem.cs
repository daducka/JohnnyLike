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
        // Playful / recreational comfort actions — only when actor is in good condition
        AddHumToSelfCandidate(ctx, output);
        AddPaceBeachCandidate(ctx, output);
        AddCollectShellsCandidate(ctx, output);
        AddSitAndWatchWavesCandidate(ctx, output);
        AddSkipStonesCandidate(ctx, output);

        // Despair comfort actions — only when actor is suffering
        AddCurlInABallCandidate(ctx, output);
        AddStareAtSkyCandidate(ctx, output);
        AddReflectOnLifeCandidate(ctx, output);
        AddEatSandCandidate(ctx, output);

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
                Duration.Minutes(20.0, 30.0, ctx.Random),
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
                [QualityType.Preparation]        = 0.78,
                [QualityType.Efficiency]         = 0.12,
                [QualityType.ResourcePreservation] = 0.4
            },
            ActorRequirement: CandidateRequirements.AliveOnly
        ));
    }

    private void AddCurlInABallCandidate(IslandContext ctx, List<ActionCandidate> output)
    {
        var baseDC = 8;
        var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC);

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("curl_in_a_ball"),
                ActionKind.Interact,
                parameters,
                Duration.Minutes(10.0, 15.0, ctx.Random),
                "curl into a ball and withdraw from the world",
                parameters.ToResultData()
            ),
            0.16,
            Reason: $"Curl in a ball (Survival DC {baseDC}, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})",
            EffectHandler: new Action<EffectContext>(effectCtx =>
            {
                if (effectCtx.Tier == null)
                    return;

                var tier = effectCtx.Tier.Value;
                var actorName = effectCtx.ActorId.Value;

                switch (tier)
                {
                    case RollOutcomeTier.CriticalSuccess:
                        effectCtx.Actor.Health += 10.0;
                        effectCtx.Actor.Energy += 40.0;
                        effectCtx.SetOutcomeNarration($"Something shifts deep in {actorName}'s body. They slip into a sleep so still the world seems to stop — and wake hours later with the light changed, feeling something they hadn't expected: the faintest thread of hope.");
                        break;

                    case RollOutcomeTier.Success:
                        effectCtx.Actor.Energy += 10.0;
                        effectCtx.Actor.Morale += 2.0;
                        effectCtx.SetOutcomeNarration($"{actorName} uncurls slowly and forces themselves to sit up. Their body still aches, their stomach still gnaws, but something about lying still helped them hold together a little longer.");
                        break;

                    case RollOutcomeTier.PartialSuccess:
                        effectCtx.Actor.Energy += 5.0;
                        effectCtx.SetOutcomeNarration($"{actorName} uncurls and rises with heavy limbs, the brief rest leaving them no better than before — but no worse.");
                        break;

                    case RollOutcomeTier.Failure:
                        effectCtx.Actor.Morale -= 1.0;
                        effectCtx.SetOutcomeNarration($"{actorName} cannot rest. Their thoughts won't quiet. They eventually drag themselves back upright, as wretched as before.");
                        break;

                    case RollOutcomeTier.CriticalFailure:
                        effectCtx.Actor.Morale -= 3.0;
                        effectCtx.SetOutcomeNarration($"The silence presses in on {actorName} like something alive. Every minute curled on the sand makes it worse. They finally give up and stare into the distance, more desolate than before.");
                        break;
                }
            }),
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Rest]        = 0.8,
                [QualityType.Comfort]     = 0.6,
                [QualityType.Safety]      = 0.2,
                [QualityType.Fun]         = -0.2,
                [QualityType.Preparation] = -0.3
            },
            ActorRequirement: CandidateRequirements.DespairingOnly
        ));
    }

    private void AddStareAtSkyCandidate(IslandContext ctx, List<ActionCandidate> output)
    {
        var baseDC = 10;
        var parameters = ctx.RollSkillCheck(SkillType.Perception, baseDC);

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("stare_at_sky"),
                ActionKind.Interact,
                parameters,
                Duration.Minutes(10.0, 20.0, ctx.Random),
                "lie still and stare at the endless sky",
                parameters.ToResultData()
            ),
            0.14,
            Reason: $"Stare at sky (Perception DC {baseDC}, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})",
            EffectHandler: new Action<EffectContext>(effectCtx =>
            {
                if (effectCtx.Tier == null)
                    return;

                var tier = effectCtx.Tier.Value;
                var actorName = effectCtx.ActorId.Value;

                switch (tier)
                {
                    case RollOutcomeTier.CriticalSuccess:
                        effectCtx.Actor.Morale += 3.0;
                        var pile = effectCtx.World.SharedSupplyPile;
                        pile?.AddSupply(2.0, () => new Supply.CoconutSupply());
                        effectCtx.SetOutcomeNarration($"{actorName} is watching a large seabird circling overhead — and something drops from its talons into the sand nearby. {actorName} scrambles over and finds coconuts half-buried in the sand.");
                        break;

                    case RollOutcomeTier.Success:
                        effectCtx.Actor.Morale += 3.0;
                        effectCtx.SetOutcomeNarration($"{actorName} stares up at the shifting clouds for a long time. The vastness of the sky briefly drowns out the hunger and pain. They feel marginally less alone.");
                        break;

                    case RollOutcomeTier.PartialSuccess:
                        effectCtx.Actor.Morale += 1.0;
                        effectCtx.SetOutcomeNarration($"{actorName} lies on the warm sand and watches the clouds drift. The stillness is brief, but their mind quiets for just a moment.");
                        break;

                    case RollOutcomeTier.Failure:
                        effectCtx.SetOutcomeNarration($"{actorName} stares at the sky until the light hurts their eyes. Nothing happens. They roll onto their side feeling hollow.");
                        break;

                    case RollOutcomeTier.CriticalFailure:
                        effectCtx.Actor.Morale -= 2.0;
                        effectCtx.SetOutcomeNarration($"{actorName} lies staring upward as the minutes stretch out, thoughts filling with every mistake that led here. They sit up feeling worse than before.");
                        break;
                }
            }),
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Comfort] = 0.7,
                [QualityType.Rest]    = 0.2,
                [QualityType.Safety]  = 0.1,
                [QualityType.Fun]     = -0.1
            },
            ActorRequirement: CandidateRequirements.DespairingOnly
        ));
    }

    private void AddReflectOnLifeCandidate(IslandContext ctx, List<ActionCandidate> output)
    {
        var baseDC = 12;
        var parameters = ctx.RollSkillCheck(SkillType.Survival, baseDC);

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("reflect_on_life"),
                ActionKind.Interact,
                parameters,
                Duration.Minutes(10.0, 15.0, ctx.Random),
                "reflect quietly on life and how they ended up here",
                parameters.ToResultData()
            ),
            0.15,
            Reason: $"Reflect on life (Survival DC {baseDC}, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})",
            EffectHandler: new Action<EffectContext>(effectCtx =>
            {
                if (effectCtx.Tier == null)
                    return;

                var tier = effectCtx.Tier.Value;
                var actorName = effectCtx.ActorId.Value;

                switch (tier)
                {
                    case RollOutcomeTier.CriticalSuccess:
                        effectCtx.Actor.Morale += 75.0;
                        effectCtx.SetOutcomeNarration($"A sudden clarity breaks over {actorName} like cold water. Something about the silence and the horizon makes everything snap into focus. They are not ready to die here. They sit up straighter, jaw set.");
                        break;

                    case RollOutcomeTier.Success:
                        effectCtx.Actor.Morale += 5.0;
                        effectCtx.SetOutcomeNarration($"{actorName} spends a long time sitting with their thoughts, picking through the wreckage of how they came to be here. By the end they feel no better materially — but somewhere beneath the surface, a small resolve quietly steadies them.");
                        break;

                    case RollOutcomeTier.PartialSuccess:
                        effectCtx.Actor.Morale += 2.0;
                        effectCtx.SetOutcomeNarration($"{actorName} turns everything over in their mind. There is no answer, no resolution — but the act of sitting quietly with it, of not running, offers a small and fragile comfort.");
                        break;

                    case RollOutcomeTier.Failure:
                        effectCtx.SetOutcomeNarration($"{actorName}'s thoughts churn without landing anywhere useful. The quiet makes it worse. They sit there until the stillness feels like pressure, and stand up none the wiser.");
                        break;

                    case RollOutcomeTier.CriticalFailure:
                        effectCtx.Actor.Morale -= 5.0;
                        effectCtx.SetOutcomeNarration($"{actorName} cannot stop thinking. The same moments replay, each one making the situation feel more hopeless, less survivable. They finally stop, shaking slightly, feeling more lost than when they sat down.");
                        break;
                }
            }),
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Comfort]     = 0.5,
                [QualityType.Rest]        = 0.2,
                [QualityType.Safety]      = 0.2,
                [QualityType.Fun]         = -0.1,
                [QualityType.Preparation] = -0.1
            },
            ActorRequirement: CandidateRequirements.DespairingOnly
        ));
    }

    private void AddEatSandCandidate(IslandContext ctx, List<ActionCandidate> output)
    {
        var baseDC = 6;
        var parameters = ctx.RollSkillCheck(SkillType.Athletics, baseDC);

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("eat_sand"),
                ActionKind.Interact,
                parameters,
                Duration.Minutes(3.0, 5.0, ctx.Random),
                "hopelessly dig at the sand and eat a handful, hoping to find anything",
                parameters.ToResultData()
            ),
            0.08,
            Reason: $"Eat sand (Athletics DC {baseDC}, rolled {parameters.Result.Total}, {parameters.Result.OutcomeTier})",
            EffectHandler: new Action<EffectContext>(effectCtx =>
            {
                if (effectCtx.Tier == null)
                    return;

                var tier = effectCtx.Tier.Value;
                var actorName = effectCtx.ActorId.Value;

                switch (tier)
                {
                    case RollOutcomeTier.CriticalSuccess:
                        effectCtx.Actor.Satiety += 80.0;
                        effectCtx.SetOutcomeNarration($"{actorName}'s fingers scrape something hard and smooth just below the surface. Turtle eggs — a whole clutch of them, buried just above the tideline. {actorName} tears them open and eats greedily, hands trembling, barely tasting them.");
                        break;

                    case RollOutcomeTier.Success:
                        effectCtx.Actor.Satiety += 5.0;
                        effectCtx.SetOutcomeNarration($"{actorName} finds a small sand crab buried an inch down. They eat it whole without stopping to think about it. It barely registers as food, but the body counts what it can.");
                        break;

                    case RollOutcomeTier.PartialSuccess:
                        effectCtx.Actor.Satiety += 2.0;
                        effectCtx.SetOutcomeNarration($"{actorName} digs with shaking hands and eats a few palmfuls of wet sand. Nothing. They spit and spit and eventually swallow the rest.");
                        break;

                    case RollOutcomeTier.Failure:
                        effectCtx.Actor.Morale -= 2.0;
                        effectCtx.SetOutcomeNarration($"{actorName} sits eating sand by the handful, spitting out grit and shell fragments. Their stomach twists. This is what they have become.");
                        break;

                    case RollOutcomeTier.CriticalFailure:
                        effectCtx.Actor.Satiety -= 2.0;
                        effectCtx.Actor.Morale  -= 3.0;
                        effectCtx.SetOutcomeNarration($"{actorName} eats a mouthful of sand and swallows something sharp. They double over coughing. Their body rejects it, shaking and sick.");
                        break;
                }
            }),
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Comfort]         = 0.1,
                [QualityType.FoodConsumption] = 0.3,
                [QualityType.Safety]          = -0.1,
                [QualityType.Fun]             = -0.2
            },
            ActorRequirement: CandidateRequirements.DespairingOnly
        ));
    }

    private void AddHumToSelfCandidate(IslandContext ctx, List<ActionCandidate> output)
    {
        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("hum_to_self"),
                ActionKind.Wait,
                EmptyActionParameters.Instance,
                Duration.Minutes(5.0, 8.0, ctx.Random),
                NarrationDescription: "hum quietly to themselves"
            ),
            0.09,
            Reason: "Hum to self",
            EffectHandler: new Action<EffectContext>(effectCtx =>
            {
                var actor = effectCtx.ActorId.Value;
                effectCtx.Actor.Morale += 5.0;
                effectCtx.SetOutcomeNarration($"{actor} hums a soft tune under their breath, finding a little comfort in the rhythm.");
            }),
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Fun]        = 0.12,
                [QualityType.Comfort]    = 0.45,
                [QualityType.Efficiency] = -0.05,
                [QualityType.Safety]     = 0.05
            },
            ActorRequirement: CandidateRequirements.PlayfulOnly
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
                Duration.Minutes(15.0, 20.0, ctx.Random),
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
                [QualityType.Comfort]     = 0.35,
                [QualityType.Rest]        = -0.10,
                [QualityType.Safety]      = 0.05,
                [QualityType.Preparation] = 0.05
            },
            ActorRequirement: CandidateRequirements.PlayfulOnly
        ));
    }

    private void AddCollectShellsCandidate(IslandContext ctx, List<ActionCandidate> output)
    {
        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("collect_shells"),
                ActionKind.Interact,
                EmptyActionParameters.Instance,
                Duration.Minutes(10.0, 15.0, ctx.Random),
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
            ActorRequirement: CandidateRequirements.PlayfulOnly
        ));
    }

    private void AddSitAndWatchWavesCandidate(IslandContext ctx, List<ActionCandidate> output)
    {
        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("sit_and_watch_waves"),
                ActionKind.Wait,
                EmptyActionParameters.Instance,
                Duration.Minutes(10.0, 20.0, ctx.Random),
                NarrationDescription: "sit quietly and watch the waves roll in"
            ),
            0.10,
            Reason: "Sit and watch waves",
            EffectHandler: new Action<EffectContext>(effectCtx =>
            {
                var actor = effectCtx.ActorId.Value;
                effectCtx.Actor.Morale += 12.0;
                effectCtx.SetOutcomeNarration($"{actor} sits at the water's edge and watches the waves roll in and out, feeling the tension slowly ease.");
            }),
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Fun]        = 0.15,
                [QualityType.Comfort]    = 0.55,
                [QualityType.Efficiency] = -0.10,
                [QualityType.Safety]     = 0.10
            },
            ActorRequirement: CandidateRequirements.PlayfulOnly
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
                Duration.Minutes(8.0, 12.0, ctx.Random),
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
            ActorRequirement: CandidateRequirements.PlayfulOnly
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
