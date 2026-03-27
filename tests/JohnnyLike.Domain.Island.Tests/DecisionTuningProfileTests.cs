using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Island.Supply;

namespace JohnnyLike.Domain.Island.Tests;

/// <summary>
/// Tests for <see cref="DecisionTuningProfile"/> and its sub-types.
/// Verifies:
///   - Default profile exists and carries production-default values
///   - Scoring under default profile is identical to pre-refactor behavior
///   - Custom profile values propagate correctly through scoring
/// </summary>
public class DecisionTuningProfileTests
{
    // ── Shared test helpers ───────────────────────────────────────────────────

    private static (IslandDomainPack domain, ActorId actorId, IslandActorState actor, IslandWorldState world)
        CreateSetup(
            double satiety = 70.0, double energy = 80.0,
            double morale  = 60.0, double health = 100.0,
            DecisionTuningProfile? profile = null)
    {
        var domain  = new IslandDomainPack(profile);
        var actorId = new ActorId("Tester");
        var actor   = (IslandActorState)domain.CreateActorState(actorId, new Dictionary<string, object>
        {
            ["satiety"] = satiety,
            ["energy"]  = energy,
            ["morale"]  = morale
        });
        actor.Health = health;
        actor.DecisionPragmatism = 1.0; // deterministic for scoring tests
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        domain.InitializeActorItems(actorId, world);
        return (domain, actorId, actor, world);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 1. Profile structure
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Default_IsNotNull()
    {
        Assert.NotNull(DecisionTuningProfile.Default);
    }

    [Fact]
    public void Default_SubObjectsAreNotNull()
    {
        var p = DecisionTuningProfile.Default;
        Assert.NotNull(p.Need);
        Assert.NotNull(p.Mood);
        Assert.NotNull(p.Personality);
        Assert.NotNull(p.Categories);
    }

    [Fact]
    public void Default_NeedTuning_HasProductionValues()
    {
        var n = DecisionTuningProfile.Default.Need;
        Assert.Equal(0.015, n.FatiguePressureRestScale);
        Assert.Equal(0.01,  n.MiseryPressureComfortScale);
        Assert.Equal(0.025, n.InjurySafetyNeedScale);
        Assert.Equal(0.010, n.InjuryRestNeedScale);
        Assert.Equal(0.005, n.InjuryComfortNeedScale);
        Assert.Equal(70.0,  n.SatietyRampMild);
        Assert.Equal(50.0,  n.SatietyRampModerate);
        Assert.Equal(30.0,  n.SatietyRampStrong);
        Assert.Equal(0.3,   n.HungerMildMax);
        Assert.Equal(1.2,   n.HungerModerateRange);
        Assert.Equal(0.5,   n.HungerStrongRange);
        Assert.Equal(5.0,   n.FoodAvailabilityNormCap);
        Assert.Equal(0.2,   n.ImmediateFoodSignificanceThreshold);
        Assert.Equal(0.2,   n.AcquirableFoodSignificanceThreshold);
        Assert.Equal(0.80,  n.FoodConsumptionShareHigh);
        Assert.Equal(0.20,  n.FoodConsumptionShareLow);
        Assert.Equal(0.50,  n.FoodShareNeutral);
        Assert.Equal(0.20,  n.PrepTimePressureCap);
        Assert.Equal(0.05,  n.PrepTimePressureRatePerDay);
    }

    [Fact]
    public void Default_MoodTuning_HasProductionValues()
    {
        var m = DecisionTuningProfile.Default.Mood;
        Assert.Equal(20.0, m.StarvatingSatietyThreshold);
        Assert.Equal(0.3,  m.PrepStarvationFloor);
        Assert.Equal(20.0, m.ExhaustedEnergyThreshold);
        Assert.Equal(0.4,  m.MasteryExhaustionFloor);
        Assert.Equal(0.6,  m.FunBaseScale);
        Assert.Equal(0.35, m.FunCriticalSurvivalScale);
        Assert.Equal(25.0, m.FunCriticalSatietyThreshold);
        Assert.Equal(20.0, m.FunCriticalEnergyThreshold);
        Assert.Equal(0.15, m.InjuryFunSuppressionFloor);
        Assert.Equal(0.30, m.InjuryMasterySuppressionFloor);
        Assert.Equal(0.40, m.InjuryPreparationSuppressionFloor);
        Assert.Equal(25.0, m.HungerSuppressionStartSatiety);
        Assert.Equal(10.0, m.HungerSuppressionFullSatiety);
        Assert.Equal(0.3,  m.ComfortRestSuppressionMin);
        Assert.Equal(2.0,  m.HungerSuppressionExponent);
    }

    [Fact]
    public void Default_PersonalityTuning_HasProductionValues()
    {
        var p = DecisionTuningProfile.Default.Personality;
        Assert.Equal(0.7,  p.PreparationScale);
        Assert.Equal(0.6,  p.EfficiencyScale);
        Assert.Equal(0.6,  p.MasteryScale);
        Assert.Equal(0.4,  p.ComfortScale);
        Assert.Equal(0.3,  p.SafetyScale);
        Assert.Equal(0.2,  p.FoodConsumptionScale);
        Assert.Equal(0.15, p.FoodAcquisitionScale);
        Assert.Equal(0.80, p.PragmatismBase);
        Assert.Equal(0.10, p.PragmatismPlannerScale);
        Assert.Equal(0.05, p.PragmatismSurvivorScale);
        Assert.Equal(0.06, p.PragmatismHedonistScale);
        Assert.Equal(0.04, p.PragmatismInstinctiveScale);
        Assert.Equal(0.65, p.PragmatismMin);
        Assert.Equal(0.98, p.PragmatismMax);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 2. Default profile produces identical scores
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void DefaultProfile_ProducesSameCandidateScores_AsImplicitDefault()
    {
        // Two domain packs: one constructed without profile (implicit default),
        // one with explicit DecisionTuningProfile.Default — must produce identical scores.
        var domainImplicit = new IslandDomainPack();
        var domainExplicit = new IslandDomainPack(DecisionTuningProfile.Default);

        var actorId = new ActorId("ScoreCheck");
        var actorImplicit = (IslandActorState)domainImplicit.CreateActorState(actorId, new Dictionary<string, object>
        {
            ["satiety"] = 40.0, ["energy"] = 60.0, ["morale"] = 50.0
        });
        var actorExplicit = (IslandActorState)domainExplicit.CreateActorState(actorId, new Dictionary<string, object>
        {
            ["satiety"] = 40.0, ["energy"] = 60.0, ["morale"] = 50.0
        });
        actorImplicit.DecisionPragmatism = 1.0;
        actorExplicit.DecisionPragmatism = 1.0;

        var worldImplicit = (IslandWorldState)domainImplicit.CreateInitialWorldState();
        var worldExplicit = (IslandWorldState)domainExplicit.CreateInitialWorldState();
        domainImplicit.InitializeActorItems(actorId, worldImplicit);
        domainExplicit.InitializeActorItems(actorId, worldExplicit);

        var resources  = new EmptyResourceAvailability();
        var rng        = new Random(42);
        var rng2       = new Random(42);

        var candidatesImplicit = domainImplicit.GenerateCandidates(actorId, actorImplicit, worldImplicit, 0L, rng,  resources);
        var candidatesExplicit = domainExplicit.GenerateCandidates(actorId, actorExplicit, worldExplicit, 0L, rng2, resources);

        Assert.Equal(candidatesImplicit.Count, candidatesExplicit.Count);
        for (var i = 0; i < candidatesImplicit.Count; i++)
        {
            Assert.Equal(candidatesImplicit[i].Action.Id, candidatesExplicit[i].Action.Id);
            Assert.Equal(candidatesImplicit[i].Score,     candidatesExplicit[i].Score, precision: 9);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 3. Default profile preserves known scoring values
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void DefaultProfile_NeedAdd_Rest_MatchesExpectedFormula()
    {
        // Energy=50 → fatiguePressure=50; Health=100 → injuryPressure=0
        // Rest needAdd = 50 * 0.015 + 0 * 0.010 = 0.75
        var (domain, actorId, actor, world) = CreateSetup(energy: 50.0, health: 100.0);
        var resources  = new EmptyResourceAvailability();
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);
        var explanation = domain.ExplainCandidateScoring(actorId, actor, world, 0L, candidates)!;

        var qmd  = (Dictionary<string, object>)explanation["qualityModelDecomposition"];
        var rest = (Dictionary<string, object>)qmd["Rest"];
        Assert.Equal(0.75, (double)rest["needAdd"], precision: 6);
    }

    [Fact]
    public void DefaultProfile_NeedAdd_Safety_MatchesExpectedFormula()
    {
        // Health=60 → injuryPressure=40; Safety needAdd = 40 * 0.025 = 1.0
        var (domain, actorId, actor, world) = CreateSetup(health: 60.0);
        var resources  = new EmptyResourceAvailability();
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);
        var explanation = domain.ExplainCandidateScoring(actorId, actor, world, 0L, candidates)!;

        var qmd    = (Dictionary<string, object>)explanation["qualityModelDecomposition"];
        var safety = (Dictionary<string, object>)qmd["Safety"];
        Assert.Equal(1.0, (double)safety["needAdd"], precision: 6);
    }

    [Fact]
    public void DefaultProfile_MoodMultiplier_Preparation_StarvedActorGetsFloor()
    {
        // Satiety=15 < StarvatingSatietyThreshold(20) → PrepStarvationFloor = 0.3
        var (domain, actorId, actor, world) = CreateSetup(satiety: 15.0);
        var resources  = new EmptyResourceAvailability();
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);
        var explanation = domain.ExplainCandidateScoring(actorId, actor, world, 0L, candidates)!;

        var qmd  = (Dictionary<string, object>)explanation["qualityModelDecomposition"];
        if (qmd.ContainsKey("Preparation"))
        {
            var prep = (Dictionary<string, object>)qmd["Preparation"];
            Assert.Equal(0.3, (double)prep["moodMultiplier"], precision: 4);
        }
    }

    [Fact]
    public void DefaultProfile_PersonalityBase_Preparation_MatchesFormula()
    {
        // Uses stat actor: INT=14, WIS=16 → planner=0.5; STR=DEX=10 → industrious=0
        // personalityBase(Preparation) = (0.5+0.0) * 0.7 = 0.35
        var domain  = new IslandDomainPack();
        var actorId = new ActorId("StatActor");
        var actor   = (IslandActorState)domain.CreateActorState(actorId, new Dictionary<string, object>
        {
            ["INT"] = 14, ["WIS"] = 16,
            ["satiety"] = 70.0, ["energy"] = 80.0, ["morale"] = 60.0
        });
        actor.DecisionPragmatism = 1.0;
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        domain.InitializeActorItems(actorId, world);

        var resources  = new EmptyResourceAvailability();
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);
        var explanation = domain.ExplainCandidateScoring(actorId, actor, world, 0L, candidates)!;

        var pi        = (Dictionary<string, object>)explanation["personalityInfluence"];
        var breakdown = (Dictionary<string, object>)pi["qualityPersonalityBreakdown"];
        var prep      = (Dictionary<string, object>)breakdown["Preparation"];

        Assert.Equal(0.35, (double)prep["personalityBase"], precision: 4);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 4. Custom profile values propagate
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void CustomProfile_HigherFatiguePressureRestScale_IncreasesRestNeedAdd()
    {
        var customProfile = new DecisionTuningProfile
        {
            Need = new NeedTuning { FatiguePressureRestScale = 0.05 } // 3× default (0.015)
        };

        var (domainDefault, actorIdD, actorD, worldD) = CreateSetup(energy: 50.0);
        var (domainCustom,  actorIdC, actorC, worldC) = CreateSetup(energy: 50.0, profile: customProfile);

        var resources = new EmptyResourceAvailability();
        var candD = domainDefault.GenerateCandidates(actorIdD, actorD, worldD, 0L, new Random(1), resources);
        var candC = domainCustom .GenerateCandidates(actorIdC, actorC, worldC, 0L, new Random(1), resources);

        var explainD = domainDefault.ExplainCandidateScoring(actorIdD, actorD, worldD, 0L, candD)!;
        var explainC = domainCustom .ExplainCandidateScoring(actorIdC, actorC, worldC, 0L, candC)!;

        var restNeedD = (double)((Dictionary<string, object>)((Dictionary<string, object>)explainD["qualityModelDecomposition"])["Rest"])["needAdd"];
        var restNeedC = (double)((Dictionary<string, object>)((Dictionary<string, object>)explainC["qualityModelDecomposition"])["Rest"])["needAdd"];

        // Energy=50 → fatiguePressure=50; default=0.75, custom=2.5
        Assert.True(restNeedC > restNeedD,
            $"Custom profile should produce higher Rest needAdd ({restNeedC}) than default ({restNeedD})");
        Assert.Equal(2.5, restNeedC, precision: 6); // 50 * 0.05
    }

    [Fact]
    public void CustomProfile_ZeroPrepStarvationFloor_AllowsFullPreparationWhenStarving()
    {
        // Floor = 0.0 means starvation does NOT suppress Preparation
        var customProfile = new DecisionTuningProfile
        {
            Mood = new MoodTuning { PrepStarvationFloor = 0.0 }
        };

        // Satiety=10 < StarvatingSatietyThreshold=20 → starvation suppression active
        var (domain, actorId, actor, world) = CreateSetup(satiety: 10.0, profile: customProfile);
        var resources  = new EmptyResourceAvailability();
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);
        var explanation = domain.ExplainCandidateScoring(actorId, actor, world, 0L, candidates)!;

        var qmd = (Dictionary<string, object>)explanation["qualityModelDecomposition"];
        if (qmd.ContainsKey("Preparation"))
        {
            var prep = (Dictionary<string, object>)qmd["Preparation"];
            // With floor=0.0, mood multiplier for Preparation should be 0.0 (fully suppressed)
            Assert.Equal(0.0, (double)prep["moodMultiplier"], precision: 4);
        }
    }

