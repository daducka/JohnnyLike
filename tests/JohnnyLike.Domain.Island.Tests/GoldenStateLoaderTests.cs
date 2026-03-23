using JohnnyLike.Domain.Abstractions;
using JohnnyLike.SimRunner;
using JohnnyLike.SimRunner.PressureFuzzer;

namespace JohnnyLike.Domain.Island.Tests;

/// <summary>
/// Tests for <see cref="GoldenStateLoader"/>: parsing, validation, and the bundled dataset.
/// </summary>
public class GoldenStateLoaderTests
{
    // ═══════════════════════════════════════════════════════════════════════
    // 1. Embedded dataset structural checks
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
        {
            var expected = GoldenStateLoader.BuildExpectedSampleKey(e);
            Assert.Equal(expected, e.SampleKey);
        }
    }

    [Fact]
    public void LoadEmbedded_AllEntriesHaveValidActor()
    {
        var entries = GoldenStateLoader.LoadEmbedded();
        foreach (var e in entries)
            Assert.Contains(e.Actor, Archetypes.All.Keys);
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
            bool hasDesired    = e.DesiredOutcome.DesiredTopCategory.HasValue;
            bool hasAcceptable = e.DesiredOutcome.AcceptableTopCategories?.Count > 0;
            Assert.True(hasDesired || hasAcceptable,
                $"Entry '{e.SampleKey}' has no desired or acceptable categories.");
        }
    }

    [Fact]
    public void LoadEmbedded_AllSampleKeysUnique()
    {
        var entries = GoldenStateLoader.LoadEmbedded();
        var keys    = entries.Select(e => e.SampleKey).ToList();
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void LoadEmbedded_HasExpectedBucketCoverage()
    {
        var entries = GoldenStateLoader.LoadEmbedded();

        // Each core scenario bucket must have at least one entry.
        Assert.Contains(entries, e => e.Scenario == nameof(FuzzerScenarioKind.FoodAvailableNow));
        Assert.Contains(entries, e => e.Scenario == nameof(FuzzerScenarioKind.NoFood_SourceAvailable));
        Assert.Contains(entries, e => e.Scenario == nameof(FuzzerScenarioKind.FoodAvailable_WithComfort));

        // Core survival categories must appear in desired or acceptable outcomes.
        Assert.Contains(entries, e =>
            e.DesiredOutcome.DesiredTopCategory == QualityType.FoodConsumption ||
            (e.DesiredOutcome.AcceptableTopCategories?.Contains(QualityType.FoodConsumption) ?? false));

        Assert.Contains(entries, e =>
            e.DesiredOutcome.DesiredTopCategory == QualityType.FoodAcquisition ||
            (e.DesiredOutcome.AcceptableTopCategories?.Contains(QualityType.FoodAcquisition) ?? false));
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
        Assert.Equal(QualityType.FoodConsumption, e.DesiredOutcome.DesiredTopCategory);
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
        Assert.Equal(QualityType.FoodAcquisition, e.DesiredOutcome.DesiredTopCategory);
        Assert.Equal(new[] { QualityType.FoodConsumption }, e.DesiredOutcome.AcceptableTopCategories);
        Assert.Equal(new[] { QualityType.Fun, QualityType.Comfort }, e.DesiredOutcome.ForbiddenTopCategories);
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
        Assert.Equal(new[] { QualityType.Rest, QualityType.FoodConsumption },
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
    public void LoadFromJson_UnknownActor_ThrowsValidationException()
    {
        // "Jhonny" is a typo — not a known archetype.
        const string json = """
            [
              {
                "sampleKey": "Jhonny|FoodAvailableNow|s10|h70|e50|m50",
                "actor": "Jhonny",
                "scenario": "FoodAvailableNow",
                "state": { "satiety": 10, "health": 70, "energy": 50, "morale": 50 },
                "desiredOutcome": { "desiredTopCategory": "FoodConsumption" },
                "priority": 10
              }
            ]
            """;

        var ex = Assert.Throws<GoldenStateValidationException>(
            () => GoldenStateLoader.LoadFromJson(json));
        Assert.Contains("Jhonny", ex.Message);
    }

    [Fact]
    public void LoadFromJson_ActorWrongCase_ThrowsValidationException()
    {
        // Actor names are case-sensitive; "johnny" should not match "Johnny".
        const string json = """
            [
              {
                "sampleKey": "johnny|FoodAvailableNow|s10|h70|e50|m50",
                "actor": "johnny",
                "scenario": "FoodAvailableNow",
                "state": { "satiety": 10, "health": 70, "energy": 50, "morale": 50 },
                "desiredOutcome": { "desiredTopCategory": "FoodConsumption" },
                "priority": 10
              }
            ]
            """;

        Assert.Throws<GoldenStateValidationException>(
            () => GoldenStateLoader.LoadFromJson(json));
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

        // JsonStringEnumConverter rejects unknown enum values during parsing,
        // and the GoldenStateLoader re-wraps the JsonException.
        Assert.Throws<GoldenStateValidationException>(
            () => GoldenStateLoader.LoadFromJson(json));
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

        // JsonStringEnumConverter rejects "BogusCategory" during parsing.
        Assert.Throws<GoldenStateValidationException>(
            () => GoldenStateLoader.LoadFromJson(json));
    }

    [Fact]
    public void LoadFromJson_SampleKeyMismatch_ThrowsValidationException()
    {
        // SampleKey says s99 but state has satiety=10.
        const string json = """
            [
              {
                "sampleKey": "Johnny|FoodAvailableNow|s99|h70|e50|m50",
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
        Assert.Contains("s99", ex.Message);
    }

    [Fact]
    public void LoadFromJson_DuplicateSampleKeys_ThrowsValidationException()
    {
        // Two identical entries — duplicate SampleKey must be rejected.
        const string json = """
            [
              {
                "sampleKey": "Johnny|FoodAvailableNow|s10|h70|e50|m50",
                "actor": "Johnny",
                "scenario": "FoodAvailableNow",
                "state": { "satiety": 10, "health": 70, "energy": 50, "morale": 50 },
                "desiredOutcome": { "desiredTopCategory": "FoodConsumption" },
                "priority": 10
              },
              {
                "sampleKey": "Johnny|FoodAvailableNow|s10|h70|e50|m50",
                "actor": "Johnny",
                "scenario": "FoodAvailableNow",
                "state": { "satiety": 10, "health": 70, "energy": 50, "morale": 50 },
                "desiredOutcome": { "desiredTopCategory": "Rest" },
                "priority": 5
              }
            ]
            """;

        var ex = Assert.Throws<GoldenStateValidationException>(
            () => GoldenStateLoader.LoadFromJson(json));
        Assert.Contains("Duplicate", ex.Message);
        Assert.Contains("Johnny|FoodAvailableNow|s10|h70|e50|m50", ex.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void LoadFromJson_StatOutOfRange_ThrowsValidationException(double badValue)
    {
        // Note: we embed the stat value in both the sampleKey and state fields so they
        // match each other; the stat-range check fires before the SampleKey check.
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

    [Fact]
    public void LoadFromJson_NumericDesiredTopCategory_ThrowsValidationException()
    {
        // JsonStringEnumConverter can let numeric payloads through as undefined enum values.
        // The loader must explicitly reject them via Enum.IsDefined.
        const string json = """
            [
              {
                "sampleKey": "Johnny|FoodAvailableNow|s10|h70|e50|m50",
                "actor": "Johnny",
                "scenario": "FoodAvailableNow",
                "state": { "satiety": 10, "health": 70, "energy": 50, "morale": 50 },
                "desiredOutcome": { "desiredTopCategory": 9999 },
                "priority": 10
              }
            ]
            """;

        Assert.Throws<GoldenStateValidationException>(
            () => GoldenStateLoader.LoadFromJson(json));
    }

    [Fact]
    public void LoadFromJson_NumericAcceptableCategory_ThrowsValidationException()
    {
        const string json = """
            [
              {
                "sampleKey": "Johnny|FoodAvailableNow|s10|h70|e50|m50",
                "actor": "Johnny",
                "scenario": "FoodAvailableNow",
                "state": { "satiety": 10, "health": 70, "energy": 50, "morale": 50 },
                "desiredOutcome": { "acceptableTopCategories": [ 9999 ] },
                "priority": 10
              }
            ]
            """;

        Assert.Throws<GoldenStateValidationException>(
            () => GoldenStateLoader.LoadFromJson(json));
    }

    [Fact]
    public void Validate_UndefinedQualityTypeValue_ThrowsValidationException()
    {
        // Simulates a scenario where an undefined numeric enum value slips in at the C# level.
        var entry = new GoldenStateEntry(
            SampleKey:     "Johnny|FoodAvailableNow|s10|h70|e50|m50",
            Actor:         "Johnny",
            Scenario:      "FoodAvailableNow",
            State:         new GoldenStateValues(10, 70, 50, 50),
            DesiredOutcome: new GoldenStateDesiredOutcome(
                (QualityType)9999,
                null,
                null),
            Priority: 10);

        var ex = Assert.Throws<GoldenStateValidationException>(
            () => GoldenStateLoader.Validate(entry));
        Assert.Contains("9999", ex.Message);
        Assert.Contains("defined QualityType", ex.Message);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 6. Contradictory desired outcome validation
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void LoadFromJson_DesiredCategoryAlsoForbidden_ThrowsValidationException()
    {
        // FoodConsumption is both desired and forbidden — must be rejected.
        const string json = """
            [
              {
                "sampleKey": "Johnny|FoodAvailableNow|s10|h70|e50|m50",
                "actor": "Johnny",
                "scenario": "FoodAvailableNow",
                "state": { "satiety": 10, "health": 70, "energy": 50, "morale": 50 },
                "desiredOutcome": {
                  "desiredTopCategory": "FoodConsumption",
                  "forbiddenTopCategories": [ "FoodConsumption" ]
                },
                "priority": 10
              }
            ]
            """;

        var ex = Assert.Throws<GoldenStateValidationException>(
            () => GoldenStateLoader.LoadFromJson(json));
        Assert.Contains("forbidden", ex.Message.ToLowerInvariant());
    }

    [Fact]
    public void LoadFromJson_AcceptableAndForbiddenOverlap_ThrowsValidationException()
    {
        // Rest appears in both acceptable and forbidden — must be rejected.
        const string json = """
            [
              {
                "sampleKey": "Johnny|FoodAvailableNow|s10|h70|e50|m50",
                "actor": "Johnny",
                "scenario": "FoodAvailableNow",
                "state": { "satiety": 10, "health": 70, "energy": 50, "morale": 50 },
                "desiredOutcome": {
                  "desiredTopCategory": "FoodConsumption",
                  "acceptableTopCategories": [ "Rest", "Safety" ],
                  "forbiddenTopCategories": [ "Rest", "Fun" ]
                },
                "priority": 10
              }
            ]
            """;

        var ex = Assert.Throws<GoldenStateValidationException>(
            () => GoldenStateLoader.LoadFromJson(json));
        Assert.Contains("overlap", ex.Message.ToLowerInvariant());
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 7. Validate — direct call
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Validate_ValidEntry_DoesNotThrow()
    {
        var entry = new GoldenStateEntry(
            SampleKey:     "Johnny|FoodAvailableNow|s10|h70|e50|m50",
            Actor:         "Johnny",
            Scenario:      "FoodAvailableNow",
            State:         new GoldenStateValues(10, 70, 50, 50),
            DesiredOutcome: new GoldenStateDesiredOutcome(QualityType.FoodConsumption, null, null),
            Priority:      10);

        // Should not throw
        GoldenStateLoader.Validate(entry);
    }

    [Fact]
    public void Validate_DesiredCategoryAlsoForbidden_ThrowsValidationException()
    {
        var entry = new GoldenStateEntry(
            SampleKey:     "Johnny|FoodAvailableNow|s10|h70|e50|m50",
            Actor:         "Johnny",
            Scenario:      "FoodAvailableNow",
            State:         new GoldenStateValues(10, 70, 50, 50),
            DesiredOutcome: new GoldenStateDesiredOutcome(
                QualityType.FoodConsumption,
                null,
                ForbiddenTopCategories: [QualityType.FoodConsumption]),
            Priority: 10);

        var ex = Assert.Throws<GoldenStateValidationException>(
            () => GoldenStateLoader.Validate(entry));
        Assert.Contains("forbidden", ex.Message.ToLowerInvariant());
    }

    [Fact]
    public void Validate_SampleKeyMismatch_ThrowsValidationException()
    {
        // SampleKey references satiety=99 but State has satiety=10.
        var entry = new GoldenStateEntry(
            SampleKey:     "Johnny|FoodAvailableNow|s99|h70|e50|m50",
            Actor:         "Johnny",
            Scenario:      "FoodAvailableNow",
            State:         new GoldenStateValues(10, 70, 50, 50),
            DesiredOutcome: new GoldenStateDesiredOutcome(QualityType.FoodConsumption, null, null),
            Priority:      10);

        var ex = Assert.Throws<GoldenStateValidationException>(
            () => GoldenStateLoader.Validate(entry));
        Assert.Contains("SampleKey", ex.Message);
    }

    [Fact]
    public void Validate_UnknownActor_ThrowsValidationException()
    {
        var entry = new GoldenStateEntry(
            SampleKey:     "Jhonny|FoodAvailableNow|s10|h70|e50|m50",
            Actor:         "Jhonny",
            Scenario:      "FoodAvailableNow",
            State:         new GoldenStateValues(10, 70, 50, 50),
            DesiredOutcome: new GoldenStateDesiredOutcome(QualityType.FoodConsumption, null, null),
            Priority:      10);

        var ex = Assert.Throws<GoldenStateValidationException>(
            () => GoldenStateLoader.Validate(entry));
        Assert.Contains("Jhonny", ex.Message);
    }
}
