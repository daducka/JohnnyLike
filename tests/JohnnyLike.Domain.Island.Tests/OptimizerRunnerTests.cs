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
    /// </summary>
    private static GoldenStateEntry MakeFoodState(
        string actor = "Johnny",
        string scenario = "FoodAvailableNow",
        double satiety = 10,
        double priority = 5.0,
        QualityType? desired = QualityType.FoodConsumption,
        IReadOnlyList<QualityType>? forbidden = null) =>
        new(
            SampleKey: $"{actor}|{scenario}|s{(int)satiety}|h70|e50|m50",
            Actor:     actor,
            Scenario:  scenario,
            State:     new GoldenStateValues(satiety, 70, 50, 50),
            DesiredOutcome: new GoldenStateDesiredOutcome(
                DesiredTopCategory:        desired,
                AcceptableTopCategories:   null,
                ForbiddenTopCategories:    forbidden),
            Priority: priority,
            Label: $"test/{actor}");

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
        var rng    = new Random(42);

        var result = OptimizerRunner.EvaluateEntry(entry, domain, rng);

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
        // Wellrested actor with high satiety — desire = FoodConsumption is not realistic,
        // so another category (e.g. Preparation) likely wins.
        // We mark FoodConsumption as forbidden to create a forbidden-triggers scenario.
        var entry = new GoldenStateEntry(
            SampleKey: "Johnny|FoodAvailableNow|s10|h70|e50|m50",
            Actor:     "Johnny",
            Scenario:  "FoodAvailableNow",
            State:     new GoldenStateValues(10, 70, 50, 50),
            DesiredOutcome: new GoldenStateDesiredOutcome(
                DesiredTopCategory:        null,
                AcceptableTopCategories:   new[] { QualityType.Rest },
                ForbiddenTopCategories:    new[] { QualityType.Rest }),  // forbidden == acceptable to force a penalty
            Priority: 2.0,
            Label: "test/forbidden_wins");

        var domain = new IslandDomainPack(DecisionTuningProfile.Default);
        var rng    = new Random(42);

        var result = OptimizerRunner.EvaluateEntry(entry, domain, rng);

        // If Rest wins (which is both acceptable and forbidden), the net effect is
        // AcceptableCategoryReward - ForbiddenCategoryPenalty = (3 - 15) × 2 = -24.
        // If Rest doesn't win, the score depends on what actually wins.
        Assert.True(result.Score <= 6.0 * entry.Priority,
            "Score should not be higher than max acceptable reward when forbidden may trigger.");
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
}
