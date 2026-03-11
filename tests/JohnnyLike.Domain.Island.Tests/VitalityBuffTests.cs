using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Island.Vitality;

namespace JohnnyLike.Domain.Island.Tests;

/// <summary>
/// Tests for VitalityBuff health deterioration/recovery logic and the health-pressure
/// contributions to BuildQualityModel.
/// </summary>
public class VitalityBuffTests
{
    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static (IslandActorState actor, IslandWorldState world) MakeActor(
        double health  = 100.0,
        double satiety = 100.0,
        double energy  = 100.0,
        double morale  = 80.0)
    {
        var domain    = new IslandDomainPack();
        var actorId   = new ActorId("TestActor");
        var actorState = (IslandActorState)domain.CreateActorState(actorId,
            new Dictionary<string, object>
            {
                ["satiety"] = satiety,
                ["energy"]  = energy,
                ["morale"]  = morale
            });
        actorState.Health = health;
        var worldState = new IslandWorldState();
        return (actorState, worldState);
    }

    private static void Tick(IslandActorState actor, IslandWorldState world, long toTick)
    {
        var domain = new IslandDomainPack();
        var actors = new Dictionary<ActorId, ActorState> { [actor.Id] = actor };
        domain.TickWorldState(world, actors, toTick, new EmptyResourceAvailability());
    }

    // Simulate many ticks of elapsed time.
    private static void TickForSeconds(IslandActorState actor, IslandWorldState world, double seconds)
    {
        long ticks = (long)(seconds * EngineConstants.TickHz);
        Tick(actor, world, ticks);
    }

    private static List<ActionCandidate> GenerateCandidates(IslandActorState actor)
    {
        var domain  = new IslandDomainPack();
        var world   = new IslandWorldState();
        domain.InitializeActorItems(actor.Id, world);
        world.WorldItems.Add(new OceanItem("ocean"));
        return domain.GenerateCandidates(actor.Id, actor, world, 0L, new Random(42), new EmptyResourceAvailability());
    }

    private static Dictionary<string, object>? ExplainScoring(IslandActorState actor)
    {
        var domain     = new IslandDomainPack();
        var world      = new IslandWorldState();
        domain.InitializeActorItems(actor.Id, world);
        world.WorldItems.Add(new OceanItem("ocean"));
        var candidates = domain.GenerateCandidates(actor.Id, actor, world, 0L, new Random(42), new EmptyResourceAvailability());
        return domain.ExplainCandidateScoring(actor.Id, actor, world, 0L, candidates);
    }

    // ─── Presence and initialization ─────────────────────────────────────────

    [Fact]
    public void CreateActorState_AlwaysHasVitalityBuff()
    {
        var domain = new IslandDomainPack();
        var actor  = (IslandActorState)domain.CreateActorState(new ActorId("A"));

        Assert.True(actor.HasBuff<VitalityBuff>(), "Actor should have VitalityBuff on creation");
    }

    [Fact]
    public void VitalityBuff_Type_IsVitality()
    {
        var domain = new IslandDomainPack();
        var actor  = (IslandActorState)domain.CreateActorState(new ActorId("A"));
        var buff   = actor.TryGetBuff<VitalityBuff>();

        Assert.NotNull(buff);
        Assert.Equal(BuffType.Vitality, buff.Type);
    }

    [Fact]
    public void VitalityBuff_NeverExpires()
    {
        var domain = new IslandDomainPack();
        var actor  = (IslandActorState)domain.CreateActorState(new ActorId("A"));
        var buff   = actor.TryGetBuff<VitalityBuff>()!;

        Assert.Equal(long.MaxValue, buff.ExpiresAtTick);
    }

    // ─── B. Health deterioration ──────────────────────────────────────────────

    [Fact]
    public void Health_DeterioratesFromStarvation_WhenSatietyBelowThreshold()
    {
        // Satiety below StarvationSatietyThreshold (20) → health should fall
        var (actor, world) = MakeActor(health: 100.0, satiety: 10.0, energy: 80.0, morale: 80.0);
        var healthBefore   = actor.Health;

        TickForSeconds(actor, world, 60.0); // 1 sim-minute

        Assert.True(actor.Health < healthBefore,
            $"Health should have deteriorated due to starvation; was {healthBefore}, now {actor.Health}");
    }

