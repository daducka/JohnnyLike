using JohnnyLike.Domain.Island;
using JohnnyLike.SimRunner.PressureFuzzer;

namespace JohnnyLike.Domain.Island.Tests;

/// <summary>
/// Tests for profile-injection support in <see cref="PressureFuzzerRunner"/>.
/// Verifies:
///   - Default profile (null option) produces the same output as an explicit production-default profile
///   - An alternate profile can be injected and actually changes scoring results when expected
///   - Profile metadata is correctly embedded in every sample and in the summary
///   - <see cref="DecisionTuningProfile"/> hash and JSON round-trip work correctly
/// </summary>
public class PressureFuzzerProfileTests
{
    // ── Minimal run options that keep tests fast ──────────────────────────────
    private static PressureFuzzerOptions MinimalOptions(DecisionTuningProfile? profile = null) =>
        new(
            ActorFilter:        new[] { "Johnny" },
            ScenarioFilter:     new[] { FuzzerScenarioKind.FoodAvailableNow },
            CoarseGrid:         true,
            TopCandidateCount:  3,
            IncludeGoldenStates: false,
            TuningProfile:      profile);

    // ═══════════════════════════════════════════════════════════════════════
    // 1. Profile metadata embedded in samples
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Run_WithNullProfile_EmbedsDefaultProfileMetadata()
    {
        var samples = PressureFuzzerRunner.Run(MinimalOptions(profile: null));

        Assert.NotEmpty(samples);
        foreach (var s in samples)
        {
            Assert.NotNull(s.TuningProfile);
            Assert.Equal("ProductionDefault", s.TuningProfile.ProfileName);
            Assert.NotNull(s.TuningProfile.ProfileHash);
            Assert.Equal(8, s.TuningProfile.ProfileHash.Length);
        }
    }

    [Fact]
    public void Run_WithExplicitDefaultProfile_EmbedsCorrectMetadata()
    {
        var samples = PressureFuzzerRunner.Run(MinimalOptions(DecisionTuningProfile.Default));

        Assert.NotEmpty(samples);
        Assert.All(samples, s =>
        {
            Assert.Equal("ProductionDefault", s.TuningProfile.ProfileName);
            Assert.Equal(DecisionTuningProfile.Default.ComputeHash(), s.TuningProfile.ProfileHash);
        });
    }

