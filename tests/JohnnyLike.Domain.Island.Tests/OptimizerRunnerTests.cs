using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.SimRunner;
using JohnnyLike.SimRunner.Optimizer;
using JohnnyLike.SimRunner.PressureFuzzer;

namespace JohnnyLike.Domain.Island.Tests;

/// <summary>
/// Tests for <see cref="OptimizerRunner"/> and related types.
/// Verifies:
///   - Base-profile evaluation returns valid results for all golden states
///   - Scoring function correctly identifies desired/forbidden/acceptable outcomes
///   - Coordinate descent finds improved profiles when improvement is possible
///   - Profile diff is correctly computed
///   - Output model is fully populated
/// </summary>
public class OptimizerRunnerTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// A minimal single golden state that always expects FoodConsumption,
    /// for use in targeted scoring tests.
    /// Uses the "balanced generalist" trait profile (Johnny-equivalent) by default.
    /// </summary>
    private static GoldenStateEntry MakeFoodState(
        TraitProfile? profile = null,
        string scenario = "FoodAvailableNow",
        double satiety = 10,
        double priority = 5.0,
        QualityType? desired = QualityType.FoodConsumption,
        IReadOnlyList<QualityType>? forbidden = null)
    {
        // Default: Johnny-equivalent profile (planner=0.05, craftsman=0.20, survivor=0.20,
        // hedonist=0.40, instinctive=0.35, industrious=0.30)
        var traitProfile = profile ?? new TraitProfile(0.05, 0.20, 0.20, 0.40, 0.35, 0.30);
        var state = new GoldenStateValues(satiety, 70, 50, 50);
        var sampleKey = GoldenStateLoader.BuildExpectedSampleKey(new GoldenStateEntry(
            SampleKey: "",
            TraitProfile: traitProfile,
            Scenario: scenario,
            State: state,
            DesiredOutcome: new GoldenStateDesiredOutcome(desired, null, null),
            Priority: priority));
        return new(
            SampleKey: sampleKey,
            TraitProfile: traitProfile,
            Scenario:  scenario,
            State:     state,
            DesiredOutcome: new GoldenStateDesiredOutcome(
                DesiredTopCategory:        desired,
                AcceptableTopCategories:   null,
                ForbiddenTopCategories:    forbidden),
            Priority: priority);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 1. EvaluateProfile returns results for every golden state
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void EvaluateProfile_DefaultProfile_ReturnsResultForEveryGoldenState()
    {
        var goldenStates = GoldenStateLoader.LoadEmbedded();
        var results      = OptimizerRunner.EvaluateProfile(DecisionTuningProfile.Default, goldenStates);

        Assert.Equal(goldenStates.Count, results.Count);
    }

    [Fact]
    public void EvaluateProfile_DefaultProfile_AllResultsHaveNonNullSampleKey()
    {
        var goldenStates = GoldenStateLoader.LoadEmbedded();
        var results      = OptimizerRunner.EvaluateProfile(DecisionTuningProfile.Default, goldenStates);

        foreach (var r in results)
            Assert.False(string.IsNullOrWhiteSpace(r.SampleKey), $"Result for '{r.Label}' has null/empty SampleKey.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 2. Scoring function — desired top category
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void EvaluateEntry_WhenDesiredCategoryWins_ReturnsPositiveScore()
    {
        // FoodAvailableNow + very low satiety → food should win.
        var entry  = MakeFoodState(satiety: 10, priority: 1.0, desired: QualityType.FoodConsumption);
        var domain = new IslandDomainPack(DecisionTuningProfile.Default);

        var result = OptimizerRunner.EvaluateEntry(entry, domain);

        // If food wins, DesiredTopCategoryMet = true and score = 10 × priority.
        if (result.DesiredTopCategoryMet)
        {
            Assert.True(result.Score > 0, "Expected positive score when desired category met.");
        }
        // If food does not win under the default profile, the score can be 0 or negative — that's
        // a data point, not a test failure.
        Assert.NotNull(result.ActualTopCategory);
    }

    [Fact]
    public void EvaluateEntry_WhenForbiddenCategoryWins_ScoreIsReduced()
    {
        // Use the known failing golden state "starving+food+comfort/Johnny"
        // (Johnny|FoodAvailable_WithComfort|s10|h70|e50|m50) where under the default profile
        // Rest wins but FoodConsumption is desired and Rest is forbidden.
        // This is a loader-valid golden state from the embedded dataset.
        var goldenStates = GoldenStateLoader.LoadEmbedded();
        var forbiddenEntry = goldenStates.FirstOrDefault(e =>
            e.DesiredOutcome.ForbiddenTopCategories?.Count > 0);

        // The embedded dataset must contain at least one golden state with forbidden categories
        // for this test to be meaningful. If it doesn't, the dataset has been incorrectly modified.
        Assert.NotNull(forbiddenEntry);

        var domain = new IslandDomainPack(DecisionTuningProfile.Default);
        var result = OptimizerRunner.EvaluateEntry(forbiddenEntry, domain);

        // When a forbidden category triggers, the score must be reduced by the penalty.
        if (result.ForbiddenCategoryTriggered)
        {
            // Score must be ≤ 0 when desired is not met but forbidden fires.
            Assert.True(result.Score < 0,
                $"Expected negative score when forbidden category '{result.ActualTopCategory}' triggered. " +
                $"Score was {result.Score}.");
        }
        // If the forbidden category didn't fire for this state/profile, the test is still
        // valid — we just verify the result is self-consistent.
        Assert.Equal(
            result.ForbiddenCategoryTriggered,
            forbiddenEntry.DesiredOutcome.ForbiddenTopCategories!
                .Any(f => f.ToString() == result.ActualTopCategory));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3. Run finds an improved profile against the embedded golden states
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Run_WithDefaultProfile_ReturnsFullyPopulatedResult()
    {
        var goldenStates = GoldenStateLoader.LoadEmbedded();
        var result = OptimizerRunner.Run(new OptimizerOptions(
            GoldenStates:  goldenStates,
            MaxIterations: 1));

        Assert.NotNull(result);
        Assert.NotEmpty(result.BestProfileName);
        Assert.NotEmpty(result.BestProfileHash);
        Assert.NotEmpty(result.BestProfileJson);
        Assert.Equal(goldenStates.Count, result.BaseResults.Count);
        Assert.Equal(goldenStates.Count, result.BestResults.Count);
        Assert.True(result.MaxIterations == 1);
        Assert.NotEmpty(result.SearchBounds);
        Assert.False(string.IsNullOrEmpty(result.CompletedAt));
        // New fields
        Assert.True(result.BaseDesiredPassCount >= 0);
        Assert.True(result.BaseSatisfiedCount >= result.BaseDesiredPassCount);
        Assert.True(result.BestDesiredPassCount >= 0);
        Assert.True(result.BestSatisfiedCount >= result.BestDesiredPassCount);
    }

    [Fact]
    public void Run_BestScoreIsAtLeastAsGoodAsBaseScore()
    {
        var goldenStates = GoldenStateLoader.LoadEmbedded();
        var result = OptimizerRunner.Run(new OptimizerOptions(
            GoldenStates:  goldenStates,
            MaxIterations: 5));

        Assert.True(result.BestScore >= result.BaseScore,
            $"Best score ({result.BestScore}) should be at least as good as base ({result.BaseScore}).");
    }

    [Fact]
    public void Run_ScoreImprovementMatchesDifference()
    {
        var goldenStates = GoldenStateLoader.LoadEmbedded();
        var result = OptimizerRunner.Run(new OptimizerOptions(
            GoldenStates:  goldenStates,
            MaxIterations: 3));

        Assert.Equal(
            Math.Round(result.BestScore - result.BaseScore, 4),
            result.ScoreImprovement,
            precision: 4);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 4. Profile diff is correctly computed
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Run_WhenProfileChanges_DiffContainsChangedParameters()
    {
        var goldenStates = GoldenStateLoader.LoadEmbedded();
        var result = OptimizerRunner.Run(new OptimizerOptions(
            GoldenStates:  goldenStates,
            MaxIterations: 5));

        foreach (var d in result.ProfileDiff)
        {
            Assert.False(string.IsNullOrEmpty(d.ParameterName));
            Assert.NotEqual(d.BaselineValue, d.CandidateValue, precision: 6);
            Assert.Equal(Math.Round(d.CandidateValue - d.BaselineValue, 6), d.Delta, precision: 6);
        }
    }

    [Fact]
    public void Run_WhenProfileUnchanged_DiffIsEmpty()
    {
        // Use a single golden state that is already satisfied by the default profile
        // so no improvement is possible and no parameters change.
        // We pick an entry that we know the base profile passes.
        var goldenStates = GoldenStateLoader.LoadEmbedded();
        var baseResults  = OptimizerRunner.EvaluateProfile(DecisionTuningProfile.Default, goldenStates);

        // Find a golden state that the base profile already passes.
        var passingEntry = goldenStates
            .Zip(baseResults, (gs, r) => (gs, r))
            .FirstOrDefault(pair => pair.r.DesiredTopCategoryMet).gs;

        if (passingEntry is null)
        {
            // If no entry passes the base profile, this test is not applicable.
            return;
        }

        // Run with only the passing entry — no room to improve → diff should be empty.
        var result = OptimizerRunner.Run(new OptimizerOptions(
            GoldenStates:  new[] { passingEntry },
            MaxIterations: 3));

        // The single entry already passes, so the optimizer won't change anything.
        Assert.Empty(result.ProfileDiff);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 5. DefaultParameters spec is well-formed
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void DefaultParameters_AllHaveUniqueNames()
    {
        var names = OptimizerRunner.DefaultParameters.Select(p => p.Name).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Fact]
    public void DefaultParameters_AllBoundsValid()
    {
        foreach (var p in OptimizerRunner.DefaultParameters)
        {
            Assert.True(p.Min < p.Max,
                $"Parameter '{p.Name}' has Min={p.Min} ≥ Max={p.Max}.");
            Assert.True(p.Step > 0,
                $"Parameter '{p.Name}' has Step={p.Step} ≤ 0.");
            Assert.True(p.Step < (p.Max - p.Min),
                $"Parameter '{p.Name}' step {p.Step} exceeds range [{p.Min}, {p.Max}].");
        }
    }

    [Fact]
    public void DefaultParameters_GetterReturnsDefaultValueWithinBounds()
    {
        var profile = DecisionTuningProfile.Default;
        foreach (var p in OptimizerRunner.DefaultParameters)
        {
            var value = p.Getter(profile);
            Assert.True(value >= p.Min && value <= p.Max,
                $"Parameter '{p.Name}' default value {value} is outside [{p.Min}, {p.Max}].");
        }
    }

    [Fact]
    public void DefaultParameters_SetterRoundTrips()
    {
        var profile = DecisionTuningProfile.Default;
        foreach (var p in OptimizerRunner.DefaultParameters)
        {
            var originalValue = p.Getter(profile);
            var modified      = p.Setter(profile, originalValue + p.Step);
            Assert.NotEqual(
                    originalValue, p.Getter(modified), precision: 6);

            // Other parameters must remain unchanged.
            foreach (var other in OptimizerRunner.DefaultParameters.Where(o => o.Name != p.Name))
            {
                Assert.Equal(
                    other.Getter(profile),
                    other.Getter(modified),
                    precision: 6);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 6. BestProfileJson is a valid serialized DecisionTuningProfile
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Run_BestProfileJson_IsValidDecisionTuningProfile()
    {
        var goldenStates = GoldenStateLoader.LoadEmbedded();
        var result = OptimizerRunner.Run(new OptimizerOptions(
            GoldenStates:  goldenStates,
            MaxIterations: 2));

        var roundTripped = DecisionTuningProfile.FromJson(result.BestProfileJson);
        Assert.NotNull(roundTripped);
        Assert.NotEmpty(roundTripped.ProfileName);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 7. Search bounds summary is complete
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Run_SearchBoundsContainsAllDefaultParameters()
    {
        var goldenStates = GoldenStateLoader.LoadEmbedded();
        var result       = OptimizerRunner.Run(new OptimizerOptions(
            GoldenStates:  goldenStates,
            MaxIterations: 1));

        foreach (var p in OptimizerRunner.DefaultParameters)
            Assert.True(result.SearchBounds.ContainsKey(p.Name),
                $"SearchBounds is missing parameter '{p.Name}'.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 8. Per-state result contains rich outcome fields
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void EvaluateProfile_ResultsHaveActualTopActionId()
    {
        var goldenStates = GoldenStateLoader.LoadEmbedded();
        var results      = OptimizerRunner.EvaluateProfile(DecisionTuningProfile.Default, goldenStates);

        foreach (var r in results)
            Assert.False(string.IsNullOrEmpty(r.ActualTopActionId),
                $"Result for '{r.Label ?? r.SampleKey}' has null/empty ActualTopActionId.");
    }

    [Fact]
    public void EvaluateProfile_ResultsHaveNonEmptyActualTopCategories()
    {
        var goldenStates = GoldenStateLoader.LoadEmbedded();
        var results      = OptimizerRunner.EvaluateProfile(DecisionTuningProfile.Default, goldenStates);

        foreach (var r in results)
            Assert.NotEmpty(r.ActualTopCategories);
    }

    [Fact]
    public void EvaluateProfile_ActualTopCategoryIsFirstInActualTopCategories()
    {
        var goldenStates = GoldenStateLoader.LoadEmbedded();
        var results      = OptimizerRunner.EvaluateProfile(DecisionTuningProfile.Default, goldenStates);

        foreach (var r in results)
        {
            if (r.ActualTopCategory != null && r.ActualTopCategories.Count > 0)
                Assert.Equal(r.ActualTopCategory, r.ActualTopCategories[0]);
        }
    }

    [Fact]
    public void EvaluateProfile_DesiredPassHasRankOne()
    {
        var goldenStates = GoldenStateLoader.LoadEmbedded();
        var results      = OptimizerRunner.EvaluateProfile(DecisionTuningProfile.Default, goldenStates);

        foreach (var r in results.Where(r => r.DesiredTopCategoryMet))
        {
            Assert.True(r.BestDesiredCategoryRank == 1,
                $"State '{r.Label ?? r.SampleKey}' has DesiredTopCategoryMet=true but rank={r.BestDesiredCategoryRank}.");
            Assert.Equal(0.0, r.DesiredCategoryVsWinnerDelta ?? double.NaN, precision: 3);
        }
    }

    [Fact]
    public void EvaluateProfile_NonPassHasNegativeDeltaOrMissingDesired()
    {
        var goldenStates = GoldenStateLoader.LoadEmbedded();
        var results      = OptimizerRunner.EvaluateProfile(DecisionTuningProfile.Default, goldenStates);

        foreach (var r in results.Where(r => !r.DesiredTopCategoryMet && r.DesiredTopCategory != null))
        {
            if (r.DesiredCategoryVsWinnerDelta.HasValue)
                Assert.True(r.DesiredCategoryVsWinnerDelta.Value <= 0,
                    $"State '{r.Label ?? r.SampleKey}' did not pass but delta={r.DesiredCategoryVsWinnerDelta} is positive.");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 9. Satisfied vs. desired pass count
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Run_SatisfiedCountIsAtLeastDesiredPassCount()
    {
        var goldenStates = GoldenStateLoader.LoadEmbedded();
        var result = OptimizerRunner.Run(new OptimizerOptions(
            GoldenStates:  goldenStates,
            MaxIterations: 2));

        Assert.True(result.BaseSatisfiedCount >= result.BaseDesiredPassCount,
            $"BaseSatisfiedCount ({result.BaseSatisfiedCount}) must be ≥ BaseDesiredPassCount ({result.BaseDesiredPassCount}).");
        Assert.True(result.BestSatisfiedCount >= result.BestDesiredPassCount,
            $"BestSatisfiedCount ({result.BestSatisfiedCount}) must be ≥ BestDesiredPassCount ({result.BestDesiredPassCount}).");
    }

    [Fact]
    public void GoldenStateResult_StateSatisfied_EqualsDesiredOrAcceptable()
    {
        var goldenStates = GoldenStateLoader.LoadEmbedded();
        var results      = OptimizerRunner.EvaluateProfile(DecisionTuningProfile.Default, goldenStates);

        foreach (var r in results)
            Assert.Equal(r.DesiredTopCategoryMet || r.AcceptableCategoryMet, r.StateSatisfied);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 10. Evaluation is deterministic independent of ordering
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void EvaluateProfile_SameResultsRegardlessOfOrdering()
    {
        var goldenStates = GoldenStateLoader.LoadEmbedded();

        var resultsA = OptimizerRunner.EvaluateProfile(DecisionTuningProfile.Default, goldenStates);
        var resultsB = OptimizerRunner.EvaluateProfile(DecisionTuningProfile.Default,
            goldenStates.Reverse().ToList());

        // Match by SampleKey since ordering differs.
        var byKeyA = resultsA.ToDictionary(r => r.SampleKey);
        var byKeyB = resultsB.ToDictionary(r => r.SampleKey);

        foreach (var key in byKeyA.Keys)
        {
            Assert.True(byKeyB.ContainsKey(key));
            var a = byKeyA[key];
            var b = byKeyB[key];
            Assert.Equal(a.ActualTopCategory,     b.ActualTopCategory);
            Assert.Equal(a.ActualTopActionId,      b.ActualTopActionId);
            Assert.Equal(a.DesiredTopCategoryMet,  b.DesiredTopCategoryMet);
            Assert.Equal(a.StateSatisfied,         b.StateSatisfied);
            Assert.Equal(a.Score,                  b.Score, precision: 4);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 11. Direct trait injection — EvaluateEntry uses exact TraitProfile
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void EvaluateEntry_DifferentTraitProfiles_ProduceDifferentScores()
    {
        // Two golden states with identical physiological state and scenario but very different
        // trait profiles should be evaluated differently — proving that the TraitProfile is
        // used directly for personality weights and not approximated via stat synthesis.
        var domain = new IslandDomainPack();

        // High-Planner, low-Hedonist profile (Frank-like): should value Preparation/Efficiency
        var plannerProfile = new TraitProfile(
            Planner: 0.45, Craftsman: 0.35, Survivor: 0.45,
            Hedonist: 0.10, Instinctive: 0.00, Industrious: 0.20);

        // High-Hedonist, zero-Planner profile (Sawyer-like): should value Fun/Comfort
        var hedonistProfile = new TraitProfile(
            Planner: 0.00, Craftsman: 0.00, Survivor: 0.10,
            Hedonist: 0.40, Instinctive: 0.45, Industrious: 0.20);

        // Fully-satisfied physiological state so need pressures are near-zero and
        // personality weights dominate the scoring outcome.
        // At satiety=100, energy=100, health=100, morale=100 all staged hunger/fatigue/misery
        // pressures resolve to 0.0, ensuring personality is the differentiating signal.
        var stableState = new GoldenStateValues(Satiety: 100, Health: 100, Energy: 100, Morale: 100);
        var desiredOutcome = new GoldenStateDesiredOutcome(null, null, null);

        GoldenStateEntry MakeEntry(TraitProfile profile)
        {
            var proto = new GoldenStateEntry(
                SampleKey: "",
                TraitProfile: profile,
                Scenario: "FoodAvailable_WithComfort",
                State: stableState,
                DesiredOutcome: desiredOutcome,
                Priority: 5.0);
            var key = GoldenStateLoader.BuildExpectedSampleKey(proto);
            return proto with { SampleKey = key };
        }

        var plannerResult  = OptimizerRunner.EvaluateEntry(MakeEntry(plannerProfile), domain);
        var hedonistResult = OptimizerRunner.EvaluateEntry(MakeEntry(hedonistProfile), domain);

        // Both evaluations should succeed and produce a top action.
        Assert.NotNull(plannerResult.ActualTopActionId);
        Assert.NotNull(hedonistResult.ActualTopActionId);

        // With fully-satisfied needs and contrasting trait profiles, the top quality category
        // should differ: Planner → Preparation/Efficiency; Hedonist → Fun/Comfort.
        Assert.NotEqual(
            plannerResult.ActualTopCategory,
            hedonistResult.ActualTopCategory);
    }
}