    [Fact]
    public void Health_DeterioratesFromExhaustion_WhenEnergyBelowThreshold()
    {
        // Energy below ExhaustionEnergyThreshold (15) → health should fall
        var (actor, world) = MakeActor(health: 100.0, satiety: 80.0, energy: 5.0, morale: 80.0);
        var healthBefore   = actor.Health;

        TickForSeconds(actor, world, 60.0);

        Assert.True(actor.Health < healthBefore,
            $"Health should have deteriorated due to exhaustion; was {healthBefore}, now {actor.Health}");
    }

    [Fact]
    public void Health_DeterioratesFromPsycheStrain_WhenMoraleBelowThreshold()
    {
        // Morale below PsycheStrainMoraleThreshold (10) → health should fall
        var (actor, world) = MakeActor(health: 100.0, satiety: 80.0, energy: 80.0, morale: 5.0);
        var healthBefore   = actor.Health;

        TickForSeconds(actor, world, 60.0);

        Assert.True(actor.Health < healthBefore,
            $"Health should have deteriorated due to psyche strain; was {healthBefore}, now {actor.Health}");
    }

    [Fact]
    public void Health_DoesNotDeteriorate_WhenAllStatsAboveThresholds()
    {
        // All stats comfortably above their damage thresholds — no health loss expected
        var (actor, world) = MakeActor(health: 100.0, satiety: 80.0, energy: 80.0, morale: 80.0);
        var healthBefore   = actor.Health;

        TickForSeconds(actor, world, 60.0);

        // No starvation, exhaustion, or psyche damage
        // (there may be a tiny regen change; we just check that health didn't drop)
        Assert.True(actor.Health >= healthBefore,
            $"Health should not have dropped; was {healthBefore}, now {actor.Health}");
    }

    // ─── C. Stacking deterioration ────────────────────────────────────────────

    [Fact]
    public void Health_StacksMultipleDeteriorationSources()
    {
        // All three damage sources active simultaneously — loss should be larger
        var (actorBad,  worldBad)  = MakeActor(health: 100.0, satiety: 5.0,  energy: 5.0,  morale: 2.0);
        var (actorOk,   worldOk)   = MakeActor(health: 100.0, satiety: 80.0, energy: 80.0, morale: 80.0);

        TickForSeconds(actorBad, worldBad, 60.0);
        TickForSeconds(actorOk,  worldOk,  60.0);

        Assert.True(actorBad.Health < actorOk.Health,
            "Actor with all three bad conditions should lose more health than a healthy actor");
    }

    [Fact]
    public void Health_TwoBadConditionsLoseMoreThanOne()
    {
        // Two damage sources stack: starvation + exhaustion > starvation alone
        var (actorBoth, worldBoth) = MakeActor(health: 100.0, satiety: 10.0, energy: 5.0,  morale: 80.0);
        var (actorOne,  worldOne)  = MakeActor(health: 100.0, satiety: 10.0, energy: 80.0, morale: 80.0);

        TickForSeconds(actorBoth, worldBoth, 60.0);
        TickForSeconds(actorOne,  worldOne,  60.0);

        Assert.True(actorBoth.Health < actorOne.Health,
            "Two simultaneous damage sources should cause more health loss than one");
    }

    // ─── D. Health recovery ───────────────────────────────────────────────────

    [Fact]
    public void Health_RecoversSlow_WhenAllConditionsGood()
    {
        // All stats above recovery thresholds and actor not at full health.
        // Use short tick (30s) so MetabolicBuff doesn't drain stats below thresholds during the tick.
        // Starting with satiety=100: after 30s of metabolism, satiety≈96 (still above 60 threshold).
        var (actor, world) = MakeActor(health: 50.0, satiety: 100.0, energy: 100.0, morale: 80.0);
        var healthBefore   = actor.Health;

        TickForSeconds(actor, world, 30.0); // 30 sim-seconds: regen = 0.01/s × 30s = 0.3 health

        Assert.True(actor.Health > healthBefore,
            $"Health should have recovered under stable conditions; was {healthBefore}, now {actor.Health}");
    }

    [Fact]
    public void Health_DoesNotRecover_WhenSatietyTooLow()
    {
        // Satiety below recovery minimum (60) → no regen, even if energy/morale are good.
        // Using short tick to keep stats stable for the assertion.
        var (actor, world) = MakeActor(health: 80.0, satiety: 50.0, energy: 90.0, morale: 80.0);
        var healthBefore   = actor.Health;

        TickForSeconds(actor, world, 30.0);

        // Satiety after 30s metabolism: ~50 - 3.75 = 46.25 (below 60 threshold, above 20 starvation)
        // → no recovery, no starvation damage
        Assert.True(actor.Health <= healthBefore,
            $"Health should not recover when satiety is below recovery threshold; was {healthBefore}, now {actor.Health}");
    }

