using JohnnyLike.SimRunner.PressureFuzzer;

namespace JohnnyLike.Domain.Island.Tests;

/// <summary>
/// Tests for <see cref="GoldenStateLoader"/>: parsing, validation, and the bundled dataset.
/// </summary>
public class GoldenStateLoaderTests
{
    // ═══════════════════════════════════════════════════════════════════════
    // 1. Embedded dataset
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void LoadEmbedded_ReturnsNonEmptyList()
    {
        var entries = GoldenStateLoader.LoadEmbedded();
        Assert.NotEmpty(entries);
    }

    [Fact]
    public void LoadEmbedded_AllEntriesHaveValidSampleKey()
    {
        var entries = GoldenStateLoader.LoadEmbedded();
        foreach (var e in entries)
            Assert.False(string.IsNullOrWhiteSpace(e.SampleKey),
                $"Entry with label '{e.Label}' has an empty SampleKey.");
    }

    [Fact]
    public void LoadEmbedded_AllEntriesHaveValidActor()
    {
        var validActors = new HashSet<string>(StringComparer.Ordinal)
        {
            "Johnny", "Frank", "Sawyer", "Ashley", "Oscar", "Elizabeth"
        };
        var entries = GoldenStateLoader.LoadEmbedded();
        foreach (var e in entries)
            Assert.Contains(e.Actor, validActors);
    }

    [Fact]
    public void LoadEmbedded_AllEntriesHaveValidScenario()
    {
        var entries = GoldenStateLoader.LoadEmbedded();
        foreach (var e in entries)
        {
            var valid = Enum.TryParse<FuzzerScenarioKind>(e.Scenario, out _);
            Assert.True(valid, $"Entry '{e.SampleKey}' has invalid scenario '{e.Scenario}'.");
        }
    }

    [Fact]
    public void LoadEmbedded_AllStatsInRange()
    {
        var entries = GoldenStateLoader.LoadEmbedded();
        foreach (var e in entries)
        {
            Assert.InRange(e.State.Satiety, 0, 100);
            Assert.InRange(e.State.Health,  0, 100);
            Assert.InRange(e.State.Energy,  0, 100);
            Assert.InRange(e.State.Morale,  0, 100);
        }
    }

    [Fact]
    public void LoadEmbedded_AllPrioritiesPositive()
    {
        var entries = GoldenStateLoader.LoadEmbedded();
        foreach (var e in entries)
            Assert.True(e.Priority > 0, $"Entry '{e.SampleKey}' has non-positive priority {e.Priority}.");
    }

    [Fact]
    public void LoadEmbedded_AllDesiredOutcomesHaveAtLeastOneCategory()
    {
        var entries = GoldenStateLoader.LoadEmbedded();
        foreach (var e in entries)
        {
            bool hasDesired    = !string.IsNullOrWhiteSpace(e.DesiredOutcome.DesiredTopCategory);
            bool hasAcceptable = e.DesiredOutcome.AcceptableTopCategories?.Count > 0;
            Assert.True(hasDesired || hasAcceptable,
                $"Entry '{e.SampleKey}' has no desired or acceptable categories.");
        }
    }

    [Fact]
    public void LoadEmbedded_HasExpectedBucketCoverage()
    {
        var entries = GoldenStateLoader.LoadEmbedded();

        // At least one entry per core bucket
        Assert.Contains(entries, e =>
            e.DesiredOutcome.DesiredTopCategory == nameof(FuzzerScenarioKind.FoodAvailableNow) ||
            e.Scenario == nameof(FuzzerScenarioKind.FoodAvailableNow));

        Assert.Contains(entries, e =>
            e.Scenario == nameof(FuzzerScenarioKind.NoFood_SourceAvailable));

        Assert.Contains(entries, e =>
            e.DesiredOutcome.DesiredTopCategory == "FoodConsumption" ||
            (e.DesiredOutcome.AcceptableTopCategories?.Contains("FoodConsumption") ?? false));
    }