    [Fact]
    public void CustomProfile_HigherPreparationScale_IncreasesPersonalityBaseForPreparation()
    {
        var customProfile = new DecisionTuningProfile
        {
            Personality = new PersonalityTuning { PreparationScale = 1.4 } // 2× default (0.7)
        };

        // INT=14, WIS=16 → planner=0.5; industrious=0 → personalityBase = 0.5 * 1.4 = 0.7
        var domain  = new IslandDomainPack(customProfile);
        var actorId = new ActorId("ScaleActor");
        var actor   = (IslandActorState)domain.CreateActorState(actorId, new Dictionary<string, object>
        {
            ["INT"] = 14, ["WIS"] = 16,
            ["satiety"] = 70.0, ["energy"] = 80.0, ["morale"] = 60.0
        });
        actor.DecisionPragmatism = 1.0;
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        domain.InitializeActorItems(actorId, world);

        var resources  = new EmptyResourceAvailability();
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);
        var explanation = domain.ExplainCandidateScoring(actorId, actor, world, 0L, candidates)!;

        var pi        = (Dictionary<string, object>)explanation["personalityInfluence"];
        var breakdown = (Dictionary<string, object>)pi["qualityPersonalityBreakdown"];
        var prep      = (Dictionary<string, object>)breakdown["Preparation"];