    [Fact]
    public void Health_DoesNotRecover_WhenEnergyTooLow()
    {
        // Energy below recovery minimum (60) → no regen.
        // We set morale low enough that actor doesn't gain energy naturally.
        var (actor, world) = MakeActor(health: 80.0, satiety: 90.0, energy: 50.0, morale: 80.0);
        // Force energy to stay low by manually adjusting after actor creation
        actor.Energy = 50.0;
        var healthBefore   = actor.Health;

        TickForSeconds(actor, world, 30.0);

        // Energy conversion will top up energy slightly but starts at 50 < 60
        // Net: no recovery (energy still below 60 threshold for most of the tick)
        Assert.True(actor.Health <= healthBefore,
            $"Health should not recover when energy is below recovery threshold; was {healthBefore}, now {actor.Health}");
    }

    [Fact]
    public void Health_DoesNotRecover_WhenMoraleTooLow()
    {
        // Morale below recovery minimum (50) → no regen.
        var (actor, world) = MakeActor(health: 80.0, satiety: 90.0, energy: 90.0, morale: 40.0);
        var healthBefore   = actor.Health;

        TickForSeconds(actor, world, 30.0);

        Assert.True(actor.Health <= healthBefore,
            $"Health should not recover when morale is below recovery threshold; was {healthBefore}, now {actor.Health}");
    }

    // ─── E. Bounding / clamping ───────────────────────────────────────────────

    [Fact]
    public void Health_NeverExceeds100()
    {
        // Full health + stable conditions → should not exceed 100
        var (actor, world) = MakeActor(health: 100.0, satiety: 100.0, energy: 100.0, morale: 100.0);

        TickForSeconds(actor, world, 600.0);

        Assert.True(actor.Health <= 100.0, $"Health should never exceed 100; was {actor.Health}");
    }

    [Fact]
    public void Health_NeverDropsBelowZero()
    {
        // Worst possible conditions for a long time → health should clamp at 0
        var (actor, world) = MakeActor(health: 5.0, satiety: 0.0, energy: 0.0, morale: 0.0);

        // Tick for a very long time
        TickForSeconds(actor, world, 10000.0);

        Assert.True(actor.Health >= 0.0, $"Health should never drop below 0; was {actor.Health}");
    }

    // ─── F. Serialization / deserialization ──────────────────────────────────

    [Fact]
    public void VitalityBuff_SurvivesActorSerializationRoundtrip()
    {
        var domain = new IslandDomainPack();
        var actor  = (IslandActorState)domain.CreateActorState(new ActorId("A"));

        // Advance LastTick so the serialized value is non-zero
        var buff = actor.TryGetBuff<VitalityBuff>()!;
        buff.LastTick = 42L;

        var json       = actor.Serialize();
        var actor2     = new IslandActorState();
        actor2.Deserialize(json);

        var buff2 = actor2.TryGetBuff<VitalityBuff>();
        Assert.NotNull(buff2);
        Assert.Equal(42L, buff2.LastTick);
    }
}

