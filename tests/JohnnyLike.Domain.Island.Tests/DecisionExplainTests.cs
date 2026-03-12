using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.Engine;

namespace JohnnyLike.Domain.Island.Tests;

/// <summary>
/// Tests for the island-domain decision-tracing hooks:
///   - ExplainCandidateScoring (pressure values, quality contributions, final scores)
///   - OrderCandidatesForSelection with CandidateOrderingDebugSink (exploit / explore)
/// </summary>
public class DecisionExplainTests
{
    // ── Shared helpers ────────────────────────────────────────────────────────

    private static (IslandDomainPack domain, ActorId actorId, IslandActorState actor, IslandWorldState world)
        CreateSetup(double pragmatism = 1.0, double satiety = 70.0, double energy = 80.0,
                    double morale = 60.0, double health = 100.0)
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("Tester");
        var actor = (IslandActorState)domain.CreateActorState(actorId, new Dictionary<string, object>
        {
            ["satiety"] = satiety,
            ["energy"]  = energy,
            ["morale"]  = morale
        });
        actor.Health           = health;
        actor.DecisionPragmatism = pragmatism;
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        domain.InitializeActorItems(actorId, world);
        return (domain, actorId, actor, world);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 1. ExplainCandidateScoring — basic structure
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ExplainCandidateScoring_ReturnsNonNull()
    {
        var (domain, actorId, actor, world) = CreateSetup();
        var resources = new EmptyResourceAvailability();
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);

        var explanation = domain.ExplainCandidateScoring(actorId, actor, world, 0L, candidates);

