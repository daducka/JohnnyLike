using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Kit.Dice;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Island.Metabolism;
using JohnnyLike.Domain.Island.Supply;
using JohnnyLike.Domain.Island.Vitality;

namespace JohnnyLike.Domain.Island;

public class IslandDomainPack : IDomainPack
{
    public string DomainName => "Island";

    private static readonly IReadOnlyDictionary<ActorId, ActorState> EmptyActors =
        new Dictionary<ActorId, ActorState>();

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

        // Derive DecisionPragmatism from personality traits unless the actor data
        // explicitly overrides it (e.g., for deterministic scripted characters).
        if (initialData?.ContainsKey("DecisionPragmatism") == true)
        {
            state.DecisionPragmatism = Convert.ToDouble(initialData["DecisionPragmatism"]);
        }
        else
        {
            var traits = DerivePersonalityTraits(state);
            state.DecisionPragmatism = DeriveDecisionPragmatism(traits).FinalDecisionPragmatism;
        }

        // Every actor always carries a MetabolicBuff that drives Satiety/Energy changes
        // each world tick.  It never expires; intensity is updated by PreAction/ApplyActionEffects.
        state.ActiveBuffs.Add(new MetabolicBuff
        {
            Name         = "Metabolism",
            Type         = BuffType.Metabolic,
            Intensity    = MetabolicIntensity.Light,
            ExpiresAtTick = long.MaxValue
        });

        // VitalityBuff drives health deterioration (starvation/exhaustion/psyche strain) and
        // slow recovery.  It never expires and ticks with the world.
        state.ActiveBuffs.Add(new VitalityBuff
        {
            Name          = "Vitality",
            Type          = BuffType.Vitality,
            ExpiresAtTick = long.MaxValue
        });

        // Every actor starts alive.  Candidate requirements check this buff to gate actions
        // on actor condition.  Future health/death systems will mutate this buff's State.
        state.ActiveBuffs.Add(new AlivenessBuff
        {
            Name          = "Aliveness",
            Type          = BuffType.Aliveness,
            State         = AlivenessState.Alive,
            ExpiresAtTick = long.MaxValue
        });

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

        // Filter candidates whose actor requirement fails before scoring.
        // This removes impossible actions from the choice set entirely rather than
        // merely scoring them low.
        candidates.RemoveAll(c => c.ActorRequirement != null && !c.ActorRequirement(islandActorState));

        // Post-pass: compute final Score from IntrinsicScore and Quality weights
        var model = BuildQualityModel(ctx.Actor, ctx.NowTick);
        for (var i = 0; i < candidates.Count; i++)
        {
            candidates[i] = candidates[i] with { Score = ScoreCandidate(candidates[i], model) };
        }