    [Fact]
    public void Run_WithCustomProfile_EmbedsCustomProfileMetadata()
    {
        var custom = new DecisionTuningProfile
        {
            ProfileName = "TestAltProfile",
            Description = "Alternate profile for testing"
        };

        var samples = PressureFuzzerRunner.Run(MinimalOptions(custom));

        Assert.NotEmpty(samples);
        Assert.All(samples, s =>
        {
            Assert.Equal("TestAltProfile", s.TuningProfile.ProfileName);
            Assert.Equal("Alternate profile for testing", s.TuningProfile.ProfileDescription);
            Assert.Equal(custom.ComputeHash(), s.TuningProfile.ProfileHash);
        });
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 2. Default behavior unchanged
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Run_NullProfile_ProducesIdenticalScoresToExplicitDefault()
    {
        var samplesNull    = PressureFuzzerRunner.Run(MinimalOptions(profile: null));
        var samplesDefault = PressureFuzzerRunner.Run(MinimalOptions(DecisionTuningProfile.Default));

        Assert.Equal(samplesNull.Count, samplesDefault.Count);

        for (int i = 0; i < samplesNull.Count; i++)
        {
            var a = samplesNull[i];
            var b = samplesDefault[i];

            Assert.Equal(a.SampleKey, b.SampleKey);

            // Top-candidate rankings must be identical.
            Assert.Equal(a.TopCandidates.Count, b.TopCandidates.Count);
            for (int j = 0; j < a.TopCandidates.Count; j++)
            {
                Assert.Equal(a.TopCandidates[j].Action, b.TopCandidates[j].Action);
                Assert.Equal(a.TopCandidates[j].Score,  b.TopCandidates[j].Score);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3. Alternate profiles actually change results
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Run_WithDifferentProfile_ProducesDifferentScoresWhenExpected()
    {
        var defaultSamples = PressureFuzzerRunner.Run(MinimalOptions(profile: null));

        // Drastically inflate the fatigue pressure scale so Rest dominates.
        var altProfile = new DecisionTuningProfile
        {
            ProfileName = "AltRestHeavy",
            Need = new NeedTuning { FatiguePressureRestScale = 0.5 }   // 0.5 vs default 0.015 (~33×)
        };
        var altSamples = PressureFuzzerRunner.Run(MinimalOptions(altProfile));

        Assert.Equal(defaultSamples.Count, altSamples.Count);

        // At least some samples must differ in their top-candidate or score.
        bool anyDifference = defaultSamples
            .Zip(altSamples)
            .Any(pair =>
            {
                var (d, a) = pair;
                if (d.TopCandidates.Count == 0 || a.TopCandidates.Count == 0) return false;
                return d.TopCandidates[0].Action != a.TopCandidates[0].Action ||
                       d.TopCandidates[0].Score  != a.TopCandidates[0].Score;
            });

        Assert.True(anyDifference,
            "Expected alternate profile to produce at least one different top-candidate or score, " +
            "but all samples were identical.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 4. Profile hash / JSON round-trip
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void ComputeHash_DefaultProfile_Is8HexChars()
    {
        var hash = DecisionTuningProfile.Default.ComputeHash();
        Assert.Equal(8, hash.Length);
        Assert.True(hash.All(c => "0123456789ABCDEF".Contains(c)),
            $"Hash '{hash}' is not uppercase hex.");
    }

    [Fact]
    public void ComputeHash_SameProfileTwice_ReturnsSameHash()
    {
        var h1 = DecisionTuningProfile.Default.ComputeHash();
        var h2 = DecisionTuningProfile.Default.ComputeHash();
        Assert.Equal(h1, h2);
    }

    [Fact]
    public void ComputeHash_DifferentProfiles_ReturnDifferentHashes()
    {
        var profileA = new DecisionTuningProfile();
        var profileB = new DecisionTuningProfile
        {
            Need = new NeedTuning { FatiguePressureRestScale = 0.999 }
        };
        Assert.NotEqual(profileA.ComputeHash(), profileB.ComputeHash());
    }

    [Fact]
    public void JsonRoundTrip_DefaultProfile_PreservesAllValues()
    {
        var original = DecisionTuningProfile.Default;
        var json     = original.ToJson();
        var restored = DecisionTuningProfile.FromJson(json);

        Assert.Equal(original.ProfileName, restored.ProfileName);
        Assert.Equal(original.ComputeHash(), restored.ComputeHash());
    }

    [Fact]
    public void JsonRoundTrip_CustomProfile_PreservesCustomValues()
    {
        var custom = new DecisionTuningProfile
        {
            ProfileName = "MyCustom",
            Description = "Test round-trip",
            Need = new NeedTuning { FatiguePressureRestScale = 0.07 }
        };

        var restored = DecisionTuningProfile.FromJson(custom.ToJson());

        Assert.Equal("MyCustom",         restored.ProfileName);
        Assert.Equal("Test round-trip",  restored.Description);
        Assert.Equal(0.07,               restored.Need.FatiguePressureRestScale);
        Assert.Equal(custom.ComputeHash(), restored.ComputeHash());
    }

    [Fact]
    public void LoadFromFile_RoundTripsCorrectly()
    {
        var original = new DecisionTuningProfile
        {
            ProfileName = "FileRoundTrip",
            Need = new NeedTuning { FatiguePressureRestScale = 0.042 }
        };

        var tmpPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tmpPath, original.ToJson());
            var loaded = DecisionTuningProfile.LoadFromFile(tmpPath);

            Assert.Equal("FileRoundTrip", loaded.ProfileName);
            Assert.Equal(0.042, loaded.Need.FatiguePressureRestScale);
            Assert.Equal(original.ComputeHash(), loaded.ComputeHash());
        }
        finally
        {
            File.Delete(tmpPath);
        }
    }
}