    [Fact]
    public void LoadEmbedded_CoversMinimumStateCount()
    {
        var entries = GoldenStateLoader.LoadEmbedded();
        // The issue asks for 20–40 curated states.
        Assert.True(entries.Count >= 20,
            $"Expected at least 20 golden states, but found {entries.Count}.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 2. LoadFromJson — happy path
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void LoadFromJson_ValidMinimalEntry_Succeeds()
    {
        const string json = """
            [
              {
                "sampleKey": "Johnny|FoodAvailableNow|s10|h70|e50|m50",
                "actor": "Johnny",
                "scenario": "FoodAvailableNow",
                "state": { "satiety": 10, "health": 70, "energy": 50, "morale": 50 },
                "desiredOutcome": { "desiredTopCategory": "FoodConsumption" },
                "priority": 10
              }
            ]
            """;

        var entries = GoldenStateLoader.LoadFromJson(json);

        Assert.Single(entries);
        var e = entries[0];
        Assert.Equal("Johnny|FoodAvailableNow|s10|h70|e50|m50", e.SampleKey);
        Assert.Equal("Johnny", e.Actor);
        Assert.Equal("FoodAvailableNow", e.Scenario);
        Assert.Equal(10, e.State.Satiety);
        Assert.Equal(70, e.State.Health);
        Assert.Equal(50, e.State.Energy);
        Assert.Equal(50, e.State.Morale);
        Assert.Equal("FoodConsumption", e.DesiredOutcome.DesiredTopCategory);
        Assert.Equal(10, e.Priority);
        Assert.Null(e.Label);
    }

    [Fact]
    public void LoadFromJson_EntryWithAllOptionalFields_Succeeds()
    {
        const string json = """
            [
              {
                "sampleKey": "Sawyer|NoFood_SourceAvailable|s10|h70|e50|m50",
                "actor": "Sawyer",
                "scenario": "NoFood_SourceAvailable",
                "state": { "satiety": 10, "health": 70, "energy": 50, "morale": 50 },
                "desiredOutcome": {
                  "desiredTopCategory": "FoodAcquisition",
                  "acceptableTopCategories": [ "FoodConsumption" ],
                  "forbiddenTopCategories": [ "Fun", "Comfort" ],
                  "notes": "Test entry"
                },
                "priority": 9,
                "label": "test+label"
              }
            ]
            """;

        var entries = GoldenStateLoader.LoadFromJson(json);

        var e = entries[0];
        Assert.Equal("FoodAcquisition", e.DesiredOutcome.DesiredTopCategory);
        Assert.Equal(new[] { "FoodConsumption" }, e.DesiredOutcome.AcceptableTopCategories);
        Assert.Equal(new[] { "Fun", "Comfort" }, e.DesiredOutcome.ForbiddenTopCategories);
        Assert.Equal("Test entry", e.DesiredOutcome.Notes);
        Assert.Equal("test+label", e.Label);
    }

    [Fact]
    public void LoadFromJson_EntryWithOnlyAcceptableCategories_Succeeds()
    {
        const string json = """
            [
              {
                "sampleKey": "Frank|LateCollapse|s15|h50|e10|m30",
                "actor": "Frank",
                "scenario": "LateCollapse",
                "state": { "satiety": 15, "health": 50, "energy": 10, "morale": 30 },
                "desiredOutcome": {
                  "acceptableTopCategories": [ "Rest", "FoodConsumption" ]
                },
                "priority": 7
              }
            ]
            """;

        var entries = GoldenStateLoader.LoadFromJson(json);

        Assert.Single(entries);
        Assert.Null(entries[0].DesiredOutcome.DesiredTopCategory);
        Assert.Equal(new[] { "Rest", "FoodConsumption" },
            entries[0].DesiredOutcome.AcceptableTopCategories);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3. LoadFromFile
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void LoadFromFile_ValidFile_Succeeds()
    {
        const string json = """
            [
              {
                "sampleKey": "Johnny|FoodAvailableNow|s10|h70|e50|m50",
                "actor": "Johnny",
                "scenario": "FoodAvailableNow",
                "state": { "satiety": 10, "health": 70, "energy": 50, "morale": 50 },
                "desiredOutcome": { "desiredTopCategory": "FoodConsumption" },
                "priority": 10
              }
            ]
            """;

        var tmpPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tmpPath, json);
            var entries = GoldenStateLoader.LoadFromFile(tmpPath);
            Assert.Single(entries);
        }
        finally
        {
            File.Delete(tmpPath);
        }
    }

    [Fact]
    public void LoadFromFile_MissingFile_ThrowsFileNotFoundException()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), "nonexistent-golden-states.json");
        Assert.Throws<FileNotFoundException>(
            () => GoldenStateLoader.LoadFromFile(missingPath));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 4. Validation — missing required fields
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void LoadFromJson_MissingSampleKey_ThrowsValidationException()
    {
        const string json = """
            [
              {
                "actor": "Johnny",
                "scenario": "FoodAvailableNow",
                "state": { "satiety": 10, "health": 70, "energy": 50, "morale": 50 },
                "desiredOutcome": { "desiredTopCategory": "FoodConsumption" },
                "priority": 10
              }
            ]
            """;

        var ex = Assert.Throws<GoldenStateValidationException>(
            () => GoldenStateLoader.LoadFromJson(json));
        Assert.Contains("SampleKey", ex.Message);
    }

    [Fact]
    public void LoadFromJson_MissingActor_ThrowsValidationException()
    {
        const string json = """
            [
              {
                "sampleKey": "Johnny|FoodAvailableNow|s10|h70|e50|m50",
                "scenario": "FoodAvailableNow",
                "state": { "satiety": 10, "health": 70, "energy": 50, "morale": 50 },
                "desiredOutcome": { "desiredTopCategory": "FoodConsumption" },
                "priority": 10
              }
            ]
            """;

        var ex = Assert.Throws<GoldenStateValidationException>(
            () => GoldenStateLoader.LoadFromJson(json));
        Assert.Contains("Actor", ex.Message);
    }

    [Fact]
    public void LoadFromJson_MissingScenario_ThrowsValidationException()
    {
        const string json = """
            [
              {
                "sampleKey": "Johnny|FoodAvailableNow|s10|h70|e50|m50",
                "actor": "Johnny",
                "state": { "satiety": 10, "health": 70, "energy": 50, "morale": 50 },
                "desiredOutcome": { "desiredTopCategory": "FoodConsumption" },
                "priority": 10
              }
            ]
            """;

        var ex = Assert.Throws<GoldenStateValidationException>(
            () => GoldenStateLoader.LoadFromJson(json));
        Assert.Contains("Scenario", ex.Message);
    }

    [Fact]
    public void LoadFromJson_MissingDesiredOutcome_ThrowsValidationException()
    {
        const string json = """
            [
              {
                "sampleKey": "Johnny|FoodAvailableNow|s10|h70|e50|m50",
                "actor": "Johnny",
                "scenario": "FoodAvailableNow",
                "state": { "satiety": 10, "health": 70, "energy": 50, "morale": 50 },
                "priority": 10
              }
            ]
            """;

        var ex = Assert.Throws<GoldenStateValidationException>(
            () => GoldenStateLoader.LoadFromJson(json));
        Assert.Contains("DesiredOutcome", ex.Message);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 5. Validation — invalid values
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void LoadFromJson_InvalidScenario_ThrowsValidationException()
    {
        const string json = """
            [
              {
                "sampleKey": "Johnny|BadScenario|s10|h70|e50|m50",
                "actor": "Johnny",
                "scenario": "BadScenario",
                "state": { "satiety": 10, "health": 70, "energy": 50, "morale": 50 },
                "desiredOutcome": { "desiredTopCategory": "FoodConsumption" },
                "priority": 10
              }
            ]
            """;

        var ex = Assert.Throws<GoldenStateValidationException>(
            () => GoldenStateLoader.LoadFromJson(json));
        Assert.Contains("BadScenario", ex.Message);
    }

    [Fact]
    public void LoadFromJson_InvalidCategory_ThrowsValidationException()
    {
        const string json = """
            [
              {
                "sampleKey": "Johnny|FoodAvailableNow|s10|h70|e50|m50",
                "actor": "Johnny",
                "scenario": "FoodAvailableNow",
                "state": { "satiety": 10, "health": 70, "energy": 50, "morale": 50 },
                "desiredOutcome": { "desiredTopCategory": "NotACategory" },
                "priority": 10
              }
            ]
            """;

        var ex = Assert.Throws<GoldenStateValidationException>(
            () => GoldenStateLoader.LoadFromJson(json));
        Assert.Contains("NotACategory", ex.Message);
    }

    [Fact]
    public void LoadFromJson_InvalidAcceptableCategory_ThrowsValidationException()
    {
        const string json = """
            [
              {
                "sampleKey": "Johnny|FoodAvailableNow|s10|h70|e50|m50",
                "actor": "Johnny",
                "scenario": "FoodAvailableNow",
                "state": { "satiety": 10, "health": 70, "energy": 50, "morale": 50 },
                "desiredOutcome": {
                  "desiredTopCategory": "FoodConsumption",
                  "acceptableTopCategories": [ "FoodConsumption", "BogusCategory" ]
                },
                "priority": 10
              }
            ]
            """;

        var ex = Assert.Throws<GoldenStateValidationException>(
            () => GoldenStateLoader.LoadFromJson(json));
        Assert.Contains("BogusCategory", ex.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void LoadFromJson_StatOutOfRange_ThrowsValidationException(double badValue)
    {
        var json = $$"""
            [
              {
                "sampleKey": "Johnny|FoodAvailableNow|s{{(int)badValue}}|h70|e50|m50",
                "actor": "Johnny",
                "scenario": "FoodAvailableNow",
                "state": { "satiety": {{badValue}}, "health": 70, "energy": 50, "morale": 50 },
                "desiredOutcome": { "desiredTopCategory": "FoodConsumption" },
                "priority": 10
              }
            ]
            """;

        Assert.Throws<GoldenStateValidationException>(
            () => GoldenStateLoader.LoadFromJson(json));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void LoadFromJson_NonPositivePriority_ThrowsValidationException(double priority)
    {
        var json = $$"""
            [
              {
                "sampleKey": "Johnny|FoodAvailableNow|s10|h70|e50|m50",
                "actor": "Johnny",
                "scenario": "FoodAvailableNow",
                "state": { "satiety": 10, "health": 70, "energy": 50, "morale": 50 },
                "desiredOutcome": { "desiredTopCategory": "FoodConsumption" },
                "priority": {{priority}}
              }
            ]
            """;

        var ex = Assert.Throws<GoldenStateValidationException>(
            () => GoldenStateLoader.LoadFromJson(json));
        Assert.Contains("priority", ex.Message.ToLowerInvariant());
    }

    [Fact]
    public void LoadFromJson_EmptyDesiredOutcome_ThrowsValidationException()
    {
        const string json = """
            [
              {
                "sampleKey": "Johnny|FoodAvailableNow|s10|h70|e50|m50",
                "actor": "Johnny",
                "scenario": "FoodAvailableNow",
                "state": { "satiety": 10, "health": 70, "energy": 50, "morale": 50 },
                "desiredOutcome": {},
                "priority": 10
              }
            ]
            """;

        var ex = Assert.Throws<GoldenStateValidationException>(
            () => GoldenStateLoader.LoadFromJson(json));
        Assert.Contains("AcceptableTopCategories", ex.Message);
    }

    [Fact]
    public void LoadFromJson_EmptyJson_ThrowsValidationException()
    {
        Assert.Throws<GoldenStateValidationException>(
            () => GoldenStateLoader.LoadFromJson("   "));
    }

    [Fact]
    public void LoadFromJson_MalformedJson_ThrowsValidationException()
    {
        Assert.Throws<GoldenStateValidationException>(
            () => GoldenStateLoader.LoadFromJson("{ not valid json }"));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 6. Validate — direct call
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Validate_ValidEntry_DoesNotThrow()
    {
        var entry = new GoldenStateEntry(
            SampleKey:     "Johnny|FoodAvailableNow|s10|h70|e50|m50",
            Actor:         "Johnny",
            Scenario:      "FoodAvailableNow",
            State:         new GoldenStateValues(10, 70, 50, 50),
            DesiredOutcome: new GoldenStateDesiredOutcome("FoodConsumption", null, null),
            Priority:      10);

        // Should not throw
        GoldenStateLoader.Validate(entry);
    }

    [Fact]
    public void Validate_InvalidForbiddenCategory_ThrowsValidationException()
    {
        var entry = new GoldenStateEntry(
            SampleKey:     "Johnny|FoodAvailableNow|s10|h70|e50|m50",
            Actor:         "Johnny",
            Scenario:      "FoodAvailableNow",
            State:         new GoldenStateValues(10, 70, 50, 50),
            DesiredOutcome: new GoldenStateDesiredOutcome(
                "FoodConsumption",
                null,
                ForbiddenTopCategories: ["InvalidCat"]),
            Priority: 10);

        var ex = Assert.Throws<GoldenStateValidationException>(
            () => GoldenStateLoader.Validate(entry));
        Assert.Contains("InvalidCat", ex.Message);
    }
}