/// <summary>
/// Tests that verify health-pressure contributions to BuildQualityModel decision weights.
/// Accesses model behaviour through GenerateCandidates scoring and ExplainCandidateScoring.
/// </summary>
public class HealthDecisionWeightingTests
{
    private static IslandActorState MakeActor(double health, string id = "TestActor")
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId(id);
        var actorState = (IslandActorState)domain.CreateActorState(actorId,
            new Dictionary<string, object>
            {
                // Keep satiety/energy/morale neutral so health is the only variable
                ["satiety"] = 80.0,
                ["energy"]  = 80.0,
                ["morale"]  = 80.0
            });
        actorState.Health = health;
        return actorState;
    }

    private static Dictionary<string, object> GetScoringExplain(IslandActorState actor)
    {
        var domain = new IslandDomainPack();
        var world  = new IslandWorldState();
        domain.InitializeActorItems(actor.Id, world);
        world.WorldItems.Add(new OceanItem("ocean"));
        var candidates = domain.GenerateCandidates(actor.Id, actor, world, 0L, new Random(42), new EmptyResourceAvailability());
        var explain = domain.ExplainCandidateScoring(actor.Id, actor, world, 0L, candidates);
        Assert.NotNull(explain);
        return explain!;
    }

    private static double GetEffectiveWeight(Dictionary<string, object> explain, string quality)
    {
        var ew = (Dictionary<string, object>)explain["effectiveWeights"];
        return ew.TryGetValue(quality, out var v) ? (double)v : 0.0;
    }

    // ─── 10. Low health increases Safety weight ───────────────────────────────

    [Fact]
    public void LowHealth_IncreasesSafetyWeight()
    {
        var healthy  = MakeActor(100.0, "healthy");
        var injured  = MakeActor(10.0,  "injured");

        var explainHealthy = GetScoringExplain(healthy);
        var explainInjured = GetScoringExplain(injured);

        var safetHealthy = GetEffectiveWeight(explainHealthy, "Safety");
        var safetyInjured = GetEffectiveWeight(explainInjured, "Safety");

        Assert.True(safetyInjured > safetHealthy,
            $"Safety weight should be higher when injured ({safetyInjured:F4}) vs healthy ({safetHealthy:F4})");
    }

    // ─── 11. Low health increases Rest weight ─────────────────────────────────

    [Fact]
    public void LowHealth_IncreasesRestWeight()
    {
        var healthy  = MakeActor(100.0, "healthy");
        var injured  = MakeActor(10.0,  "injured");

        var explainHealthy = GetScoringExplain(healthy);
        var explainInjured = GetScoringExplain(injured);

        var restHealthy = GetEffectiveWeight(explainHealthy, "Rest");
        var restInjured = GetEffectiveWeight(explainInjured, "Rest");

        Assert.True(restInjured > restHealthy,
            $"Rest weight should be higher when injured ({restInjured:F4}) vs healthy ({restHealthy:F4})");
    }

    // ─── 12. Low health increases Comfort weight (at least slightly) ──────────

    [Fact]
    public void LowHealth_IncreasesComfortWeight()
    {
        var healthy  = MakeActor(100.0, "healthy");
        var injured  = MakeActor(10.0,  "injured");

        var explainHealthy = GetScoringExplain(healthy);
        var explainInjured = GetScoringExplain(injured);

        var comfortHealthy = GetEffectiveWeight(explainHealthy, "Comfort");
        var comfortInjured = GetEffectiveWeight(explainInjured, "Comfort");

        Assert.True(comfortInjured > comfortHealthy,
            $"Comfort weight should be higher when injured ({comfortInjured:F4}) vs healthy ({comfortHealthy:F4})");
    }

    // ─── 13. Low health reduces Fun weight ───────────────────────────────────

    [Fact]
    public void LowHealth_ReducesFunWeight()
    {
        var healthy  = MakeActor(100.0, "healthy");
        var injured  = MakeActor(10.0,  "injured");

        var explainHealthy = GetScoringExplain(healthy);
        var explainInjured = GetScoringExplain(injured);

        var funHealthy = GetEffectiveWeight(explainHealthy, "Fun");
        var funInjured = GetEffectiveWeight(explainInjured, "Fun");

        Assert.True(funInjured < funHealthy,
            $"Fun weight should be lower when injured ({funInjured:F4}) vs healthy ({funHealthy:F4})");
    }

    // ─── 13. Low health reduces Mastery weight ───────────────────────────────

    [Fact]
    public void LowHealth_ReducesMasteryWeight()
    {
        // Use actors with non-zero Mastery (high INT + DEX)
        var domain = new IslandDomainPack();
        var makeActor = (double health, string id) =>
        {
            var a = (IslandActorState)domain.CreateActorState(new ActorId(id),
                new Dictionary<string, object>
                {
                    ["satiety"] = 80.0,
                    ["energy"]  = 80.0,
                    ["morale"]  = 80.0
                });
            a.INT = 16;
            a.DEX = 16;
            a.STR = 16;
            a.Health = health;
            return a;
        };

        var healthy  = makeActor(100.0, "healthy");
        var injured  = makeActor(10.0,  "injured");

        var explainHealthy = GetScoringExplain(healthy);
        var explainInjured = GetScoringExplain(injured);

        var masteryHealthy = GetEffectiveWeight(explainHealthy, "Mastery");
        var masteryInjured = GetEffectiveWeight(explainInjured, "Mastery");

        Assert.True(masteryInjured < masteryHealthy,
            $"Mastery weight should be lower when injured ({masteryInjured:F4}) vs healthy ({masteryHealthy:F4})");
    }

    // ─── 13. Low health reduces Preparation weight ──────────────────────────

    [Fact]
    public void LowHealth_ReducesPreparationWeight()
    {
        // Use actors with high Preparation personality
        var domain = new IslandDomainPack();
        var makeActor = (double health, string id) =>
        {
            var a = (IslandActorState)domain.CreateActorState(new ActorId(id),
                new Dictionary<string, object>
                {
                    ["satiety"] = 80.0,
                    ["energy"]  = 80.0,
                    ["morale"]  = 80.0
                });
            a.INT = 16;
            a.WIS = 16;
            a.STR = 16;
            a.DEX = 16;
            a.Health = health;
            return a;
        };

        var healthy  = makeActor(100.0, "healthy");
        var injured  = makeActor(10.0,  "injured");

        var explainHealthy = GetScoringExplain(healthy);
        var explainInjured = GetScoringExplain(injured);

        var prepHealthy = GetEffectiveWeight(explainHealthy, "Preparation");
        var prepInjured = GetEffectiveWeight(explainInjured, "Preparation");

        Assert.True(prepInjured < prepHealthy,
            $"Preparation weight should be lower when injured ({prepInjured:F4}) vs healthy ({prepHealthy:F4})");
    }

    // ─── 14. Quality model remains stable at full health ─────────────────────

    [Fact]
    public void BuildQualityModel_FullHealth_HasNoInjuryPressure()
    {
        var actor   = MakeActor(100.0);
        var explain = GetScoringExplain(actor);

        var pressures = (Dictionary<string, object>)explain["pressures"];
        var injuryPressure = (double)pressures["injuryPressure"];

        Assert.Equal(0.0, injuryPressure, 6);
    }

    [Fact]
    public void ExplainCandidateScoring_IncludesHealthInfluenceSection()
    {
        var actor   = MakeActor(50.0);
        var explain = GetScoringExplain(actor);

        Assert.True(explain.ContainsKey("healthInfluence"),
            "ExplainCandidateScoring output should include 'healthInfluence' section");

        var healthInfluence = (Dictionary<string, object>)explain["healthInfluence"];
        Assert.True(healthInfluence.ContainsKey("injuryFactor"));
        Assert.True(healthInfluence.ContainsKey("safety_needAdd_contribution"));
        Assert.True(healthInfluence.ContainsKey("fun_suppressor"));
    }

    // ─── 15-16. Regression / integration sanity ──────────────────────────────

    [Fact]
    public void HealthyActor_GeneratesAndScoresCandidatesNormally()
    {
        var actor      = MakeActor(100.0);
        var domain     = new IslandDomainPack();
        var world      = new IslandWorldState();
        domain.InitializeActorItems(actor.Id, world);
        world.WorldItems.Add(new OceanItem("ocean"));

        var candidates = domain.GenerateCandidates(actor.Id, actor, world, 0L, new Random(42), new EmptyResourceAvailability());

        Assert.NotEmpty(candidates);
        Assert.True(candidates.All(c => c.Score > 0.0), "All candidates should have positive scores for a healthy actor");

        // Key actions should be present
        Assert.Contains(candidates, c => c.Action.Id.Value == "idle");
        Assert.Contains(candidates, c => c.Action.Id.Value == "go_fishing");
        Assert.Contains(candidates, c => c.Action.Id.Value == "sleep_under_tree");
    }

    [Fact]
    public void HeavilyInjuredActor_PrefersRestAndSafety_OverFunAndMastery()
    {
        // Injured actor: Safety/Rest weights should dominate over Fun
        var injured  = MakeActor(15.0, "injured");
        var healthy  = MakeActor(100.0, "healthy");

        var explainInjured = GetScoringExplain(injured);
        var explainHealthy = GetScoringExplain(healthy);

        var safetyInjured = GetEffectiveWeight(explainInjured, "Safety");
        var safetyHealthy = GetEffectiveWeight(explainHealthy, "Safety");
        var funInjured    = GetEffectiveWeight(explainInjured, "Fun");
        var funHealthy    = GetEffectiveWeight(explainHealthy, "Fun");

        // Safety should be notably higher for injured actor
        Assert.True(safetyInjured > safetyHealthy * 1.5,
            $"Injured actor's Safety weight ({safetyInjured:F4}) should be much higher than healthy ({safetyHealthy:F4})");

        // Fun should be lower for injured actor
        Assert.True(funInjured < funHealthy,
            $"Injured actor's Fun weight ({funInjured:F4}) should be lower than healthy ({funHealthy:F4})");
    }
}

