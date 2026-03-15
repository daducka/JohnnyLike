using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;

namespace JohnnyLike.Domain.Island.Tests;

/// <summary>
/// Tests for the personality-driven DecisionPragmatism derivation, time-based
/// preparation pressure, and comfort-action rebalancing introduced in the
/// "Derive DecisionPragmatism From Personality" issue.
/// </summary>
public class PersonalityPragmatismTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates an actor with the given ability scores and no explicit DecisionPragmatism override.
    /// The domain will derive pragmatism from personality.
    /// </summary>
    private static IslandActorState MakeActor(int str = 10, int dex = 10, int con = 10,
                                               int @int = 10, int wis = 10, int cha = 10)
    {
        var domain  = new IslandDomainPack();
        var actorId = new ActorId("P");
        return (IslandActorState)domain.CreateActorState(actorId, new Dictionary<string, object>
        {
            ["STR"] = str, ["DEX"] = dex, ["CON"] = con,
            ["INT"] = @int, ["WIS"] = wis, ["CHA"] = cha
        });
    }

    private static IslandActorState MakeActorWithExplicitPragmatism(double pragmatism)
    {
        var domain  = new IslandDomainPack();
        var actorId = new ActorId("PExplicit");
        return (IslandActorState)domain.CreateActorState(actorId, new Dictionary<string, object>
        {
            ["DecisionPragmatism"] = pragmatism
        });
    }

    private static (IslandDomainPack, ActorId, IslandActorState, IslandWorldState) MakeSetup(
        int str = 10, int dex = 10, int con = 10, int @int = 10, int wis = 10, int cha = 10,
        double satiety = 80.0, double energy = 80.0, double morale = 60.0)
    {
        var domain  = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actor   = (IslandActorState)domain.CreateActorState(actorId, new Dictionary<string, object>
        {
            ["STR"] = str, ["DEX"] = dex, ["CON"] = con,
            ["INT"] = @int, ["WIS"] = wis, ["CHA"] = cha,
            ["satiety"] = satiety, ["energy"] = energy, ["morale"] = morale
        });
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        domain.InitializeActorItems(actorId, world);
        return (domain, actorId, actor, world);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Part 1: Pragmatism derivation
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void DecisionPragmatism_IsDerivedFromPersonality_WhenNotExplicitlyProvided()
    {
        // Default actor (STR=DEX=CON=INT=WIS=CHA=10) with no explicit pragmatism.
        // base=0.80, all trait contributions ~0 → should be close to 0.80, clamped in [0.65, 0.98].
        var actor = MakeActor();

        Assert.NotEqual(1.0, actor.DecisionPragmatism); // should NOT be the old default of 1.0
        Assert.True(actor.DecisionPragmatism >= 0.65 && actor.DecisionPragmatism <= 0.98,
            $"Expected DecisionPragmatism in [0.65, 0.98], got {actor.DecisionPragmatism:F4}");
    }

    [Fact]
    public void DecisionPragmatism_ExplicitOverride_TakesPrecedenceOverDerived()
    {
        var actor = MakeActorWithExplicitPragmatism(0.42);
        Assert.Equal(0.42, actor.DecisionPragmatism, precision: 10);
    }

    [Fact]
    public void DecisionPragmatism_PlannerHeavy_IsHigherThanHedonistHeavy()
    {
        // Frank-archetype: high INT+WIS (planner), high CON+WIS (survivor)
        var planner   = MakeActor(@int: 16, wis: 16, con: 14);

        // Sawyer-archetype: high CHA+CON (hedonist), high STR+CHA (instinctive)
        var hedonist  = MakeActor(cha: 16, con: 14, str: 14);

        Assert.True(planner.DecisionPragmatism > hedonist.DecisionPragmatism,
            $"Planner should be more pragmatic ({planner.DecisionPragmatism:F4}) than hedonist ({hedonist.DecisionPragmatism:F4})");
    }

    [Fact]
    public void DecisionPragmatism_IsClamped_ToSafeRange()
    {
        // Extreme planner: push toward upper bound
        var extremePlanner  = MakeActor(@int: 20, wis: 20, con: 20);

        // Extreme hedonist: push toward lower bound
        var extremeHedonist = MakeActor(cha: 20, con: 20, str: 20);

        Assert.True(extremePlanner.DecisionPragmatism  <= 0.98,
            $"Max pragmatism should not exceed 0.98, got {extremePlanner.DecisionPragmatism:F4}");
        Assert.True(extremeHedonist.DecisionPragmatism >= 0.65,
            $"Min pragmatism should not go below 0.65, got {extremeHedonist.DecisionPragmatism:F4}");
    }

    [Fact]
    public void DecisionPragmatism_DefaultActor_IsNear0_80()
    {
        // STR=DEX=CON=INT=WIS=CHA=10 → all traits = 0 → raw = 0.80 (no contributions)
        var actor = MakeActor();
        Assert.Equal(0.80, actor.DecisionPragmatism, precision: 4);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Part 2: Pragmatism breakdown in traces
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ExplainCandidateScoring_ContainsDecisionPragmatismBreakdown()
    {
        var (domain, actorId, actor, world) = MakeSetup(@int: 14, wis: 14);
        var resources   = new EmptyResourceAvailability();
        var candidates  = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);
        var explanation = domain.ExplainCandidateScoring(actorId, actor, world, 0L, candidates)!;

        Assert.True(explanation.ContainsKey("decisionPragmatismBreakdown"),
            "ExplainCandidateScoring must include 'decisionPragmatismBreakdown'");

        var bd = (Dictionary<string, object>)explanation["decisionPragmatismBreakdown"];
        Assert.True(bd.ContainsKey("base"));
        Assert.True(bd.ContainsKey("plannerContribution"));
        Assert.True(bd.ContainsKey("survivorContribution"));
        Assert.True(bd.ContainsKey("hedonistContribution"));
        Assert.True(bd.ContainsKey("instinctiveContribution"));
        Assert.True(bd.ContainsKey("finalDecisionPragmatism"));
        Assert.True(bd.ContainsKey("note"));
    }

    [Fact]
    public void ExplainCandidateScoring_PragmatismBreakdown_BaseIs0_80()
    {
        var (domain, actorId, actor, world) = MakeSetup();
        var resources   = new EmptyResourceAvailability();
        var candidates  = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);
        var explanation = domain.ExplainCandidateScoring(actorId, actor, world, 0L, candidates)!;

        var bd = (Dictionary<string, object>)explanation["decisionPragmatismBreakdown"];
        Assert.Equal(0.80, (double)bd["base"], precision: 4);
    }

    [Fact]
    public void ExplainCandidateScoring_PragmatismBreakdown_FinalMatchesActorPragmatism()
    {
        var (domain, actorId, actor, world) = MakeSetup(@int: 16, wis: 14);
        var resources   = new EmptyResourceAvailability();
        var candidates  = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);
        var explanation = domain.ExplainCandidateScoring(actorId, actor, world, 0L, candidates)!;

        var bd    = (Dictionary<string, object>)explanation["decisionPragmatismBreakdown"];
        var final = (double)bd["finalDecisionPragmatism"];

        Assert.Equal(actor.DecisionPragmatism, final, precision: 4);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Part 3: Personality scaling for Preparation / Efficiency / Mastery
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Preparation_PersonalityBase_IsStrongerForPlanner()
    {
        // Planner: INT=16, WIS=16 → planner = Norm(16,16) = (32-20)/20 = 0.6
        //          STR=DEX=10    → industrious = 0
        // Scale=0.7 → base = (0.6+0)*0.7 = 0.42
        var (domainP, actorIdP, actorP, worldP) = MakeSetup(@int: 16, wis: 16);
        var resourcesP   = new EmptyResourceAvailability();
        var candidatesP  = domainP.GenerateCandidates(actorIdP, actorP, worldP, 0L, new Random(1), resourcesP);
        var explanationP = domainP.ExplainCandidateScoring(actorIdP, actorP, worldP, 0L, candidatesP)!;

        // Default actor: planner=0, industrious=0 → base = 0
        var (domainD, actorIdD, actorD, worldD) = MakeSetup();
        var resourcesD   = new EmptyResourceAvailability();
        var candidatesD  = domainD.GenerateCandidates(actorIdD, actorD, worldD, 0L, new Random(1), resourcesD);
        var explanationD = domainD.ExplainCandidateScoring(actorIdD, actorD, worldD, 0L, candidatesD)!;

        double GetPrepBase(Dictionary<string, object> expl)
        {
            var pi = (Dictionary<string, object>)expl["personalityInfluence"];
            var bd = (Dictionary<string, object>)pi["qualityPersonalityBreakdown"];
            var prep = (Dictionary<string, object>)bd["Preparation"];
            return (double)prep["personalityBase"];
        }

        var plannerPrepBase  = GetPrepBase(explanationP);
        var defaultPrepBase  = GetPrepBase(explanationD);

        Assert.True(plannerPrepBase > defaultPrepBase,
            $"Planner prep base ({plannerPrepBase:F4}) should exceed default ({defaultPrepBase:F4})");
        Assert.Equal(0.42, plannerPrepBase, precision: 4);
    }

    [Fact]
    public void ThinkAboutSupplies_RanksHigherForPlannerThanDefaultActor_EarlyGame()
    {
        // Planner actor at day 0, tick=0 (no time pressure yet)
        var (domainP, actorIdP, actorP, worldP) = MakeSetup(@int: 16, wis: 16);
        var candidatesP = domainP.GenerateCandidates(actorIdP, actorP, worldP, 0L, new Random(1), new EmptyResourceAvailability());
        var thinkP = candidatesP.FirstOrDefault(c => c.Action.Id.Value == "think_about_supplies");

        // Default actor at tick=0
        var (domainD, actorIdD, actorD, worldD) = MakeSetup();
        var candidatesD = domainD.GenerateCandidates(actorIdD, actorD, worldD, 0L, new Random(1), new EmptyResourceAvailability());
        var thinkD = candidatesD.FirstOrDefault(c => c.Action.Id.Value == "think_about_supplies");

        Assert.NotNull(thinkP);
        Assert.NotNull(thinkD);
        Assert.True(thinkP!.Score > thinkD!.Score,
            $"think_about_supplies should score higher for planner ({thinkP.Score:F4}) than default ({thinkD.Score:F4})");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Part 4: Comfort-action rebalance
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void SitAndWatchWaves_ComfortQuality_IsReducedBelowOldValue()
    {
        var (domain, actorId, actor, world) = MakeSetup(morale: 60.0, energy: 80.0, satiety: 80.0);
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), new EmptyResourceAvailability());
        var waves = candidates.FirstOrDefault(c => c.Action.Id.Value == "sit_and_watch_waves");

        Assert.NotNull(waves);
        Assert.True(waves!.Qualities.ContainsKey(QualityType.Comfort));
        // Old value was 0.70; new value should be < 0.70
        Assert.True(waves.Qualities[QualityType.Comfort] < 0.70,
            $"sit_and_watch_waves Comfort quality should be below old 0.70, got {waves.Qualities[QualityType.Comfort]:F2}");
    }

    [Fact]
    public void HumToSelf_ComfortQuality_IsReducedBelowOldValue()
    {
        var (domain, actorId, actor, world) = MakeSetup(morale: 60.0, energy: 80.0, satiety: 80.0);
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), new EmptyResourceAvailability());
        var hum = candidates.FirstOrDefault(c => c.Action.Id.Value == "hum_to_self");

        Assert.NotNull(hum);
        Assert.True(hum!.Qualities.ContainsKey(QualityType.Comfort));
        // Old value was 0.60; new value should be < 0.60
        Assert.True(hum.Qualities[QualityType.Comfort] < 0.60,
            $"hum_to_self Comfort quality should be below old 0.60, got {hum.Qualities[QualityType.Comfort]:F2}");
    }

    [Fact]
    public void SitAndWatchWaves_StillAvailable_AfterComfortRebalance()
    {
        var (domain, actorId, actor, world) = MakeSetup(morale: 60.0, energy: 80.0);
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), new EmptyResourceAvailability());
        Assert.Contains(candidates, c => c.Action.Id.Value == "sit_and_watch_waves");
    }

    [Fact]
    public void PlannerActor_ThinkAboutSupplies_RanksAboveComfortActionsEarly()
    {
        // Frank-archetype planner — should start productive actions before beach-vibing.
        var (domain, actorId, actor, world) = MakeSetup(@int: 16, wis: 16, energy: 90.0, morale: 70.0, satiety: 90.0);
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), new EmptyResourceAvailability())
            .OrderByDescending(c => c.Score).ToList();

        var thinkRank = candidates.FindIndex(c => c.Action.Id.Value == "think_about_supplies");
        var wavesRank = candidates.FindIndex(c => c.Action.Id.Value == "sit_and_watch_waves");

        Assert.True(thinkRank >= 0, "think_about_supplies should be a candidate");
        // sit_and_watch_waves requires PlayfulOnly (Morale>35, Energy>30, Satiety>25, Health>50).
        // With morale=70 it is expected to be available; if so, think_about_supplies should outrank it.
        Assert.True(wavesRank >= 0, "sit_and_watch_waves should be a candidate with morale=70");
        Assert.True(thinkRank < wavesRank,
            $"think_about_supplies (rank {thinkRank+1}) should outrank sit_and_watch_waves (rank {wavesRank+1}) for planner");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Part 5: Bounded preparation time-pressure
    // ═════════════════════════════════════════════════════════════════════════

    private static readonly long TicksPerDay = (long)EngineConstants.TickHz * 86400L;

    [Fact]
    public void PrepTimePressure_IsZero_AtDayZero()
    {
        var (domain, actorId, actor, world) = MakeSetup();
        var resources   = new EmptyResourceAvailability();
        var candidates  = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);
        var explanation = domain.ExplainCandidateScoring(actorId, actor, world, 0L, candidates)!;

        Assert.True(explanation.ContainsKey("prepTimePressureBreakdown"));
        var ptb = (Dictionary<string, object>)explanation["prepTimePressureBreakdown"];
        Assert.Equal(0.0, (double)ptb["finalPressure"], precision: 6);
    }

    [Fact]
    public void PrepTimePressure_Increases_WithTime()
    {
        var (domain, actorId, actor, world) = MakeSetup();
        var resources   = new EmptyResourceAvailability();
        var candidates0 = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);
        var candidates2 = domain.GenerateCandidates(actorId, actor, world, 2 * TicksPerDay, new Random(1), resources);
        var expl0 = domain.ExplainCandidateScoring(actorId, actor, world, 0L, candidates0)!;
        var expl2 = domain.ExplainCandidateScoring(actorId, actor, world, 2 * TicksPerDay, candidates2)!;

        var pressure0 = (double)((Dictionary<string, object>)expl0["prepTimePressureBreakdown"])["finalPressure"];
        var pressure2 = (double)((Dictionary<string, object>)expl2["prepTimePressureBreakdown"])["finalPressure"];

        Assert.True(pressure2 > pressure0,
            $"Prep time pressure should grow over time: day0={pressure0:F4}, day2={pressure2:F4}");
    }

    [Fact]
    public void PrepTimePressure_IsBounded_DoesNotGrowForever()
    {
        var (domain, actorId, actor, world) = MakeSetup();
        var resources = new EmptyResourceAvailability();

        // Compare day 10 vs day 100 — both should be at the cap (0.20)
        var candidates10  = domain.GenerateCandidates(actorId, actor, world, 10 * TicksPerDay,  new Random(1), resources);
        var candidates100 = domain.GenerateCandidates(actorId, actor, world, 100 * TicksPerDay, new Random(1), resources);
        var expl10  = domain.ExplainCandidateScoring(actorId, actor, world, 10 * TicksPerDay,  candidates10)!;
        var expl100 = domain.ExplainCandidateScoring(actorId, actor, world, 100 * TicksPerDay, candidates100)!;

        var pressure10  = (double)((Dictionary<string, object>)expl10["prepTimePressureBreakdown"])["finalPressure"];
        var pressure100 = (double)((Dictionary<string, object>)expl100["prepTimePressureBreakdown"])["finalPressure"];

        // Both should be at (or near) the cap
        Assert.Equal(pressure10,  0.20, precision: 4);
        Assert.Equal(pressure100, 0.20, precision: 4);
    }

    [Fact]
    public void PrepTimePressureBreakdown_ContainsExpectedFields()
    {
        var (domain, actorId, actor, world) = MakeSetup();
        var resources   = new EmptyResourceAvailability();
        var candidates  = domain.GenerateCandidates(actorId, actor, world, TicksPerDay, new Random(1), resources);
        var explanation = domain.ExplainCandidateScoring(actorId, actor, world, TicksPerDay, candidates)!;

        Assert.True(explanation.ContainsKey("prepTimePressureBreakdown"));
        var ptb = (Dictionary<string, object>)explanation["prepTimePressureBreakdown"];
        Assert.True(ptb.ContainsKey("daysOnIsland"));
        Assert.True(ptb.ContainsKey("rawRamp"));
        Assert.True(ptb.ContainsKey("cap"));
        Assert.True(ptb.ContainsKey("finalPressure"));

        // After 1 day, rawRamp = 1.0 * 0.05 = 0.05, cap = 0.20, final = 0.05
        Assert.Equal(1.0,  (double)ptb["daysOnIsland"], precision: 4);
        Assert.Equal(0.05, (double)ptb["rawRamp"],      precision: 4);
        Assert.Equal(0.20, (double)ptb["cap"],          precision: 4);
        Assert.Equal(0.05, (double)ptb["finalPressure"],precision: 4);
    }

    [Fact]
    public void PrepTimePressure_AffectsPreparationNeedAdd_InQualityModel()
    {
        // After enough days, needAdd[Preparation] should be > 0 due to time pressure.
        var (domain, actorId, actor, world) = MakeSetup();
        var resources = new EmptyResourceAvailability();

        var candidates0 = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);
        var candidates3 = domain.GenerateCandidates(actorId, actor, world, 3 * TicksPerDay, new Random(1), resources);
        var expl0 = domain.ExplainCandidateScoring(actorId, actor, world, 0L, candidates0)!;
        var expl3 = domain.ExplainCandidateScoring(actorId, actor, world, 3 * TicksPerDay, candidates3)!;

        double GetPrepNeedAdd(Dictionary<string, object> expl)
        {
            var qmd = (Dictionary<string, object>)expl["qualityModelDecomposition"];
            if (!qmd.ContainsKey("Preparation")) return 0.0;
            return (double)((Dictionary<string, object>)qmd["Preparation"])["needAdd"];
        }

        var needAdd0 = GetPrepNeedAdd(expl0);
        var needAdd3 = GetPrepNeedAdd(expl3);

        Assert.True(needAdd3 > needAdd0,
            $"Preparation needAdd should increase after 3 days (was {needAdd0:F4}, now {needAdd3:F4})");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Ordering behavior: lower-pragmatism actors sometimes explore
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void LowPragmatismActor_SometimesTakesExploreBranch()
    {
        var domain  = new IslandDomainPack();
        var actorId = new ActorId("Explorer");
        var actor   = (IslandActorState)domain.CreateActorState(actorId);
        actor.DecisionPragmatism = 0.65; // near floor — will often explore

        var world = (IslandWorldState)domain.CreateInitialWorldState();
        domain.InitializeActorItems(actorId, world);

        var resources  = new EmptyResourceAvailability();
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);
        var sorted     = candidates.OrderByDescending(c => c.Score).ToList();

        // Run many times with different seeds and count explore branches
        var exploreBranches = 0;
        for (var seed = 0; seed < 200; seed++)
        {
            var sink = new CandidateOrderingDebugSink();
            domain.OrderCandidatesForSelection(actorId, actor, world, 0L, sorted, new Random(seed), sink);
            if (sink.OrderingBranch == "explore")
                exploreBranches++;
        }

        Assert.True(exploreBranches > 10,
            $"Actor with pragmatism=0.65 should explore sometimes over 200 trials, got {exploreBranches}");
    }

    [Fact]
    public void HighPragmatismActor_OverwhelminglyExploits()
    {
        var domain  = new IslandDomainPack();
        var actorId = new ActorId("Exploiter");
        var actor   = (IslandActorState)domain.CreateActorState(actorId);
        actor.DecisionPragmatism = 0.98; // near upper bound

        var world = (IslandWorldState)domain.CreateInitialWorldState();
        domain.InitializeActorItems(actorId, world);

        var resources  = new EmptyResourceAvailability();
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);
        var sorted     = candidates.OrderByDescending(c => c.Score).ToList();

        var exploitBranches = 0;
        for (var seed = 0; seed < 200; seed++)
        {
            var sink = new CandidateOrderingDebugSink();
            domain.OrderCandidatesForSelection(actorId, actor, world, 0L, sorted, new Random(seed), sink);
            if (sink.OrderingBranch == "exploit")
                exploitBranches++;
        }

        Assert.True(exploitBranches > 150,
            $"Actor with pragmatism=0.98 should exploit >150/200 times, got {exploitBranches}");
    }
}