        return candidates;
    }

    // ── Health-pressure tuning constants ──────────────────────────────────────────
    // These class-level constants control how strongly low health (injuryPressure)
    // shifts decision weights. They are shared between BuildQualityModel and
    // ExplainCandidateScoring so both methods stay in sync.

    /// <summary>Safety need urgency per point of injuryPressure. Max +2.5 at 0 HP.</summary>
    private const double InjurySafetyNeedScale   = 0.025;
    /// <summary>Rest need urgency per point of injuryPressure (stacks with fatigue). Max +1.0 at 0 HP.</summary>
    private const double InjuryRestNeedScale     = 0.010;
    /// <summary>Comfort need urgency per point of injuryPressure (stacks with misery). Max +0.5 at 0 HP.</summary>
    private const double InjuryComfortNeedScale  = 0.005;

    /// <summary>Minimum multiplier for Fun personality at 0 HP (suppressed to 15%).</summary>
    private const double InjuryFunSuppressionFloor         = 0.15;
    /// <summary>Minimum multiplier for Mastery personality at 0 HP (suppressed to 30%).</summary>
    private const double InjuryMasterySuppressionFloor     = 0.30;
    /// <summary>Minimum multiplier for Preparation personality at 0 HP (suppressed to 40%).</summary>
    private const double InjuryPreparationSuppressionFloor = 0.40;

    // ── Need pressure scale constants ─────────────────────────────────────────────
    // Control how strongly each stat deficit translates into a need-quality weight.
    // All pressures are in [0, 100], so these scales keep derived weights comparable.

    /// <summary>Scales fatigue pressure (100 − Energy) into Rest need urgency. Max +1.5 at Energy=0.</summary>
    private const double FatiguePressureRestScale  = 0.015;
    /// <summary>Scales misery pressure (100 − Morale) into Comfort need urgency. Max +1.0 at Morale=0.</summary>
    private const double MiseryPressureComfortScale = 0.01;

    // ── Personality base weight scales ─────────────────────────────────────────────
    // Control how strongly each trait pair contributes to its corresponding quality.
    // Traits are normalised [0,1], so these scales set the practical weight ceiling.

    /// <summary>Planner + Industrious traits → Preparation personality weight.</summary>
    private const double PersonalityPreparationScale = 0.7;
    /// <summary>Planner + Craftsman traits → Efficiency personality weight.</summary>
    private const double PersonalityEfficiencyScale  = 0.6;
    /// <summary>Craftsman + Industrious traits → Mastery personality weight.</summary>
    private const double PersonalityMasteryScale     = 0.6;
    /// <summary>Hedonist trait → Comfort personality weight.</summary>
    private const double PersonalityComfortScale     = 0.4;
    /// <summary>Survivor trait → Safety personality weight.</summary>
    private const double PersonalitySafetyScale      = 0.3;
    /// <summary>Instinctive + Hedonist traits → FoodConsumption personality weight.</summary>
    private const double PersonalityFoodScale         = 0.2;

    // ── DecisionPragmatism derivation constants ─────────────────────────────────────
    // Used in DeriveDecisionPragmatism to compute a personality-driven pragmatism baseline.
    // Final value is clamped to [PragmatismMin, PragmatismMax] so no actor goes fully
    // random or fully deterministic by personality alone.

    /// <summary>Base pragmatism before personality adjustments.</summary>
    private const double PragmatismBase             = 0.80;
    /// <summary>Planner trait contribution toward higher pragmatism (exploit).</summary>
    private const double PragmatismPlannerScale     = 0.10;
    /// <summary>Survivor trait contribution toward higher pragmatism (exploit).</summary>
    private const double PragmatismSurvivorScale    = 0.05;
    /// <summary>Hedonist trait contribution toward lower pragmatism (explore).</summary>
    private const double PragmatismHedonistScale    = 0.06;
    /// <summary>Instinctive trait contribution toward lower pragmatism (explore).</summary>
    private const double PragmatismInstinctiveScale = 0.04;
    /// <summary>Minimum derived DecisionPragmatism — keeps actors coherent even at max spontaneity.</summary>
    private const double PragmatismMin              = 0.65;
    /// <summary>Maximum derived DecisionPragmatism — keeps explore branch reachable.</summary>
    private const double PragmatismMax              = 0.98;

    // ── Preparation time-pressure constants ─────────────────────────────────────────
    // A bounded ramp that makes planning more salient the longer the actor survives.
    // Plateaus at PrepTimePressureCap so it never overpowers other signals.

    /// <summary>Maximum bounded preparation urgency added by time-on-island pressure.</summary>
    private const double PrepTimePressureCap         = 0.20;
    /// <summary>Preparation urgency gained per in-sim day stranded.</summary>
    private const double PrepTimePressureRatePerDay  = 0.05;

    // ── Hunger ramp thresholds and slopes ─────────────────────────────────────────
    // The staged hunger ramp builds urgency only below certain Satiety thresholds
    // so actors do not seek food when already satisfied.
    // Bands: >= SatietyRampMild (none), Mild, Moderate, Strong.

    /// <summary>Satiety at or above which hunger urgency is zero; also the top of the mild urgency band.</summary>
    private const double SatietyRampMild     = 70.0;
    /// <summary>Satiety below which moderate urgency begins (ramp from HungerMildMax → HungerMildMax+HungerModerateRange).</summary>
    private const double SatietyRampModerate = 50.0;
    /// <summary>Satiety below which strong urgency begins (ramp from HungerMildMax+HungerModerateRange upwards).</summary>
    private const double SatietyRampStrong   = 30.0;
    /// <summary>Maximum hunger urgency in the mild band (Satiety 50–70).</summary>
    private const double HungerMildMax       = 0.3;
    /// <summary>Additional hunger urgency added across the moderate band (Satiety 30–50).</summary>
    private const double HungerModerateRange = 1.2;
    /// <summary>Additional hunger urgency added across the strong band (Satiety 0–30).</summary>
    private const double HungerStrongRange   = 0.5;

    // ── Mood multiplier suppression constants ─────────────────────────────────────
    // These suppress longer-horizon personality tendencies when the actor's state is critical,
    // so survival instinct takes over.

    /// <summary>Satiety threshold below which the actor is considered starving (suppresses Preparation to PrepStarvationFloor).</summary>
    private const double StarvatingSatietyThreshold   = 20.0;
    /// <summary>Preparation multiplier floor when actor is starving.</summary>
    private const double PrepStarvationFloor          = 0.3;
    /// <summary>Energy threshold below which the actor is considered exhausted (suppresses Mastery to MasteryExhaustionFloor).</summary>
    private const double ExhaustedEnergyThreshold     = 20.0;
    /// <summary>Mastery multiplier floor when actor is exhausted.</summary>
    private const double MasteryExhaustionFloor       = 0.4;
    /// <summary>Base Fun multiplier scale — keeps fun weight below 0.6 even at maximum misery.</summary>
    private const double FunBaseScale                 = 0.6;
    /// <summary>Critical-survival Fun suppression: reduces Fun weight to 35% when starving or exhausted.</summary>
    private const double FunCriticalSurvivalScale     = 0.35;
    /// <summary>Satiety threshold below which critical-survival Fun suppression activates.</summary>
    private const double FunCriticalSatietyThreshold  = 25.0;
    /// <summary>Energy threshold below which critical-survival Fun suppression activates.</summary>
    private const double FunCriticalEnergyThreshold   = 20.0;

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

    /// <summary>
    /// Six derived personality traits computed from an actor's core stats.
    /// Each trait is normalised to [0,1] using Norm(a,b) = Clamp((a+b-20)/20, 0, 1).
    /// </summary>
    private sealed record PersonalityTraits(
        double Planner,      // INT + WIS  → prefers preparation, efficiency
        double Craftsman,    // DEX + INT  → prefers crafting, mastery
        double Survivor,     // CON + WIS  → prefers safety, sustainability
        double Hedonist,     // CHA + CON  → prefers comfort, morale
        double Instinctive,  // STR + CHA  → prefers immediate reward
        double Industrious,  // STR + DEX  → prefers building, working
        int STR, int DEX, int CON, int INT, int WIS, int CHA);

    /// <summary>
    /// Per-quality personality contribution: the formula used, the traits that contributed
    /// (with their values), and the resulting personalityBase value.
    /// </summary>
    private sealed record QualityPersonalityEntry(
        string Formula,
        Dictionary<string, double> Contributors,
        double PersonalityBase);

    /// <summary>
    /// Decomposed breakdown of a personality-derived DecisionPragmatism value.
    /// Each contribution field holds the signed delta applied to the base.
    /// </summary>
    private sealed record PragmatismBreakdown(
        double Base,
        double PlannerContribution,
        double SurvivorContribution,
        double HedonistContribution,
        double InstinctiveContribution,
        double FinalDecisionPragmatism);

    /// <summary>
    /// Derives the six personality traits from an actor's core ability scores.
    /// This is the single source of truth for trait derivation used by both
    /// <see cref="BuildQualityModel"/> and <see cref="ExplainCandidateScoring"/>.
    /// </summary>
    private static PersonalityTraits DerivePersonalityTraits(IslandActorState actor)
    {
        static double Norm(int a, int b) => Math.Clamp(((double)(a + b) - 20.0) / 20.0, 0.0, 1.0);
        return new PersonalityTraits(
            Planner:     Norm(actor.INT, actor.WIS),
            Craftsman:   Norm(actor.DEX, actor.INT),
            Survivor:    Norm(actor.CON, actor.WIS),
            Hedonist:    Norm(actor.CHA, actor.CON),
            Instinctive: Norm(actor.STR, actor.CHA),
            Industrious: Norm(actor.STR, actor.DEX),
            STR: actor.STR, DEX: actor.DEX, CON: actor.CON,
            INT: actor.INT, WIS: actor.WIS, CHA: actor.CHA);
    }

    /// <summary>
    /// Derives a personality-based DecisionPragmatism value from pre-computed traits.
    /// Planners and survivors tend toward exploitation (higher pragmatism);
    /// hedonists and instinctive actors tend toward exploration (lower pragmatism).
    /// Result is clamped to [<see cref="PragmatismMin"/>, <see cref="PragmatismMax"/>].
    /// </summary>
    private static PragmatismBreakdown DeriveDecisionPragmatism(PersonalityTraits t)
    {
        var plannerContrib     =  t.Planner     * PragmatismPlannerScale;
        var survivorContrib    =  t.Survivor    * PragmatismSurvivorScale;
        var hedonistContrib    =  t.Hedonist    * PragmatismHedonistScale;
        var instinctiveContrib =  t.Instinctive * PragmatismInstinctiveScale;
        var raw   = PragmatismBase + plannerContrib + survivorContrib - hedonistContrib - instinctiveContrib;
        var final = Math.Clamp(raw, PragmatismMin, PragmatismMax);
        return new PragmatismBreakdown(
            PragmatismBase,
            plannerContrib,
            survivorContrib,
            hedonistContrib,
            instinctiveContrib,
            final);
    }

    /// <summary>
    /// Builds the per-quality personality breakdown — formula, contributors, and personalityBase —
    /// from a pre-computed <see cref="PersonalityTraits"/> snapshot.
    /// This is the single source of truth for personalityBase values used by both
    /// <see cref="BuildQualityModel"/> and <see cref="ExplainCandidateScoring"/>.
    /// </summary>
    private static Dictionary<QualityType, QualityPersonalityEntry> BuildQualityPersonalityBreakdown(
        PersonalityTraits t)
    {
        return new Dictionary<QualityType, QualityPersonalityEntry>
        {
            [QualityType.Preparation]     = new(
                Formula:       $"(planner + industrious) * {PersonalityPreparationScale}",
                Contributors:  new() { ["planner"] = t.Planner, ["industrious"] = t.Industrious },
                PersonalityBase: (t.Planner + t.Industrious) * PersonalityPreparationScale),
            [QualityType.Efficiency]      = new(
                Formula:       $"(planner + craftsman) * {PersonalityEfficiencyScale}",
                Contributors:  new() { ["planner"] = t.Planner, ["craftsman"] = t.Craftsman },
                PersonalityBase: (t.Planner + t.Craftsman) * PersonalityEfficiencyScale),
            [QualityType.Mastery]         = new(
                Formula:       $"(craftsman + industrious) * {PersonalityMasteryScale}",
                Contributors:  new() { ["craftsman"] = t.Craftsman, ["industrious"] = t.Industrious },
                PersonalityBase: (t.Craftsman + t.Industrious) * PersonalityMasteryScale),
            [QualityType.Comfort]         = new(
                Formula:       $"hedonist * {PersonalityComfortScale}",
                Contributors:  new() { ["hedonist"] = t.Hedonist },
                PersonalityBase: t.Hedonist * PersonalityComfortScale),
            [QualityType.Safety]          = new(
                Formula:       $"survivor * {PersonalitySafetyScale}",
                Contributors:  new() { ["survivor"] = t.Survivor },
                PersonalityBase: t.Survivor * PersonalitySafetyScale),
            [QualityType.FoodConsumption] = new(
                Formula:       $"(instinctive + hedonist) * {PersonalityFoodScale}",
                Contributors:  new() { ["instinctive"] = t.Instinctive, ["hedonist"] = t.Hedonist },
                PersonalityBase: (t.Instinctive + t.Hedonist) * PersonalityFoodScale),
            [QualityType.Fun]             = new(
                Formula:       "1.0",
                Contributors:  new(),
                PersonalityBase: 1.0)
        };
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

    private static QualityModel BuildQualityModel(IslandActorState actor, long currentTick = 0L)
    {
        // ── Actor Stats ───────────────────────────────────────────────────────────
        // Dynamic physiological / psychological state.
        // Each deficit becomes a "pressure" that adds weight to a need quality.

        var fatiguePressure = 100.0 - actor.Energy;    // → Rest
        var miseryPressure  = 100.0 - actor.Morale;    // → Comfort
        var injuryPressure  = 100.0 - actor.Health;    // → Safety, Rest, Comfort

        // ── Staged hunger ramp ────────────────────────────────────────────────────
        // Hunger urgency only builds meaningfully below certain satiety thresholds
        // so actors don't seek food when already satisfied.
        //   Satiety >= SatietyRampMild     : ~0    (no urgency)
        //   Satiety SatietyRampModerate–SatietyRampMild :  0 → HungerMildMax  (very mild)
        //   Satiety SatietyRampStrong–SatietyRampModerate:  HungerMildMax → HungerMildMax+HungerModerateRange (moderate)
        //   Satiety  0–SatietyRampStrong   :  HungerMildMax+HungerModerateRange → ... (strong)
        double stagedHungerNeed;
        if (actor.Satiety >= SatietyRampMild)
            stagedHungerNeed = 0.0;
        else if (actor.Satiety >= SatietyRampModerate)
            stagedHungerNeed = (SatietyRampMild - actor.Satiety) / (SatietyRampMild - SatietyRampModerate) * HungerMildMax;
        else if (actor.Satiety >= SatietyRampStrong)
            stagedHungerNeed = HungerMildMax + (SatietyRampModerate - actor.Satiety) / (SatietyRampModerate - SatietyRampStrong) * HungerModerateRange;
        else
            stagedHungerNeed = (HungerMildMax + HungerModerateRange) + (SatietyRampStrong - actor.Satiety) / SatietyRampStrong * HungerStrongRange;

        // ── Traits ────────────────────────────────────────────────────────────────
        // Derived via DerivePersonalityTraits (shared with ExplainCandidateScoring).
        var traits = DerivePersonalityTraits(actor);

        // Normalised injury factor [0,1]: 0 = healthy, 1 = 0 HP.
        // Health-pressure scale constants are defined at class level (InjurySafetyNeedScale etc.)
        // so they are shared with ExplainCandidateScoring.
        var injuryFactor = injuryPressure / 100.0;

        // ── Qualities ─────────────────────────────────────────────────────────────
        // Labels on actions describing what they provide:
        // FoodConsumption, Rest, Comfort, Fun, Safety,
        // Preparation, Efficiency, Mastery, ResourcePreservation

        // Need pressures drive urgency directly (additive, independent of personality).
        // Scale factors keep pressures comparable across the 0-100 range.
        // Health injury adds to Safety, Rest, and Comfort needs independently of fatigue/misery.

        // Bounded preparation time-pressure: ramps gently over the first few in-sim days
        // then plateaus at PrepTimePressureCap so it never overpowers other signals.
        var daysOnIsland      = currentTick / (EngineConstants.TickHz * 86400.0);
        var prepTimePressure  = Math.Min(PrepTimePressureCap, daysOnIsland * PrepTimePressureRatePerDay);

        var needAdd = new Dictionary<QualityType, double>
        {
            [QualityType.FoodConsumption] = stagedHungerNeed,
            [QualityType.Rest]            = fatiguePressure * FatiguePressureRestScale   + injuryPressure * InjuryRestNeedScale,
            [QualityType.Comfort]         = miseryPressure  * MiseryPressureComfortScale + injuryPressure * InjuryComfortNeedScale,
            [QualityType.Safety]          = injuryPressure  * InjurySafetyNeedScale,
            [QualityType.Preparation]     = prepTimePressure
        };

        // Personality weights come from traits (stable, independent of current state).
        // Derived via BuildQualityPersonalityBreakdown (shared with ExplainCandidateScoring).
        var personalityBreakdown = BuildQualityPersonalityBreakdown(traits);
        var personalityBase = personalityBreakdown.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.PersonalityBase);

        // Mood multipliers modulate personality when actor state is critical.
        // They suppress longer-horizon tendencies so survival instinct takes over.
        // Low health suppresses Fun, Mastery, and Preparation — badly hurt actors
        // should stop doing frivolous things and focus on rest/safety.
        var moodMultiplier = new Dictionary<QualityType, double>
        {
            [QualityType.Preparation]     = Math.Min(
                                                actor.Satiety < StarvatingSatietyThreshold ? PrepStarvationFloor : 1.0,
                                                Math.Max(InjuryPreparationSuppressionFloor,
                                                         1.0 - injuryFactor * (1.0 - InjuryPreparationSuppressionFloor))
                                            ),
            [QualityType.Mastery]         = Math.Min(
                                                actor.Energy < ExhaustedEnergyThreshold ? MasteryExhaustionFloor : 1.0,
                                                Math.Max(InjuryMasterySuppressionFloor,
                                                         1.0 - injuryFactor * (1.0 - InjuryMasterySuppressionFloor))
                                            ),
            [QualityType.Fun]             = (1.0 - (actor.Morale / 100.0)) * FunBaseScale *
                                                (actor.Satiety < FunCriticalSatietyThreshold || actor.Energy < FunCriticalEnergyThreshold
                                                    ? FunCriticalSurvivalScale : 1.0) *
                                                Math.Max(InjuryFunSuppressionFloor,
                                                         1.0 - injuryFactor * (1.0 - InjuryFunSuppressionFloor)),
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
            Outcome = new ActionOutcome(new ActionId("preaction_placeholder"), ActionOutcomeType.Success, Duration.FromTicks(0L)),
            Actor = (IslandActorState)actorState,
            World = (IslandWorldState)worldState,
            Tier = null,
            Rng = rng,
            Reservations = resourceAvailability,
            Tracer = worldState.Tracer
        };

        return preAction(effectCtx);
    }

    // ── Outcome-driven morale constants ─────────────────────────────────────
    /// <summary>Morale bonus applied when the action roll tier is CriticalSuccess.</summary>
    public const double MoraleCriticalSuccessBonus  = +8.0;
    /// <summary>Morale bonus applied when the action roll tier is Success.</summary>
    public const double MoraleSuccessBonus          = +2.0;
    /// <summary>Morale bonus applied when the action roll tier is PartialSuccess.</summary>
    public const double MoralePartialSuccessBonus   = +1.0;
    /// <summary>Morale penalty applied when the action roll tier is Failure.</summary>
    public const double MoraleFailurePenalty        = -3.0;
    /// <summary>Morale penalty applied when the action roll tier is CriticalFailure.</summary>
    public const double MoraleCriticalFailurePenalty = -8.0;

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

        // Metabolism is now handled by MetabolicBuff.OnTick each world tick.
        // Reset the buff intensity back to Light once the action completes so the next
        // action starts at the default resting/light-activity rate.
        var metabolicBuff = islandActorState.ActiveBuffs.OfType<MetabolicBuff>().FirstOrDefault();
        if (metabolicBuff != null)
            metabolicBuff.Intensity = MetabolicIntensity.Light;

        // Run action-specific effect handler for successful completions.
        if (outcome.Type == ActionOutcomeType.Success)
        {
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

        // Apply outcome-driven morale adjustment (after any action-specific effects).
        ApplyOutcomeMorale(actorId, outcome, islandActorState, islandWorld);
    }

    /// <summary>
    /// Adjusts morale based on the outcome of an action.  The adjustment is derived from
    /// the <see cref="RollOutcomeTier"/> stored in <c>outcome.ResultData["tier"]</c> when
    /// available, falling back to <see cref="ActionOutcomeType"/> otherwise.
    /// </summary>
    private void ApplyOutcomeMorale(
        ActorId actorId,
        ActionOutcome outcome,
        IslandActorState actor,
        IslandWorldState world)
    {
        double moraleDelta;
        string reason;

        var tier = GetTierFromOutcome(outcome);
        if (tier.HasValue)
        {
            (moraleDelta, reason) = tier.Value switch
            {
                RollOutcomeTier.CriticalSuccess => (MoraleCriticalSuccessBonus,   "Critical success"),
                RollOutcomeTier.Success         => (MoraleSuccessBonus,           "Success"),
                RollOutcomeTier.PartialSuccess  => (MoralePartialSuccessBonus,    "Partial success"),
                RollOutcomeTier.Failure         => (MoraleFailurePenalty,         "Failure"),
                RollOutcomeTier.CriticalFailure => (MoraleCriticalFailurePenalty, "Critical failure"),
                _                               => (0.0, string.Empty)
            };
        }
        else
        {
            (moraleDelta, reason) = outcome.Type switch
            {
                ActionOutcomeType.Success => (MoraleSuccessBonus,  "Success"),
                ActionOutcomeType.Failed  => (MoraleFailurePenalty, "Failure"),
                _                         => (0.0, string.Empty)
            };
        }

        if (moraleDelta == 0.0)
            return;

        actor.Morale += moraleDelta;
        world.Tracer.Beat(
            $"[Morale] {moraleDelta:+0.#;-0.#} ({reason} outcome)",
            actorId: actorId.Value,
            priority: 25);
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
        => TickWorldState(worldState, EmptyActors, currentTick, resourceAvailability);

    public List<TraceEvent> TickWorldState(
        WorldState worldState,
        IReadOnlyDictionary<ActorId, ActorState> actors,
        long currentTick,
        IResourceAvailability resourceAvailability)
    {
        var islandWorld = (IslandWorldState)worldState;

        // Tick ITickableBuff implementations on each actor.
        foreach (var actorState in actors.Values)
        {
            if (actorState is not IslandActorState islandActor)
                continue;

            foreach (var buff in islandActor.ActiveBuffs.OfType<ITickableBuff>())
                buff.OnTick(islandActor, worldState, currentTick);
        }

        return islandWorld.OnTickAdvanced(currentTick, resourceAvailability);
    }

    // ── Softmax weight floor — ensures every candidate retains a nonzero probability
    // even when its score is very negative relative to the current maximum.
    private const double SoftmaxEpsilon = 1e-8;

    /// <summary>
    /// Delegates to the sink-aware overload with a null sink (backwards-compatibility shim).
    /// </summary>
    public IReadOnlyList<ActionCandidate> OrderCandidatesForSelection(
        ActorId actorId,
        ActorState actorState,
        WorldState worldState,
        long currentTick,
        IReadOnlyList<ActionCandidate> sortedCandidates,
        Random rng)
        => OrderCandidatesForSelection(actorId, actorState, worldState, currentTick, sortedCandidates, rng, null);

    /// <summary>
    /// Mixture model: with probability P (DecisionPragmatism) return the deterministic
    /// best-first order unchanged; otherwise draw a softmax-weighted attempt order
    /// without replacement using the provided RNG.
    /// Populates <paramref name="debugSink"/> (when non-null) with structured ordering
    /// branch information for engine-level decision traces.
    /// </summary>
    public IReadOnlyList<ActionCandidate> OrderCandidatesForSelection(
        ActorId actorId,
        ActorState actorState,
        WorldState worldState,
        long currentTick,
        IReadOnlyList<ActionCandidate> sortedCandidates,
        Random rng,
        CandidateOrderingDebugSink? debugSink)
    {
        if (sortedCandidates.Count == 0)
            return sortedCandidates;

        var actor = (IslandActorState)actorState;
        var p = Math.Clamp(actor.DecisionPragmatism, 0.0, 1.0);

        var originalTopActionId = sortedCandidates[0].Action.Id.Value;

        // Pragmatic (exploit) branch — return deterministic order unchanged.
        if (rng.NextDouble() < p)
        {
            worldState.Tracer.Beat(
                $"[DecisionPragmatism] exploit branch (P={p:F2}): using best-first order.",
                actorId: actorId.Value);

            if (debugSink != null)
            {
                debugSink.OrderingBranch      = "exploit";
                debugSink.DecisionPragmatism  = p;
                debugSink.OriginalTopActionId = originalTopActionId;
                debugSink.ChosenActionId      = originalTopActionId;
                debugSink.ChosenOriginalRank  = 1;
            }
            return sortedCandidates;
        }

        // Spontaneous (explore) branch — softmax-weighted sampling without replacement.
        var spontaneity = 1.0 - p;
        // Temperature = Lerp(TLow, THigh, spontaneity): higher spontaneity → higher temp → flatter distribution.
        var temperature = actor.SoftmaxTLow + spontaneity * (actor.SoftmaxTHigh - actor.SoftmaxTLow);

        worldState.Tracer.Beat(
            $"[DecisionPragmatism] explore branch (P={p:F2}, T={temperature:F1}): softmax ordering.",
            actorId: actorId.Value);

        var remaining = new List<ActionCandidate>(sortedCandidates);
        var result    = new List<ActionCandidate>(remaining.Count);

        // Capture per-candidate softmax details on the first sampling step (for verbose traces).
        SoftmaxWeightEntry[]? firstStepWeightDetails = null;

        while (remaining.Count > 0)
        {
            // Numerically stable softmax: subtract max score before exponentiation.
            // Add SoftmaxEpsilon floor so that even very negative scores remain eligible.
            var maxScore = remaining.Max(c => c.Score);
            var weights  = new double[remaining.Count];
            var totalWeight = 0.0;
            for (var i = 0; i < remaining.Count; i++)
            {
                weights[i]   = Math.Max(Math.Exp((remaining[i].Score - maxScore) / temperature), SoftmaxEpsilon);
                totalWeight += weights[i];
            }

            // Capture first-step weight details for verbose sink (original sorted order alignment).
            if (firstStepWeightDetails == null && debugSink != null)
            {
                firstStepWeightDetails = remaining
                    .Select((c, i) => new SoftmaxWeightEntry(
                        c.Action.Id.Value,
                        c.ProviderItemId,
                        weights[i],
                        weights[i] / totalWeight))
                    .ToArray();
            }

            // Sample one candidate by cumulative probability.
            var u          = rng.NextDouble() * totalWeight;
            var cumulative = 0.0;
            var chosen     = remaining.Count - 1; // fallback to last item
            for (var i = 0; i < remaining.Count; i++)
            {
                cumulative += weights[i];
                if (u <= cumulative)
                {
                    chosen = i;
                    break;
                }
            }

            result.Add(remaining[chosen]);
            remaining.RemoveAt(chosen);
        }

        if (debugSink != null)
        {
            debugSink.OrderingBranch      = "explore";
            debugSink.DecisionPragmatism  = p;
            debugSink.Spontaneity         = spontaneity;
            debugSink.Temperature         = temperature;
            debugSink.OriginalTopActionId = originalTopActionId;
            debugSink.ChosenActionId      = result[0].Action.Id.Value;
            // Find chosen original rank: position in sortedCandidates (1-based)
            var chosenKey = result[0].Action.Id.Value + "|" + (result[0].ProviderItemId ?? "");
            for (var i = 0; i < sortedCandidates.Count; i++)
            {
                var ck = sortedCandidates[i].Action.Id.Value + "|" + (sortedCandidates[i].ProviderItemId ?? "");
                if (ck == chosenKey) { debugSink.ChosenOriginalRank = i + 1; break; }
            }
            debugSink.SoftmaxWeightDetails = firstStepWeightDetails;
        }

        return result;
    }

    /// <inheritdoc/>
    public Dictionary<string, object>? ExplainCandidateScoring(
        ActorId actorId,
        ActorState actorState,
        WorldState worldState,
        long currentTick,
        IReadOnlyList<ActionCandidate> candidates)
    {
        var actor = (IslandActorState)actorState;
        var model = BuildQualityModel(actor, currentTick);

        var hungerPressure  = 100.0 - actor.Satiety;
        var fatiguePressure = 100.0 - actor.Energy;
        var miseryPressure  = 100.0 - actor.Morale;
        var injuryPressure  = 100.0 - actor.Health;

        var actorStats = new Dictionary<string, object>
        {
            ["satiety"]             = actor.Satiety,
            ["energy"]              = actor.Energy,
            ["morale"]              = actor.Morale,
            ["health"]              = actor.Health,
            ["decisionPragmatism"]  = actor.DecisionPragmatism,
            ["softmaxTLow"]         = actor.SoftmaxTLow,
            ["softmaxTHigh"]        = actor.SoftmaxTHigh
        };

        var pressures = new Dictionary<string, object>
        {
            ["hungerPressure"]  = hungerPressure,
            ["fatiguePressure"] = fatiguePressure,
            ["miseryPressure"]  = miseryPressure,
            ["injuryPressure"]  = injuryPressure
        };

        // Preparation time-pressure breakdown — matches the bounded ramp in BuildQualityModel.
        var daysOnIsland     = currentTick / (EngineConstants.TickHz * 86400.0);
        var prepTimePressure = Math.Min(PrepTimePressureCap, daysOnIsland * PrepTimePressureRatePerDay);
        var prepTimePressureBreakdown = new Dictionary<string, object>
        {
            ["daysOnIsland"]   = Math.Round(daysOnIsland,    4),
            ["rawRamp"]        = Math.Round(daysOnIsland * PrepTimePressureRatePerDay, 4),
            ["cap"]            = PrepTimePressureCap,
            ["finalPressure"]  = Math.Round(prepTimePressure, 4)
        };

        // Health-pressure contribution breakdown — uses same class-level constants as BuildQualityModel.
        var injuryFactor = injuryPressure / 100.0;
        var healthInfluence = new Dictionary<string, object>
        {
            ["injuryFactor"]                 = Math.Round(injuryFactor, 4),
            ["safety_needAdd_contribution"]  = Math.Round(injuryPressure * InjurySafetyNeedScale,  4),
            ["rest_needAdd_contribution"]    = Math.Round(injuryPressure * InjuryRestNeedScale,    4),
            ["comfort_needAdd_contribution"] = Math.Round(injuryPressure * InjuryComfortNeedScale, 4),
            ["fun_suppressor"]               = Math.Round(Math.Max(InjuryFunSuppressionFloor,
                                                 1.0 - injuryFactor * (1.0 - InjuryFunSuppressionFloor)), 4),
            ["mastery_suppressor"]           = Math.Round(Math.Max(InjuryMasterySuppressionFloor,
                                                 1.0 - injuryFactor * (1.0 - InjuryMasterySuppressionFloor)), 4),
            ["preparation_suppressor"]       = Math.Round(Math.Max(InjuryPreparationSuppressionFloor,
                                                 1.0 - injuryFactor * (1.0 - InjuryPreparationSuppressionFloor)), 4)
        };

        // Personality influence breakdown — uses same helpers as BuildQualityModel to avoid formula drift.
        var traits = DerivePersonalityTraits(actor);
        var qualityPersonalityBreakdown = BuildQualityPersonalityBreakdown(traits);

        var traitDetails = new Dictionary<string, object>
        {
            ["planner"]     = new Dictionary<string, object> { ["value"] = Math.Round(traits.Planner,     4), ["source"] = "Norm(INT, WIS)", ["inputs"] = new Dictionary<string, int> { ["INT"] = traits.INT, ["WIS"] = traits.WIS } },
            ["craftsman"]   = new Dictionary<string, object> { ["value"] = Math.Round(traits.Craftsman,   4), ["source"] = "Norm(DEX, INT)", ["inputs"] = new Dictionary<string, int> { ["DEX"] = traits.DEX, ["INT"] = traits.INT } },
            ["survivor"]    = new Dictionary<string, object> { ["value"] = Math.Round(traits.Survivor,    4), ["source"] = "Norm(CON, WIS)", ["inputs"] = new Dictionary<string, int> { ["CON"] = traits.CON, ["WIS"] = traits.WIS } },
            ["hedonist"]    = new Dictionary<string, object> { ["value"] = Math.Round(traits.Hedonist,    4), ["source"] = "Norm(CHA, CON)", ["inputs"] = new Dictionary<string, int> { ["CHA"] = traits.CHA, ["CON"] = traits.CON } },
            ["instinctive"] = new Dictionary<string, object> { ["value"] = Math.Round(traits.Instinctive, 4), ["source"] = "Norm(STR, CHA)", ["inputs"] = new Dictionary<string, int> { ["STR"] = traits.STR, ["CHA"] = traits.CHA } },
            ["industrious"] = new Dictionary<string, object> { ["value"] = Math.Round(traits.Industrious, 4), ["source"] = "Norm(STR, DEX)", ["inputs"] = new Dictionary<string, int> { ["STR"] = traits.STR, ["DEX"] = traits.DEX } }
        };

        var qualityPersonalityBreakdownExplain = new Dictionary<string, object>();
        foreach (var (q, entry) in qualityPersonalityBreakdown)
        {
            qualityPersonalityBreakdownExplain[q.ToString()] = new Dictionary<string, object>
            {
                ["formula"]         = entry.Formula,
                ["contributors"]    = entry.Contributors.ToDictionary(
                                          kvp => kvp.Key,
                                          kvp => (object)Math.Round(kvp.Value, 4)),
                ["personalityBase"] = Math.Round(entry.PersonalityBase, 4)
            };
        }

        var personalityInfluence = new Dictionary<string, object>
        {
            ["traits"]                    = traitDetails,
            ["qualityPersonalityBreakdown"] = qualityPersonalityBreakdownExplain
        };

        // DecisionPragmatism derivation breakdown.
        var pragmatismBreakdown = DeriveDecisionPragmatism(traits);
        var decisionPragmatismBreakdown = new Dictionary<string, object>
        {
            ["base"]                   = Math.Round(pragmatismBreakdown.Base,                  4),
            ["plannerContribution"]    = Math.Round(pragmatismBreakdown.PlannerContribution,   4),
            ["survivorContribution"]   = Math.Round(pragmatismBreakdown.SurvivorContribution,  4),
            ["hedonistContribution"]   = Math.Round(-pragmatismBreakdown.HedonistContribution,    4),
            ["instinctiveContribution"]= Math.Round(-pragmatismBreakdown.InstinctiveContribution, 4),
            ["finalDecisionPragmatism"]= Math.Round(pragmatismBreakdown.FinalDecisionPragmatism, 4),
            ["note"] = actor.DecisionPragmatism != pragmatismBreakdown.FinalDecisionPragmatism
                           ? "overridden by actor data"
                           : "derived from personality"
        };

        var effectiveWeights = new Dictionary<string, object>();
        foreach (var q in Enum.GetValues<QualityType>())
        {
            var w = model.EffectiveWeight(q);
            if (w != 0.0)
                effectiveWeights[q.ToString()] = w;
        }

        // Quality model decomposition: shows how each effective weight is built from
        // its three independent sources so traces make tuning decisions transparent.
        var qualityModelDecomposition = new Dictionary<string, object>();
        foreach (var q in Enum.GetValues<QualityType>())
        {
            model.NeedAdd.TryGetValue(q, out var needAdd);
            model.PersonalityBase.TryGetValue(q, out var personalityBase);
            model.MoodMultiplier.TryGetValue(q, out var moodMultiplier);
            var effectiveWeight = model.EffectiveWeight(q);
            if (needAdd == 0.0 && personalityBase == 0.0 && moodMultiplier == 0.0)
                continue;

            qualityModelDecomposition[q.ToString()] = new Dictionary<string, object>
            {
                ["needAdd"]         = Math.Round(needAdd,          4),
                ["personalityBase"] = Math.Round(personalityBase,  4),
                ["moodMultiplier"]  = Math.Round(moodMultiplier,   4),
                ["effectiveWeight"] = Math.Round(effectiveWeight,  4)
            };
        }

        var candidateBreakdowns = candidates.Select(c =>
        {
            var contributions = new Dictionary<string, object>();
            var totalQualitySum = 0.0;
            foreach (var (q, value) in c.Qualities)
            {
                var weight       = model.EffectiveWeight(q);
                var contribution = weight * value;
                totalQualitySum += contribution;
                contributions[q.ToString()] = new Dictionary<string, object>
                {
                    ["qualityValue"]    = value,
                    ["effectiveWeight"] = weight,
                    ["contribution"]    = contribution
                };
            }
            var breakdown = new Dictionary<string, object>
            {
                ["actionId"]            = c.Action.Id.Value,
                ["intrinsicScore"]      = c.IntrinsicScore,
                ["qualityContributions"] = contributions,
                ["totalQualitySum"]     = totalQualitySum,
                ["finalPreVarietyScore"] = c.IntrinsicScore + totalQualitySum
            };
            if (c.ProviderItemId != null)
                breakdown["providerItemId"] = c.ProviderItemId;
            return (object)breakdown;
        }).ToList();

        return new Dictionary<string, object>
        {
            ["actorStats"]                  = actorStats,
            ["pressures"]                   = pressures,
            ["prepTimePressureBreakdown"]   = prepTimePressureBreakdown,
            ["decisionPragmatismBreakdown"] = decisionPragmatismBreakdown,
            ["healthInfluence"]             = healthInfluence,
            ["personalityInfluence"]        = personalityInfluence,
            ["effectiveWeights"]            = effectiveWeights,
            ["qualityModelDecomposition"]   = qualityModelDecomposition,
            ["candidateBreakdowns"]         = candidateBreakdowns
        };
    }

    /// <summary>
    /// Builds periodic snapshot trace events for the island domain.
    /// Emits one <c>PeriodicWorldSnapshot</c>, one <c>PeriodicSupplySnapshot</c> per supply
    /// pile, one <c>PeriodicWorldItemSnapshot</c> per world item, and per-actor
    /// <c>PeriodicActorSnapshot</c> and <c>PeriodicRecipeSnapshot</c> events.
    /// </summary>
    public List<TraceEvent> BuildPeriodicSnapshot(
        WorldState worldState,
        IReadOnlyDictionary<ActorId, ActorState> actors,
        long currentTick)
    {
        var islandWorld = (IslandWorldState)worldState;
        var events = new List<TraceEvent>();

        // ── World snapshot ─────────────────────────────────────────────────────
        var calendar = islandWorld.GetItem<Items.CalendarItem>("calendar");
        var weather  = islandWorld.GetItem<Items.WeatherItem>("weather");
        var beach    = islandWorld.GetItem<Items.BeachItem>("beach");
        var ocean    = islandWorld.GetItem<Items.OceanItem>("ocean");

        var worldDetails = new Dictionary<string, object>();
        if (calendar != null)
        {
            worldDetails["day"]      = calendar.DayCount;
            worldDetails["hour"]     = Math.Round(calendar.HourOfDay, 2);
            worldDetails["dayPhase"] = calendar.CurrentDayPhase.ToString();
        }
        if (weather != null)
        {
            worldDetails["temperature"]   = weather.Temperature.ToString();
            worldDetails["precipitation"] = weather.Precipitation.ToString();
        }
        if (beach != null)
            worldDetails["tide"] = beach.Tide.ToString();
        if (ocean != null)
        {
            var fish = ocean.BountySupplies.OfType<Supply.FishSupply>().FirstOrDefault();
            if (fish != null)
                worldDetails["fishAvailable"] = Math.Round(fish.Quantity, 2);
        }

        events.Add(new TraceEvent(currentTick, null, "PeriodicWorldSnapshot", worldDetails));

        // ── Supply pile snapshots ───────────────────────────────────────────────
        foreach (var pile in islandWorld.WorldItems.OfType<Supply.SupplyPile>().OrderBy(p => p.Id))
        {
            var supplies = pile.Supplies
                .Where(s => s.Quantity > 0)
                .OrderBy(s => s.Type)
                .Select(s => $"{s.Type.Replace("supply_", "")}:{Math.Round(s.Quantity, 0)}")
                .ToList();

            events.Add(new TraceEvent(currentTick, null, "PeriodicSupplySnapshot", new Dictionary<string, object>
            {
                ["pileId"]   = pile.Id,
                ["access"]   = pile.AccessControl,
                ["supplies"] = supplies.Count > 0 ? string.Join(", ", supplies) : "(empty)"
            }));
        }

        // ── World item snapshots ────────────────────────────────────────────────
        foreach (var item in islandWorld.WorldItems.OrderBy(i => i.Id))
        {
            // Skip supply piles (already covered above)
            if (item is Supply.SupplyPile)
                continue;

            var room = islandWorld.GetItemRoomId(item.Id);

            var details = new Dictionary<string, object>
            {
                ["itemId"]   = item.Id,
                ["itemType"] = item.Type,
                ["room"]     = room ?? "(none)"
            };

            if (item is MaintainableWorldItem maintainable)
                details["quality"] = Math.Round(maintainable.Quality, 2);

            if (item is Items.ToolItem tool)
            {
                details["isBroken"]      = tool.IsBroken;
                details["ownershipType"] = tool.OwnershipType.ToString();
                if (tool.OwnerActorId.HasValue)
                    details["owner"] = tool.OwnerActorId.Value.Value;
            }

            events.Add(new TraceEvent(currentTick, null, "PeriodicWorldItemSnapshot", details));
        }

        // ── Per-actor snapshots ─────────────────────────────────────────────────
        foreach (var actorId in actors.Keys.OrderBy(a => a.Value))
        {
            if (actors[actorId] is not IslandActorState actor)
                continue;

            var actorDetails = new Dictionary<string, object>
            {
                ["status"]            = actor.Status.ToString(),
                ["currentAction"]     = actor.CurrentAction?.Id.Value ?? "idle",
                ["satiety"]           = Math.Round(actor.Satiety, 1),
                ["energy"]            = Math.Round(actor.Energy, 1),
                ["morale"]            = Math.Round(actor.Morale, 1),
                ["health"]            = Math.Round(actor.Health, 1),
                ["decisionPragmatism"] = Math.Round(actor.DecisionPragmatism, 2),
                ["room"]              = actor.CurrentRoomId
            };

            if (actor.ActiveBuffs.Count > 0)
                actorDetails["buffs"] = string.Join(", ", actor.ActiveBuffs.Select(b => b.Name).OrderBy(n => n));

            events.Add(new TraceEvent(currentTick, actorId, "PeriodicActorSnapshot", actorDetails));

            // Recipe snapshot
            var knownRecipes = actor.KnownRecipeIds.OrderBy(r => r).ToList();
            events.Add(new TraceEvent(currentTick, actorId, "PeriodicRecipeSnapshot", new Dictionary<string, object>
            {
                ["knownRecipes"] = knownRecipes.Count > 0 ? string.Join(", ", knownRecipes) : "(none)"
            }));
        }

        return events;
    }
}