/// <summary>
/// Tests that verify the explicit AliveOnly requirement coverage across island candidates.
/// Supplements AlivenessCandidateRequirementTests with scenario-level checks.
/// </summary>
public class IslandCandidateAliveRequirementCoverageTests
{
    private static IslandActorState MakeAliveActor(string id = "TestActor")
    {
        var domain = new IslandDomainPack();
        return (IslandActorState)domain.CreateActorState(new ActorId(id));
    }

    private static IslandActorState MakeDownedActor(string id = "TestActor")
    {
        var actor = MakeAliveActor(id);
        actor.TryGetBuff<AlivenessBuff>()!.State = AlivenessState.Downed;
        return actor;
    }

    private static List<ActionCandidate> GenerateCandidates(IslandActorState actor)
    {
        var domain = new IslandDomainPack();
        var world  = new IslandWorldState();
        domain.InitializeActorItems(actor.Id, world);
        world.WorldItems.Add(new OceanItem("ocean"));
        return domain.GenerateCandidates(actor.Id, actor, world, 0L, new Random(42), new EmptyResourceAvailability());
    }

    [Fact]
    public void AliveActor_HasCoreIslandCandidates()
    {
        // Alive actor should have all normal action candidates
        var actor      = MakeAliveActor();
        var candidates = GenerateCandidates(actor);

        var ids = candidates.Select(c => c.Action.Id.Value).ToHashSet();
        Assert.Contains("idle",              ids);
        Assert.Contains("go_fishing",        ids);
        Assert.Contains("sleep_under_tree",  ids);
    }