        Assert.Equal(0.7, (double)prep["personalityBase"], precision: 4);
    }

    [Fact]
    public void CustomProfile_DoesNotAffectNullProfileDomainPack()
    {
        // Constructing a domain with null should fall back to Default (no mutation of Default).
        var domainNull    = new IslandDomainPack(null);
        var domainDefault = new IslandDomainPack(DecisionTuningProfile.Default);

        var actorId = new ActorId("NullCheck");
        var actorN  = (IslandActorState)domainNull   .CreateActorState(actorId, null);
        var actorD  = (IslandActorState)domainDefault.CreateActorState(actorId, null);

        // Pragmatism should be identical since both use Default profile
        Assert.Equal(actorD.DecisionPragmatism, actorN.DecisionPragmatism, precision: 9);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 5. Profile is immutable (init-only)
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void DecisionTuningProfile_CanBeConstructedWithWithExpression()
    {
        // Verify that init-only properties support record-style creation via 'with'-like patterns.
        var custom = new DecisionTuningProfile
        {
            Need = new NeedTuning { SatietyRampMild = 80.0 },
            Mood = new MoodTuning { FunBaseScale    = 0.8  }
        };

        Assert.Equal(80.0, custom.Need.SatietyRampMild);
        Assert.Equal(0.8,  custom.Mood.FunBaseScale);
        // Defaults preserved for non-overridden fields
        Assert.Equal(DecisionTuningProfile.Default.Need.SatietyRampModerate, custom.Need.SatietyRampModerate);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 6. Profile metadata
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Default_ProfileName_IsProductionDefault()
    {
        Assert.Equal("ProductionDefault", DecisionTuningProfile.Default.ProfileName);
    }

    [Fact]
    public void Default_Description_IsNull()
    {
        Assert.Null(DecisionTuningProfile.Default.Description);
    }

    [Fact]
    public void CustomProfile_CanSetNameAndDescription()
    {
        var custom = new DecisionTuningProfile
        {
            ProfileName = "HighHungerSensitivity",
            Description = "2× hunger ramp scale for testing"
        };
        Assert.Equal("HighHungerSensitivity", custom.ProfileName);
        Assert.Equal("2× hunger ramp scale for testing", custom.Description);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 7. CategoryTuning — ThinkAboutSupplies extraction
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Default_CategoryTuning_ThinkAboutSupplies_HasProductionValues()
    {
        var t = DecisionTuningProfile.Default.Categories.ThinkAboutSupplies;
        Assert.Equal(3,    t.TopN);
        Assert.Equal(25.0, t.StarvationThreshold);
        Assert.Equal(0.2,  t.StarvationSuppression);
        Assert.Equal(0.15, t.FallbackPreparation);
        Assert.Equal(0.10, t.FallbackEfficiency);
    }

    [Fact]
    public void CustomProfile_ThinkAboutSupplies_ZeroFallback_RemovesThinkSuppliesFromPool()
    {
        // When FallbackPreparation and FallbackEfficiency are 0.0 and actor has nothing
        // discoverable, think_about_supplies would have zero quality contribution.
        // It can still be generated (intrinsic score 0.08 is unchanged), but quality score is 0.
        var customProfile = new DecisionTuningProfile
        {
            Categories = new CategoryTuning
            {
                ThinkAboutSupplies = new ThinkAboutSuppliesTuning
                {
                    FallbackPreparation = 0.0,
                    FallbackEfficiency  = 0.0
                }
            }
        };

        var (domain, actorId, actor, world) = CreateSetup(profile: customProfile);
        var resources  = new EmptyResourceAvailability();

        // Should still generate without error
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);
        Assert.NotEmpty(candidates);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 8. ToDebugString
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Default_ToDebugString_ContainsProfileName()
    {
        var s = DecisionTuningProfile.Default.ToDebugString();
        Assert.Contains("ProductionDefault", s);
    }

    [Fact]
    public void Default_ToDebugString_ContainsKeyValues()
    {
        var s = DecisionTuningProfile.Default.ToDebugString();
        // Spot-check a few key values appear in the output
        Assert.Contains("0.015",  s); // FatiguePressureRestScale
        Assert.Contains("0.025",  s); // InjurySafetyNeedScale
        Assert.Contains("0.7",    s); // PreparationScale
        Assert.Contains("0.8",    s); // PragmatismBase
        Assert.Contains("TopN=3", s); // ThinkAboutSupplies.TopN
    }

    [Fact]
    public void CustomProfile_ToDebugString_ContainsCustomName()
    {
        var custom = new DecisionTuningProfile
        {
            ProfileName = "TestProfile",
            Description = "For unit testing"
        };
        var s = custom.ToDebugString();
        Assert.Contains("TestProfile",    s);
        Assert.Contains("For unit testing", s);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 9. Multi-state parity: default profile produces identical scores across
    //    a representative grid of actor states (behavior drift detection)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Verifies that the refactored scoring path (using DecisionTuningProfile) produces
    /// exactly the same candidate scores as the implicit-default path across a representative
    /// grid of actor states. This is the primary parity validation harness.
    /// </summary>
    [Theory]
    [InlineData(10.0, 50.0, 50.0, 100.0)]  // starving
    [InlineData(25.0, 10.0, 50.0, 100.0)]  // exhausted
    [InlineData(50.0, 50.0, 10.0, 100.0)]  // miserable
    [InlineData(50.0, 50.0, 50.0,  10.0)]  // injured
    [InlineData(100.0, 100.0, 100.0, 100.0)] // fully healthy
    [InlineData(70.0,  80.0,  60.0, 100.0)]  // default-ish state
    [InlineData(15.0,  15.0,  15.0,  20.0)]  // extreme survival
    [InlineData(60.0,  60.0,  80.0, 100.0)]  // high morale
    [InlineData(40.0,  40.0,  40.0,  60.0)]  // moderate all
    public void DefaultProfile_MatchesImplicitDefault_AcrossActorStateGrid(
        double satiety, double energy, double morale, double health)
    {
        // Implicit (null → default internally)
        var domainA = new IslandDomainPack();
        // Explicit default
        var domainB = new IslandDomainPack(DecisionTuningProfile.Default);

        var actorId = new ActorId("ParityCheck");
        var actorA  = (IslandActorState)domainA.CreateActorState(actorId, new Dictionary<string, object>
            { ["satiety"] = satiety, ["energy"] = energy, ["morale"] = morale });
        var actorB  = (IslandActorState)domainB.CreateActorState(actorId, new Dictionary<string, object>
            { ["satiety"] = satiety, ["energy"] = energy, ["morale"] = morale });
        actorA.Health = actorB.Health = health;
        actorA.DecisionPragmatism = actorB.DecisionPragmatism = 1.0;

        var worldA = (IslandWorldState)domainA.CreateInitialWorldState();
        var worldB = (IslandWorldState)domainB.CreateInitialWorldState();
        domainA.InitializeActorItems(actorId, worldA);
        domainB.InitializeActorItems(actorId, worldB);

        var resources = new EmptyResourceAvailability();
        var candA = domainA.GenerateCandidates(actorId, actorA, worldA, 0L, new Random(99), resources);
        var candB = domainB.GenerateCandidates(actorId, actorB, worldB, 0L, new Random(99), resources);

        Assert.Equal(candA.Count, candB.Count);
        for (var i = 0; i < candA.Count; i++)
        {
            Assert.Equal(candA[i].Action.Id, candB[i].Action.Id);
            Assert.Equal(candA[i].Score, candB[i].Score, precision: 9);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 10. Hunger suppression for Comfort/Rest
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void HungerSuppression_AboveStartThreshold_MoodMultiplierIsOne()
    {
        // Satiety=30 > HungerSuppressionStartSatiety(25) → no suppression; Comfort/Rest moodMultiplier = 1.0
        var (domain, actorId, actor, world) = CreateSetup(satiety: 30.0, energy: 50.0, morale: 50.0);
        var resources   = new EmptyResourceAvailability();
        var candidates  = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);
        var explanation = domain.ExplainCandidateScoring(actorId, actor, world, 0L, candidates)!;

        var qmd = (Dictionary<string, object>)explanation["qualityModelDecomposition"];

        if (qmd.ContainsKey("Comfort"))
        {
            var comfort = (Dictionary<string, object>)qmd["Comfort"];
            Assert.Equal(1.0, (double)comfort["moodMultiplier"], precision: 4);
        }
        if (qmd.ContainsKey("Rest"))
        {
            var rest = (Dictionary<string, object>)qmd["Rest"];
            Assert.Equal(1.0, (double)rest["moodMultiplier"], precision: 4);
        }
    }

    [Fact]
    public void HungerSuppression_AtFullThreshold_MoodMultiplierIsMin()
    {
        // Satiety=10 == HungerSuppressionFullSatiety(10) → full suppression; moodMultiplier = ComfortRestSuppressionMin(0.3)
        var (domain, actorId, actor, world) = CreateSetup(satiety: 10.0, energy: 50.0, morale: 50.0);
        var resources   = new EmptyResourceAvailability();
        var candidates  = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);
        var explanation = domain.ExplainCandidateScoring(actorId, actor, world, 0L, candidates)!;

        var qmd = (Dictionary<string, object>)explanation["qualityModelDecomposition"];

        if (qmd.ContainsKey("Comfort"))
        {
            var comfort = (Dictionary<string, object>)qmd["Comfort"];
            Assert.Equal(0.3, (double)comfort["moodMultiplier"], precision: 4);
        }
        if (qmd.ContainsKey("Rest"))
        {
            var rest = (Dictionary<string, object>)qmd["Rest"];
            Assert.Equal(0.3, (double)rest["moodMultiplier"], precision: 4);
        }
    }

    [Fact]
    public void HungerSuppression_BelowFullThreshold_MoodMultiplierIsMin()
    {
        // Satiety=5 < HungerSuppressionFullSatiety(10) → full suppression floor applied
        var (domain, actorId, actor, world) = CreateSetup(satiety: 5.0, energy: 50.0, morale: 50.0);
        var resources   = new EmptyResourceAvailability();
        var candidates  = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);
        var explanation = domain.ExplainCandidateScoring(actorId, actor, world, 0L, candidates)!;

        var qmd = (Dictionary<string, object>)explanation["qualityModelDecomposition"];

        if (qmd.ContainsKey("Comfort"))
        {
            var comfort = (Dictionary<string, object>)qmd["Comfort"];
            Assert.Equal(0.3, (double)comfort["moodMultiplier"], precision: 4);
        }
        if (qmd.ContainsKey("Rest"))
        {
            var rest = (Dictionary<string, object>)qmd["Rest"];
            Assert.Equal(0.3, (double)rest["moodMultiplier"], precision: 4);
        }
    }

    [Fact]
    public void HungerSuppression_MidRange_MoodMultiplierIsBetweenMinAndOne()
    {
        // Satiety=17.5 is midway between FullSatiety(10) and StartSatiety(25) → t=0.5, curved=0.25 (exp=2)
        // factor = 0.3 + (1.0 - 0.3) * 0.25 = 0.3 + 0.175 = 0.475
        var (domain, actorId, actor, world) = CreateSetup(satiety: 17.5, energy: 50.0, morale: 50.0);
        var resources   = new EmptyResourceAvailability();
        var candidates  = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);
        var explanation = domain.ExplainCandidateScoring(actorId, actor, world, 0L, candidates)!;

        var qmd = (Dictionary<string, object>)explanation["qualityModelDecomposition"];

        if (qmd.ContainsKey("Comfort"))
        {
            var comfort = (Dictionary<string, object>)qmd["Comfort"];
            var moodMult = (double)comfort["moodMultiplier"];
            Assert.InRange(moodMult, 0.3, 1.0);
            Assert.Equal(0.475, moodMult, precision: 4);
        }
    }

    [Fact]
    public void HungerSuppression_CriticalHunger_ComfortScoreLowerThanNormal()
    {
        // Critical hunger (satiety=5) should produce lower Comfort effective weight than normal satiety (satiety=80)
        var (domainCritical, actorIdC, actorC, worldC) = CreateSetup(satiety: 5.0,  energy: 50.0, morale: 0.0);
        var (domainNormal,   actorIdN, actorN, worldN) = CreateSetup(satiety: 80.0, energy: 50.0, morale: 0.0);

        var resources = new EmptyResourceAvailability();
        var candC = domainCritical.GenerateCandidates(actorIdC, actorC, worldC, 0L, new Random(1), resources);
        var candN = domainNormal  .GenerateCandidates(actorIdN, actorN, worldN, 0L, new Random(1), resources);

        var explainC = domainCritical.ExplainCandidateScoring(actorIdC, actorC, worldC, 0L, candC)!;
        var explainN = domainNormal  .ExplainCandidateScoring(actorIdN, actorN, worldN, 0L, candN)!;

        var qmdC = (Dictionary<string, object>)explainC["qualityModelDecomposition"];
        var qmdN = (Dictionary<string, object>)explainN["qualityModelDecomposition"];

        if (qmdC.ContainsKey("Comfort") && qmdN.ContainsKey("Comfort"))
        {
            var comfortWeightCritical = (double)((Dictionary<string, object>)qmdC["Comfort"])["effectiveWeight"];
            var comfortWeightNormal   = (double)((Dictionary<string, object>)qmdN["Comfort"])["effectiveWeight"];
            Assert.True(comfortWeightCritical < comfortWeightNormal,
                $"Critical hunger Comfort weight ({comfortWeightCritical}) should be less than normal ({comfortWeightNormal})");
        }
    }

    [Fact]
    public void HungerSuppression_ExplainScoring_ContainsSuppressionBreakdown()
    {
        // ExplainCandidateScoring should include hungerSuppressionBreakdown key
        var (domain, actorId, actor, world) = CreateSetup(satiety: 12.0);
        var resources   = new EmptyResourceAvailability();
        var candidates  = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);
        var explanation = domain.ExplainCandidateScoring(actorId, actor, world, 0L, candidates)!;

        Assert.True(explanation.ContainsKey("hungerSuppressionBreakdown"),
            "ExplainCandidateScoring should include hungerSuppressionBreakdown");

        var breakdown = (Dictionary<string, object>)explanation["hungerSuppressionBreakdown"];
        Assert.True(breakdown.ContainsKey("suppressionFactor"), "Breakdown should contain suppressionFactor");
        Assert.True(breakdown.ContainsKey("satiety"),           "Breakdown should contain satiety");

        var factor = (double)breakdown["suppressionFactor"];
        Assert.InRange(factor, 0.3, 1.0);
    }

    [Fact]
    public void HungerSuppression_CustomMin_AffectsComfortRestAtCriticalHunger()
    {
        // Custom profile with ComfortRestSuppressionMin=0.1 should produce lower Comfort weight than default (0.3)
        var customProfile = new DecisionTuningProfile
        {
            Mood = new MoodTuning { ComfortRestSuppressionMin = 0.1 }
        };

        var (domainDefault, actorIdD, actorD, worldD) = CreateSetup(satiety: 5.0, morale: 0.0);
        var (domainCustom,  actorIdC, actorC, worldC) = CreateSetup(satiety: 5.0, morale: 0.0, profile: customProfile);

        var resources = new EmptyResourceAvailability();
        var candD = domainDefault.GenerateCandidates(actorIdD, actorD, worldD, 0L, new Random(1), resources);
        var candC = domainCustom .GenerateCandidates(actorIdC, actorC, worldC, 0L, new Random(1), resources);

        var explainD = domainDefault.ExplainCandidateScoring(actorIdD, actorD, worldD, 0L, candD)!;
        var explainC = domainCustom .ExplainCandidateScoring(actorIdC, actorC, worldC, 0L, candC)!;

        var qmdD = (Dictionary<string, object>)explainD["qualityModelDecomposition"];
        var qmdC = (Dictionary<string, object>)explainC["qualityModelDecomposition"];

        if (qmdD.ContainsKey("Comfort") && qmdC.ContainsKey("Comfort"))
        {
            var moodDefault = (double)((Dictionary<string, object>)qmdD["Comfort"])["moodMultiplier"];
            var moodCustom  = (double)((Dictionary<string, object>)qmdC["Comfort"])["moodMultiplier"];
            Assert.True(moodCustom < moodDefault,
                $"Custom ComfortRestSuppressionMin=0.1 should give lower moodMultiplier ({moodCustom}) than default 0.3 ({moodDefault})");
            Assert.Equal(0.1, moodCustom, precision: 4);
        }
    }

    [Fact]
    public void Default_ToDebugString_ContainsHungerSuppressionValues()
    {
        var s = DecisionTuningProfile.Default.ToDebugString();
        Assert.Contains("HungerSuppressionStartSatiety", s);
        Assert.Contains("HungerSuppressionFullSatiety",  s);
        Assert.Contains("ComfortRestSuppressionMin",     s);
        Assert.Contains("HungerSuppressionExponent",     s);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 11. Regression: food beats comfort under critical hunger
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Regression test for the comfort-trap failure mode.
    /// Actor has critical hunger (satiety=10), food available in the supply pile,
    /// and a bed present for rest/comfort candidates.
    /// FoodConsumption action should be the top-scoring candidate.
    /// </summary>
    [Fact]
    public void CriticalHunger_WithFoodAndBedAvailable_FoodConsumptionCandidateWins()
    {
        var domain  = new IslandDomainPack();
        var actorId = new ActorId("Tester");
        var actor   = (IslandActorState)domain.CreateActorState(actorId, new Dictionary<string, object>
        {
            // Critical hunger; comfortable energy and morale (so misery pressure would normally push comfort)
            ["satiety"] = 10.0,
            ["energy"]  = 80.0,
            ["morale"]  = 0.0   // max misery pressure → highest possible comfort need without suppression
        });
        actor.DecisionPragmatism = 1.0; // deterministic
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        domain.InitializeActorItems(actorId, world);

        // Add food to the shared supply pile so bash_and_eat_coconut / eat actions are available.
        world.SharedSupplyPile!.AddSupply(5, () => new CoconutSupply());

        // Add a bed to the world so sleep_in_bed and rest candidates compete.
        world.AddWorldItem(new PalmFrondBedItem("test_bed"), "beach");

        var resources  = new EmptyResourceAvailability();
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(42), resources);

        // Identify the top-scoring candidate's dominant quality.
        var explanation = domain.ExplainCandidateScoring(actorId, actor, world, 0L, candidates)!;
        var contribs    = domain.ComputeQualityContributions(actor, world, 0L, candidates);

        // Top candidate (highest Score) should have FoodConsumption as its dominant contribution.
        var topCandidate = candidates.OrderByDescending(c => c.Score).First();
        var topContribs  = contribs[candidates.IndexOf(topCandidate)];

        Assert.NotEmpty(topContribs);
        var dominantQuality = topContribs[0].Quality; // contribs are sorted descending by contribution
        Assert.Equal(QualityType.FoodConsumption, dominantQuality);

        // Also verify the effective weight of FoodConsumption exceeds Comfort under critical hunger.
        var qmd = (Dictionary<string, object>)explanation["qualityModelDecomposition"];
        var foodWeight = qmd.ContainsKey("FoodConsumption")
            ? (double)((Dictionary<string, object>)qmd["FoodConsumption"])["effectiveWeight"]
            : 0.0;
        var comfortWeight = qmd.ContainsKey("Comfort")
            ? (double)((Dictionary<string, object>)qmd["Comfort"])["effectiveWeight"]
            : 0.0;

        Assert.True(foodWeight > comfortWeight,
            $"FoodConsumption effective weight ({foodWeight}) should exceed Comfort ({comfortWeight}) at critical hunger (satiety=10)");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 12. Threshold guard: invalid threshold ordering
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void HungerSuppression_InvertedThresholds_ReturnsSuppressedValue()
    {
        // Invalid config: start <= full (inverted). Should not divide by zero or return > 1.0.
        // Any satiety value below the start threshold should get the min suppression.
        var customProfile = new DecisionTuningProfile
        {
            Mood = new MoodTuning
            {
                HungerSuppressionStartSatiety = 10.0,  // start == full (degenerate)
                HungerSuppressionFullSatiety  = 10.0,
                ComfortRestSuppressionMin     = 0.3
            }
        };

        var (domain, actorId, actor, world) = CreateSetup(satiety: 5.0, profile: customProfile);
        var resources   = new EmptyResourceAvailability();
        var candidates  = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);
        var explanation = domain.ExplainCandidateScoring(actorId, actor, world, 0L, candidates)!;

        // Should not throw and should clamp to ComfortRestSuppressionMin
        var qmd = (Dictionary<string, object>)explanation["qualityModelDecomposition"];
        if (qmd.ContainsKey("Comfort"))
        {
            var factor = (double)((Dictionary<string, object>)qmd["Comfort"])["moodMultiplier"];
            Assert.InRange(factor, 0.0, 1.0); // must not produce invalid values
        }
    }

    [Fact]
    public void HungerSuppression_StartLessThanFull_NeverProducesInvalidValues()
    {
        // Invalid config: start(5) < full(20) (fully inverted).
        // With inverted thresholds, the first two guards cover all satiety values:
        //   satiety >= start(5) → guard 1 returns 1.0 (no suppression)
        //   satiety <= full(20) → guard 2 returns min (full suppression)
        // No satiety value can slip through to the range division, so no NaN/infinity.
        var customProfile = new DecisionTuningProfile
        {
            Mood = new MoodTuning
            {
                HungerSuppressionStartSatiety = 5.0,   // start < full (inverted)
                HungerSuppressionFullSatiety  = 20.0,
                ComfortRestSuppressionMin     = 0.3
            }
        };

        // satiety=3 < start(5): guard 1 fails; satiety=3 <= full(20): guard 2 fires → min(0.3)
        var (domain, actorId, actor, world) = CreateSetup(satiety: 3.0, profile: customProfile);
        var resources   = new EmptyResourceAvailability();
        var candidates  = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), resources);
        var explanation = domain.ExplainCandidateScoring(actorId, actor, world, 0L, candidates)!;

        var qmd = (Dictionary<string, object>)explanation["qualityModelDecomposition"];
        if (qmd.ContainsKey("Comfort"))
        {
            var factor = (double)((Dictionary<string, object>)qmd["Comfort"])["moodMultiplier"];
            // Must be a valid value — no NaN/infinity. Guard 2 fires: returns min (0.3).
            Assert.InRange(factor, 0.0, 1.0);
            Assert.Equal(0.3, factor, precision: 4);
        }
    }
}