        Assert.NotNull(explanation);
    }

    [Fact]
    public void ExplainCandidateScoring_ContainsActorStats()
    {
        var (domain, actorId, actor, world) = CreateSetup(satiety: 55.0, energy: 30.0, morale: 40.0);
        var resources = new EmptyResourceAvailability();
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);
        var explanation = domain.ExplainCandidateScoring(actorId, actor, world, 0L, candidates)!;

        Assert.True(explanation.ContainsKey("actorStats"));
        var stats = (Dictionary<string, object>)explanation["actorStats"];
        Assert.Equal(55.0, (double)stats["satiety"]);
        Assert.Equal(30.0, (double)stats["energy"]);
        Assert.Equal(40.0, (double)stats["morale"]);
        Assert.True(stats.ContainsKey("decisionPragmatism"));
        Assert.True(stats.ContainsKey("softmaxTLow"));
        Assert.True(stats.ContainsKey("softmaxTHigh"));
    }

    [Fact]
    public void ExplainCandidateScoring_ContainsPressures()
    {
        var (domain, actorId, actor, world) = CreateSetup(satiety: 40.0, energy: 70.0, morale: 50.0, health: 80.0);
        var resources = new EmptyResourceAvailability();
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);
        var explanation = domain.ExplainCandidateScoring(actorId, actor, world, 0L, candidates)!;

        var pressures = (Dictionary<string, object>)explanation["pressures"];
        Assert.Equal(60.0, (double)pressures["hungerPressure"],  precision: 5);
        Assert.Equal(30.0, (double)pressures["fatiguePressure"], precision: 5);
        Assert.Equal(50.0, (double)pressures["miseryPressure"],  precision: 5);
        Assert.Equal(20.0, (double)pressures["injuryPressure"],  precision: 5);
    }

    [Fact]
    public void ExplainCandidateScoring_ContainsEffectiveWeights()
    {
        var (domain, actorId, actor, world) = CreateSetup();
        var resources = new EmptyResourceAvailability();
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);
        var explanation = domain.ExplainCandidateScoring(actorId, actor, world, 0L, candidates)!;

        var weights = (Dictionary<string, object>)explanation["effectiveWeights"];
        Assert.True(weights.Count > 0, "effectiveWeights should contain at least one entry");
    }

    [Fact]
    public void ExplainCandidateScoring_ContainsCandidateBreakdowns()
    {
        var (domain, actorId, actor, world) = CreateSetup();
        var resources = new EmptyResourceAvailability();
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);
        var explanation = domain.ExplainCandidateScoring(actorId, actor, world, 0L, candidates)!;

        var breakdowns = (List<object>)explanation["candidateBreakdowns"];
        Assert.Equal(candidates.Count, breakdowns.Count);
    }

    [Fact]
    public void ExplainCandidateScoring_BreakdownFinalScoreMatchesDomainScore()
    {
        // The finalPreVarietyScore in the explanation must equal the domain-computed Score
        // (i.e., IntrinsicScore + sum of quality contributions).
        var (domain, actorId, actor, world) = CreateSetup();
        var resources = new EmptyResourceAvailability();
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);
        var explanation = domain.ExplainCandidateScoring(actorId, actor, world, 0L, candidates)!;

        var breakdowns = (List<object>)explanation["candidateBreakdowns"];
        foreach (var (candidate, breakdownObj) in candidates.Zip(breakdowns))
        {
            var bd = (Dictionary<string, object>)breakdownObj;
            var explainedScore = (double)bd["finalPreVarietyScore"];

            // Allow tiny floating-point epsilon
            Assert.Equal(candidate.Score, explainedScore, precision: 5);
        }
    }

    [Fact]
    public void ExplainCandidateScoring_BreakdownContainsQualityContributions()
    {
        var (domain, actorId, actor, world) = CreateSetup(satiety: 10.0); // high hunger → big FoodConsumption weight
        var resources = new EmptyResourceAvailability();
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);
        var explanation = domain.ExplainCandidateScoring(actorId, actor, world, 0L, candidates)!;

        var breakdowns = (List<object>)explanation["candidateBreakdowns"];
        // Find a candidate with qualities
        var withQualities = candidates
            .Zip(breakdowns)
            .FirstOrDefault(pair => ((Dictionary<string, object>)pair.Second).ContainsKey("qualityContributions") &&
                ((Dictionary<string, object>)((Dictionary<string, object>)pair.Second)["qualityContributions"]).Count > 0);

        if (withQualities.First != null)
        {
            var bd = (Dictionary<string, object>)withQualities.Second;
            var contribs = (Dictionary<string, object>)bd["qualityContributions"];
            Assert.True(contribs.Count > 0);
            // Each entry should have qualityValue, effectiveWeight, contribution
            var first = (Dictionary<string, object>)contribs.Values.First();
            Assert.True(first.ContainsKey("qualityValue"));
            Assert.True(first.ContainsKey("effectiveWeight"));
            Assert.True(first.ContainsKey("contribution"));
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 2. OrderCandidatesForSelection with debug sink — exploit branch
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ExploitBranch_SinkCaptures_ExploitBranch()
    {
        var (domain, actorId, actor, world) = CreateSetup(pragmatism: 1.0);
        var resources = new EmptyResourceAvailability();
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);
        var sorted = candidates
            .OrderByDescending(c => c.Score)
            .ThenBy(c => c.Action.Id.Value)
            .ThenBy(c => c.ProviderItemId ?? "")
            .ToList();

        var sink = new CandidateOrderingDebugSink();
        domain.OrderCandidatesForSelection(actorId, actor, world, 0L, sorted, new Random(42), sink);

        Assert.Equal("exploit", sink.OrderingBranch);
        Assert.Equal(1.0, sink.DecisionPragmatism!.Value, precision: 5);
        Assert.NotNull(sink.OriginalTopActionId);
        Assert.Equal(sink.OriginalTopActionId, sink.ChosenActionId);
        Assert.Equal(1, sink.ChosenOriginalRank);
    }

    [Fact]
    public void ExploitBranch_SinkCaptures_OriginalTopActionId()
    {
        var (domain, actorId, actor, world) = CreateSetup(pragmatism: 1.0);
        var resources = new EmptyResourceAvailability();
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);
        var sorted = candidates
            .OrderByDescending(c => c.Score)
            .ThenBy(c => c.Action.Id.Value)
            .ThenBy(c => c.ProviderItemId ?? "")
            .ToList();

        var sink = new CandidateOrderingDebugSink();
        domain.OrderCandidatesForSelection(actorId, actor, world, 0L, sorted, new Random(42), sink);

        Assert.Equal(sorted[0].Action.Id.Value, sink.OriginalTopActionId);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 3. OrderCandidatesForSelection with debug sink — explore branch
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ExploreBranch_SinkCaptures_ExploreBranch()
    {
        var (domain, actorId, actor, world) = CreateSetup(pragmatism: 0.0);
        var resources = new EmptyResourceAvailability();
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);
        var sorted = candidates
            .OrderByDescending(c => c.Score)
            .ThenBy(c => c.Action.Id.Value)
            .ThenBy(c => c.ProviderItemId ?? "")
            .ToList();

        var sink = new CandidateOrderingDebugSink();
        // Pragmatism=0 means rng.NextDouble() always >= p=0, so we always take explore branch
        domain.OrderCandidatesForSelection(actorId, actor, world, 0L, sorted, new Random(42), sink);

        Assert.Equal("explore", sink.OrderingBranch);
        Assert.Equal(0.0, sink.DecisionPragmatism!.Value, precision: 5);
        Assert.NotNull(sink.Spontaneity);
        Assert.Equal(1.0, sink.Spontaneity!.Value, precision: 5);
        Assert.NotNull(sink.Temperature);
        Assert.True(sink.Temperature!.Value > 0.0);
    }

    [Fact]
    public void ExploreBranch_SinkCaptures_SoftmaxWeights()
    {
        var (domain, actorId, actor, world) = CreateSetup(pragmatism: 0.0);
        var resources = new EmptyResourceAvailability();
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);
        Assert.True(candidates.Count >= 2, "Need at least 2 candidates");
        var sorted = candidates
            .OrderByDescending(c => c.Score)
            .ThenBy(c => c.Action.Id.Value)
            .ThenBy(c => c.ProviderItemId ?? "")
            .ToList();

        var sink = new CandidateOrderingDebugSink();
        domain.OrderCandidatesForSelection(actorId, actor, world, 0L, sorted, new Random(99), sink);

        Assert.NotNull(sink.SoftmaxWeightDetails);
        Assert.Equal(sorted.Count, sink.SoftmaxWeightDetails!.Count);

        // All probabilities should be in (0, 1] and sum to ~1
        var totalProb = sink.SoftmaxWeightDetails.Sum(e => e.Probability);
        Assert.Equal(1.0, totalProb, precision: 5);
        Assert.All(sink.SoftmaxWeightDetails, e => Assert.True(e.Probability > 0));
    }

    [Fact]
    public void ExploreBranch_SinkCaptures_ChosenOriginalRank()
    {
        var (domain, actorId, actor, world) = CreateSetup(pragmatism: 0.0);
        var resources = new EmptyResourceAvailability();
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);
        var sorted = candidates
            .OrderByDescending(c => c.Score)
            .ThenBy(c => c.Action.Id.Value)
            .ThenBy(c => c.ProviderItemId ?? "")
            .ToList();

        var sink = new CandidateOrderingDebugSink();
        var ordered = domain.OrderCandidatesForSelection(actorId, actor, world, 0L, sorted, new Random(99), sink);

        // ChosenOriginalRank should match where ordered[0] sits in sorted
        var chosenId = ordered[0].Action.Id.Value;
        var expectedRank = sorted.FindIndex(c => c.Action.Id.Value == chosenId) + 1;
        Assert.Equal(expectedRank, sink.ChosenOriginalRank);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 4. Six-param overload still works (backward compat)
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void SixParamOverload_ProducesIdenticalResultToSevenParamWithNullSink()
    {
        var (domain, actorId, actor, world) = CreateSetup(pragmatism: 1.0);
        var resources = new EmptyResourceAvailability();
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);
        var sorted = candidates
            .OrderByDescending(c => c.Score)
            .ThenBy(c => c.Action.Id.Value)
            .ThenBy(c => c.ProviderItemId ?? "")
            .ToList();

        var result6 = domain.OrderCandidatesForSelection(actorId, actor, world, 0L, sorted, new Random(42));
        var result7 = domain.OrderCandidatesForSelection(actorId, actor, world, 0L, sorted, new Random(42), null);

        Assert.Equal(result6.Count, result7.Count);
        for (var i = 0; i < result6.Count; i++)
            Assert.Equal(result6[i].Action.Id.Value, result7[i].Action.Id.Value);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 5. End-to-end engine verbose trace includes island scoring explanation
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void VerboseEngine_DecisionSelected_IncludesIslandScoringExplanation()
    {
        var domain = new IslandDomainPack();
        var sink = new InMemoryTraceSink();
        var opts = new JohnnyLike.Engine.DecisionTraceOptions(JohnnyLike.Engine.DecisionTraceLevel.Verbose);
        var engine = new JohnnyLike.Engine.Engine(domain, 42, sink, opts);

        engine.AddActor(new ActorId("Johnny"), new Dictionary<string, object>
        {
            ["satiety"] = 40.0, ["energy"] = 60.0, ["morale"] = 50.0
        });

        engine.TryGetNextAction(new ActorId("Johnny"), out _);

        var selected = sink.GetEvents().FirstOrDefault(e => e.EventType == "DecisionSelected");
        Assert.NotNull(selected);
        Assert.True(selected!.Details.ContainsKey("scoringExplanation"),
            "DecisionSelected should include scoringExplanation at Verbose level");

        var json = selected.Details["scoringExplanation"].ToString()!;
        Assert.Contains("actorStats", json);
        Assert.Contains("pressures", json);
        Assert.Contains("effectiveWeights", json);
        Assert.Contains("candidateBreakdowns", json);
    }

    [Fact]
    public void VerboseEngine_DecisionOrderingApplied_CapturedForIslandPragmatism()
    {
        var domain = new IslandDomainPack();
        var sink = new InMemoryTraceSink();
        var opts = new JohnnyLike.Engine.DecisionTraceOptions(JohnnyLike.Engine.DecisionTraceLevel.Summary);
        var engine = new JohnnyLike.Engine.Engine(domain, 42, sink, opts);

        // Add actor with explicit pragmatism=1 (exploit branch guaranteed)
        engine.AddActor(new ActorId("Johnny"), new Dictionary<string, object>
        {
            ["satiety"] = 70.0, ["energy"] = 80.0, ["morale"] = 60.0
        });
        // Force pragmatism by directly setting on actor state
        var actor = (IslandActorState)engine.Actors[new ActorId("Johnny")];
        actor.DecisionPragmatism = 1.0;

        engine.TryGetNextAction(new ActorId("Johnny"), out _);

        var orderingEvt = sink.GetEvents().FirstOrDefault(e => e.EventType == "DecisionOrderingApplied");
        Assert.NotNull(orderingEvt);
        Assert.Equal("exploit", orderingEvt!.Details["orderingBranch"].ToString());
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 6. personalityInfluence section — trait values, per-quality breakdowns
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A focused "stat actor" with non-default abilities so trait values are
    /// clearly non-zero and easy to verify deterministically.
    /// STR=10, DEX=10, CON=10, INT=14, WIS=16, CHA=10
    ///   planner     = Norm(14,16) = (30-20)/20 = 0.5
    ///   craftsman   = Norm(10,14) = (24-20)/20 = 0.2
    ///   survivor    = Norm(10,16) = (26-20)/20 = 0.3
    ///   hedonist    = Norm(10,10) = (20-20)/20 = 0.0
    ///   instinctive = Norm(10,10) = (20-20)/20 = 0.0
    ///   industrious = Norm(10,10) = (20-20)/20 = 0.0
    /// </summary>
    private static (IslandDomainPack domain, ActorId actorId, IslandActorState actor, IslandWorldState world)
        CreateStatActor()
    {
        var domain  = new IslandDomainPack();
        var actorId = new ActorId("StatTester");
        var actor   = (IslandActorState)domain.CreateActorState(actorId, new Dictionary<string, object>
        {
            ["satiety"] = 70.0,
            ["energy"]  = 80.0,
            ["morale"]  = 60.0,
            ["STR"] = 10,
            ["DEX"] = 10,
            ["CON"] = 10,
            ["INT"] = 14,
            ["WIS"] = 16,
            ["CHA"] = 10
        });
        actor.Health = 100.0;
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        domain.InitializeActorItems(actorId, world);
        return (domain, actorId, actor, world);
    }

    [Fact]
    public void ExplainCandidateScoring_ContainsPersonalityInfluence()
    {
        var (domain, actorId, actor, world) = CreateStatActor();
        var resources  = new EmptyResourceAvailability();
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);
        var explanation = domain.ExplainCandidateScoring(actorId, actor, world, 0L, candidates)!;

        Assert.True(explanation.ContainsKey("personalityInfluence"),
            "scoringExplanation must include 'personalityInfluence'");
        var pi = (Dictionary<string, object>)explanation["personalityInfluence"];
        Assert.True(pi.ContainsKey("traits"));
        Assert.True(pi.ContainsKey("qualityPersonalityBreakdown"));
    }

    [Fact]
    public void ExplainCandidateScoring_PersonalityTraitValues_AreCorrect()
    {
        // For the stat actor: INT=14, WIS=16 → planner = Norm(14,16) = (30-20)/20 = 0.5
        //                     DEX=10, INT=14 → craftsman = Norm(10,14) = (24-20)/20 = 0.2
        //                     CON=10, WIS=16 → survivor  = Norm(10,16) = (26-20)/20 = 0.3
        //                     CHA=10, CON=10 → hedonist  = 0.0
        //                     STR=10, CHA=10 → instinctive = 0.0
        //                     STR=10, DEX=10 → industrious  = 0.0
        var (domain, actorId, actor, world) = CreateStatActor();
        var resources  = new EmptyResourceAvailability();
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);
        var explanation = domain.ExplainCandidateScoring(actorId, actor, world, 0L, candidates)!;

        var pi     = (Dictionary<string, object>)explanation["personalityInfluence"];
        var traits = (Dictionary<string, object>)pi["traits"];

        double TraitValue(string name) =>
            (double)((Dictionary<string, object>)traits[name])["value"];

        Assert.Equal(0.5,  TraitValue("planner"),     precision: 4);
        Assert.Equal(0.2,  TraitValue("craftsman"),   precision: 4);
        Assert.Equal(0.3,  TraitValue("survivor"),    precision: 4);
        Assert.Equal(0.0,  TraitValue("hedonist"),    precision: 4);
        Assert.Equal(0.0,  TraitValue("instinctive"), precision: 4);
        Assert.Equal(0.0,  TraitValue("industrious"), precision: 4);
    }

    [Fact]
    public void ExplainCandidateScoring_PersonalityTraitDetails_ContainSourceAndInputs()
    {
        var (domain, actorId, actor, world) = CreateStatActor();
        var resources  = new EmptyResourceAvailability();
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);
        var explanation = domain.ExplainCandidateScoring(actorId, actor, world, 0L, candidates)!;

        var pi     = (Dictionary<string, object>)explanation["personalityInfluence"];
        var traits = (Dictionary<string, object>)pi["traits"];

        foreach (var traitName in new[] { "planner", "craftsman", "survivor", "hedonist", "instinctive", "industrious" })
        {
            var traitEntry = (Dictionary<string, object>)traits[traitName];
            Assert.True(traitEntry.ContainsKey("value"),  $"{traitName} must have 'value'");
            Assert.True(traitEntry.ContainsKey("source"), $"{traitName} must have 'source'");
            Assert.True(traitEntry.ContainsKey("inputs"), $"{traitName} must have 'inputs'");
        }

        // Spot-check source strings
        Assert.Equal("Norm(INT, WIS)", ((Dictionary<string, object>)traits["planner"])["source"]);
        Assert.Equal("Norm(STR, DEX)", ((Dictionary<string, object>)traits["industrious"])["source"]);
    }

    [Fact]
    public void ExplainCandidateScoring_QualityPersonalityBreakdown_ContainsExpectedQualities()
    {
        var (domain, actorId, actor, world) = CreateStatActor();
        var resources  = new EmptyResourceAvailability();
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);
        var explanation = domain.ExplainCandidateScoring(actorId, actor, world, 0L, candidates)!;

        var pi        = (Dictionary<string, object>)explanation["personalityInfluence"];
        var breakdown = (Dictionary<string, object>)pi["qualityPersonalityBreakdown"];

        // All seven quality types with a personality entry must be present
        foreach (var quality in new[] { "Preparation", "Efficiency", "Mastery", "Comfort", "Safety", "FoodConsumption", "Fun" })
        {
            Assert.True(breakdown.ContainsKey(quality), $"qualityPersonalityBreakdown must contain '{quality}'");
            var entry = (Dictionary<string, object>)breakdown[quality];
            Assert.True(entry.ContainsKey("formula"),         $"{quality} entry must have 'formula'");
            Assert.True(entry.ContainsKey("contributors"),    $"{quality} entry must have 'contributors'");
            Assert.True(entry.ContainsKey("personalityBase"), $"{quality} entry must have 'personalityBase'");
        }
    }

    [Fact]
    public void ExplainCandidateScoring_PersonalityBase_Preparation_MatchesFormula()
    {
        // planner=0.5, industrious=0.0, scale=0.4 → personalityBase = (0.5+0.0)*0.4 = 0.2
        var (domain, actorId, actor, world) = CreateStatActor();
        var resources  = new EmptyResourceAvailability();
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);
        var explanation = domain.ExplainCandidateScoring(actorId, actor, world, 0L, candidates)!;

        var pi        = (Dictionary<string, object>)explanation["personalityInfluence"];
        var breakdown = (Dictionary<string, object>)pi["qualityPersonalityBreakdown"];
        var prep      = (Dictionary<string, object>)breakdown["Preparation"];

        Assert.Equal(0.2, (double)prep["personalityBase"], precision: 4);
        var contributors = (Dictionary<string, object>)prep["contributors"];
        Assert.Equal(0.5, (double)contributors["planner"],     precision: 4);
        Assert.Equal(0.0, (double)contributors["industrious"], precision: 4);
    }

    [Fact]
    public void ExplainCandidateScoring_PersonalityBase_Comfort_IsZeroForHedonistZero()
    {
        // hedonist=0.0 for the stat actor → personalityBase(Comfort) = 0.0 * scale = 0.0
        var (domain, actorId, actor, world) = CreateStatActor();
        var resources  = new EmptyResourceAvailability();
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);
        var explanation = domain.ExplainCandidateScoring(actorId, actor, world, 0L, candidates)!;

        var pi        = (Dictionary<string, object>)explanation["personalityInfluence"];
        var breakdown = (Dictionary<string, object>)pi["qualityPersonalityBreakdown"];
        var comfort   = (Dictionary<string, object>)breakdown["Comfort"];

        Assert.Equal(0.0, (double)comfort["personalityBase"], precision: 4);
    }

    [Fact]
    public void ExplainCandidateScoring_PersonalityBase_AgreesWith_QualityModelDecomposition()
    {
        // personalityInfluence.qualityPersonalityBreakdown[q].personalityBase must match
        // qualityModelDecomposition[q].personalityBase for every quality that appears in both.
        var (domain, actorId, actor, world) = CreateStatActor();
        var resources  = new EmptyResourceAvailability();
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);
        var explanation = domain.ExplainCandidateScoring(actorId, actor, world, 0L, candidates)!;

        var pi                   = (Dictionary<string, object>)explanation["personalityInfluence"];
        var personalityBreakdown = (Dictionary<string, object>)pi["qualityPersonalityBreakdown"];
        var qualityDecomposition = (Dictionary<string, object>)explanation["qualityModelDecomposition"];

        foreach (var quality in personalityBreakdown.Keys)
        {
            if (!qualityDecomposition.ContainsKey(quality))
                continue;

            var piBase  = (double)((Dictionary<string, object>)personalityBreakdown[quality])["personalityBase"];
            var qmdBase = (double)((Dictionary<string, object>)qualityDecomposition[quality])["personalityBase"];
            Assert.Equal(qmdBase, piBase, precision: 4);
        }
    }

    [Fact]
    public void VerboseEngine_ScoringExplanation_ContainsPersonalityInfluence()
    {
        var domain = new IslandDomainPack();
        var sink   = new InMemoryTraceSink();
        var opts   = new JohnnyLike.Engine.DecisionTraceOptions(JohnnyLike.Engine.DecisionTraceLevel.Verbose);
        var engine = new JohnnyLike.Engine.Engine(domain, 42, sink, opts);

        engine.AddActor(new ActorId("Johnny"), new Dictionary<string, object>
        {
            ["satiety"] = 40.0, ["energy"] = 60.0, ["morale"] = 50.0,
            ["STR"] = 10, ["DEX"] = 10, ["CON"] = 10,
            ["INT"] = 14, ["WIS"] = 16, ["CHA"] = 10
        });
        engine.TryGetNextAction(new ActorId("Johnny"), out _);

        var selected = sink.GetEvents().First(e => e.EventType == "DecisionSelected");
        var json     = selected.Details["scoringExplanation"].ToString()!;

        Assert.Contains("personalityInfluence", json);
        Assert.Contains("traits", json);
        Assert.Contains("qualityPersonalityBreakdown", json);
        Assert.Contains("planner", json);
        Assert.Contains("craftsman", json);
    }
}