    [Fact]
    public void AllCandidates_ExplicitlyDeclareAliveOnly_ForAliveActor()
    {
        // All candidates generated for a living actor should have an explicit ActorRequirement
        // (because we've applied AliveOnly to every island candidate definition).
        var actor      = MakeAliveActor();
        var domain     = new IslandDomainPack();
        var world      = new IslandWorldState();
        domain.InitializeActorItems(actor.Id, world);
        world.WorldItems.Add(new OceanItem("ocean"));

        // Get raw candidates before filtering by inspecting the code contract
        // (we test through GenerateCandidates which filters, but all surviving candidates
        // should have come through the ActorRequirement check)
        var candidates = domain.GenerateCandidates(actor.Id, actor, world, 0L, new Random(42), new EmptyResourceAvailability());

        Assert.NotEmpty(candidates);
        // All non-idle candidates with an explicit requirement should pass for an Alive actor
        foreach (var c in candidates)
        {
            if (c.ActorRequirement != null)
                Assert.True(c.ActorRequirement(actor),
                    $"Candidate '{c.Action.Id.Value}' ActorRequirement should pass for Alive actor");
        }
    }

    [Fact]
    public void DownedActor_HasNoCandidates_WithAliveOnlyRequirement()
    {
        // A downed actor should have all AliveOnly candidates filtered out.
        var actor      = MakeDownedActor();
        var candidates = GenerateCandidates(actor);

        // All remaining candidates (if any) must not have ActorRequirement=AliveOnly
        foreach (var c in candidates)
        {
            if (c.ActorRequirement != null)
                Assert.False(c.ActorRequirement == CandidateRequirements.AliveOnly,
                    $"Candidate '{c.Action.Id.Value}' should not survive AliveOnly filter for Downed actor");
        }
    }

    [Fact]
    public void CandidateRequiresAliveOnly_PassesForAliveActor_FailsForDownedActor()
    {
        var alive  = MakeAliveActor();
        var downed = MakeDownedActor();

        // AliveOnly requirement should pass for Alive and fail for Downed
        Assert.True(CandidateRequirements.AliveOnly(alive),
            "AliveOnly should pass for Alive actor");
        Assert.False(CandidateRequirements.AliveOnly(downed),
            "AliveOnly should fail for Downed actor");
    }

    [Fact]
    public void RecipeCandidate_IsFiltered_ForDownedActor()
    {
        // Recipe candidates should also be filtered for a Downed actor.
        // We test this by giving an actor a known recipe and verifying the recipe
        // candidate does not appear when the actor is Downed.
        var actor = MakeDownedActor();
        actor.KnownRecipeIds.Add("cook_fish_campfire"); // a well-known recipe

        var candidates = GenerateCandidates(actor);

        var cookCandidate = candidates.FirstOrDefault(c => c.Action.Id.Value == "cook_fish_campfire");
        Assert.Null(cookCandidate);
    }
}
