using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Metabolism;
using JohnnyLike.Domain.Island.Supply;
using JohnnyLike.Domain.Island.Vitality;
using JohnnyLike.Engine;

namespace JohnnyLike.Domain.Island.Tests;

/// <summary>
/// Tests covering the CrabActor acceptance criteria:
/// - CrabActor exists and is based on LivingActorState
/// - Crab does not receive human-only actions
/// - IsScavenger qualifier exists and crab satisfies it
/// - SupplyPile/CarcassScrapsSupply provides scavenger-only scavenging action
/// - Scavenging consumes CarcassScraps and feeds crab
/// - Crab provides human-only catch action
/// - Catching yields CrabSupply and removes crab from active set
/// - Crab spawning from CarcassScraps supply
/// - Crab idle/rest action
/// - Crab physiology differences (no morale deterioration, health from satiety, slow energy drain)
/// </summary>
public class CrabActorTests
{
    private static readonly EmptyResourceAvailability _noReservations = new();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (IslandDomainPack domain, IslandWorldState world, ActorId actorId, HumanActorState human)
        SetupHuman(double satiety = 80.0, double energy = 80.0, double morale = 50.0)
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("human1");
        var humanState = (HumanActorState)domain.CreateActorState(actorId, new Dictionary<string, object>
        {
            ["satiety"] = satiety,
            ["energy"]  = energy,
            ["morale"]  = morale
        });
        return (domain, world, actorId, humanState);
    }

    private static IslandContext MakeCtx(
        IslandDomainPack domain,
        LivingActorState actor,
        IslandWorldState world,
        ActorId? id = null)
    {
        var actorId = id ?? actor.Id;
        return new IslandContext(
            actorId,
            actor,
            world,
            0L,
            new RandomRngStream(new Random(1)),
            new Random(1),
            _noReservations
        );
    }

    // ── 1. CrabActor type and LivingActorState inheritance ────────────────────

    [Fact]
    public void CrabActorState_IsLivingActorState()
    {
        var crab = IslandDomainPack.CreateCrabActorState(new ActorId("crab1"));
        Assert.IsAssignableFrom<LivingActorState>(crab);
    }

    [Fact]
    public void CreateCrabActorState_HasExpectedDefaultStats()
    {
        var crab = IslandDomainPack.CreateCrabActorState(new ActorId("crab1"));

        Assert.Equal(8,     crab.STR);
        Assert.Equal(12,    crab.DEX);
        Assert.Equal(2,     crab.INT);
        Assert.True(crab.Satiety > 0.0);
        Assert.True(crab.Energy  > 0.0);
        Assert.True(crab.Health  > 0.0);
    }

    [Fact]
    public void CreateCrabActorState_HasCrabPhysiologyBuff()
    {
        var crab = IslandDomainPack.CreateCrabActorState(new ActorId("crab1"));
        Assert.Contains(crab.ActiveBuffs, b => b is CrabPhysiologyBuff);
        Assert.DoesNotContain(crab.ActiveBuffs, b => b is MetabolicBuff);  // no MetabolicBuff
        Assert.DoesNotContain(crab.ActiveBuffs, b => b is VitalityBuff);   // no VitalityBuff
    }

    [Fact]
    public void CreateActorState_WithCrabKind_ReturnsCrabActorState()
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("crab1");
        var state = domain.CreateActorState(actorId, new Dictionary<string, object> { ["actorKind"] = "crab" });

        Assert.IsType<CrabActorState>(state);
    }

    // ── 2. IsScavenger qualifier ──────────────────────────────────────────────

    [Fact]
    public void IsScavenger_TrueForCrab()
    {
        var crab = IslandDomainPack.CreateCrabActorState(new ActorId("crab1"));
        Assert.True(CandidateRequirements.IsScavenger(crab));
    }

    [Fact]
    public void IsScavenger_FalseForHuman()
    {
        var domain = new IslandDomainPack();
        var human = (HumanActorState)domain.CreateActorState(new ActorId("human1"));
        Assert.False(CandidateRequirements.IsScavenger(human));
    }

    // ── 3. Crab does not receive human-only actions ───────────────────────────

    [Fact]
    public void GenerateCandidates_ForCrab_DoesNotIncludeHumanOnlyActions()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var crabId = new ActorId("crab1");
        var crab = IslandDomainPack.CreateCrabActorState(crabId);
        crab.Status = ActorStatus.Ready;

        var candidates = domain.GenerateCandidates(crabId, crab, world, 0L, new Random(1), _noReservations);
        var actionIds = candidates.Select(c => c.Action.Id.Value).ToHashSet();

        // Human-only actions must not appear for crabs.
        Assert.DoesNotContain("think_about_supplies", actionIds);
        Assert.DoesNotContain("idle", actionIds);         // human idle
        Assert.DoesNotContain("lie_down_rest", actionIds);
        Assert.DoesNotContain("fish_with_pole", actionIds);
    }

    [Fact]
    public void GenerateCandidates_ForCrab_IncludesCrabIdle()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var crabId = new ActorId("crab1");
        var crab = IslandDomainPack.CreateCrabActorState(crabId);
        crab.Status = ActorStatus.Ready;

        var candidates = domain.GenerateCandidates(crabId, crab, world, 0L, new Random(1), _noReservations);
        var actionIds = candidates.Select(c => c.Action.Id.Value).ToHashSet();

        Assert.Contains("crab_idle", actionIds);
    }

    // ── 4. Scavenge CarcassScraps action for scavengers ──────────────────────

    [Fact]
    public void CarcassScrapsSupply_ProvidesScavengeAction_ForScavengers()
    {
        var scraps = new CarcassScrapsSupply(10.0);
        var crab = IslandDomainPack.CreateCrabActorState(new ActorId("crab1"));
        var pile = new SupplyPile("shared");
        pile.Supplies.Add(scraps);

        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var ctx = MakeCtx(domain, crab, world);

        var output = new List<ActionCandidate>();
        scraps.AddCandidates(ctx, pile, output);

        Assert.Contains(output, c => c.Action.Id.Value == "scavenge_carcass_scraps");
    }

    [Fact]
    public void CarcassScrapsSupply_DoesNotProvideScavengeAction_ForHumans()
    {
        var scraps = new CarcassScrapsSupply(10.0);
        var (domain, world, actorId, human) = SetupHuman();
        var pile = new SupplyPile("shared");
        pile.Supplies.Add(scraps);
        var ctx = MakeCtx(domain, human, world, actorId);

        var output = new List<ActionCandidate>();
        scraps.AddCandidates(ctx, pile, output);

        // Filter by actor requirement (simulating what GenerateCandidates does).
        var filtered = output.Where(c => c.ActorRequirement == null || c.ActorRequirement(human)).ToList();
        Assert.DoesNotContain(filtered, c => c.Action.Id.Value == "scavenge_carcass_scraps");
    }

    [Fact]
    public void CarcassScrapsSupply_DoesNotProvideScavengeAction_WhenQuantityTooLow()
    {
        var scraps = new CarcassScrapsSupply(0.5); // below threshold
        var crab = IslandDomainPack.CreateCrabActorState(new ActorId("crab1"));
        var pile = new SupplyPile("shared");
        pile.Supplies.Add(scraps);

        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var ctx = MakeCtx(domain, crab, world);

        var output = new List<ActionCandidate>();
        scraps.AddCandidates(ctx, pile, output);

        Assert.DoesNotContain(output, c => c.Action.Id.Value == "scavenge_carcass_scraps");
    }

    // ── 5. Scavenging consumes scraps and increases satiety ───────────────────

    [Fact]
    public void ScavengeCarcassScraps_ConsumesScrapsAndFeedsCrab()
    {
        var scraps = new CarcassScrapsSupply(5.0);
        var pile = new SupplyPile("shared");
        pile.Supplies.Add(scraps);

        var crab = IslandDomainPack.CreateCrabActorState(new ActorId("crab1"));
        crab.Satiety = 20.0;
        var initialSatiety = crab.Satiety;
        var initialScraps = scraps.Quantity;

        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var ctx = MakeCtx(domain, crab, world);

        var output = new List<ActionCandidate>();
        scraps.AddCandidates(ctx, pile, output);

        var scavengeCandidate = output.First(c => c.Action.Id.Value == "scavenge_carcass_scraps");

        // Execute PreAction (consumes scraps).
        var preActionResult = domain.TryExecutePreAction(
            crab.Id, crab, world,
            new RandomRngStream(new Random(1)),
            _noReservations,
            scavengeCandidate.PreAction);

        Assert.True(preActionResult);
        Assert.True(scraps.Quantity < initialScraps, "Scraps should be consumed by the scavenge PreAction");

        // Execute EffectHandler (feeds crab).
        var outcome = new ActionOutcome(scavengeCandidate.Action.Id, ActionOutcomeType.Success,
            Duration.Seconds(1.0));
        domain.ApplyActionEffects(crab.Id, outcome, crab, world,
            new RandomRngStream(new Random(1)), _noReservations,
            scavengeCandidate.EffectHandler);

        Assert.True(crab.Satiety > initialSatiety, "Crab satiety should increase after scavenging");
    }

    // ── 6. Crab provides catch_crab affordance to humans ─────────────────────

    [Fact]
    public void CrabActorState_ProvidesHumanOnlyCatchCrabAction()
    {
        var (domain, world, actorId, human) = SetupHuman();
        var crab = IslandDomainPack.CreateCrabActorState(new ActorId("crab1"));
        world.ActiveCrabActors.Add(crab);

        var candidates = domain.GenerateCandidates(actorId, human, world, 0L, new Random(1), _noReservations);
        var catchCrab = candidates.FirstOrDefault(c => c.Action.Id.Value == "catch_crab");

        Assert.NotNull(catchCrab);
    }

    [Fact]
    public void CrabActorState_CatchCrabAction_NotOfferedToCrabs()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();

        var crab1 = IslandDomainPack.CreateCrabActorState(new ActorId("crab1"));
        var crab2 = IslandDomainPack.CreateCrabActorState(new ActorId("crab2"));
        world.ActiveCrabActors.Add(crab1);

        var candidates = domain.GenerateCandidates(crab2.Id, crab2, world, 0L, new Random(1), _noReservations);
        Assert.DoesNotContain(candidates, c => c.Action.Id.Value == "catch_crab");
    }

    // ── 7. Catching crab yields CrabSupply and removes crab ──────────────────

    [Fact]
    public void CatchCrab_YieldsCrabSupplyAndRemovesCrabFromActiveList()
    {
        var (domain, world, actorId, human) = SetupHuman();
        var crab = IslandDomainPack.CreateCrabActorState(new ActorId("crab1"));
        world.ActiveCrabActors.Add(crab);

        // Use the shared supply pile that CreateInitialWorldState already creates.
        var pile = world.SharedSupplyPile;
        Assert.NotNull(pile);

        var candidates = domain.GenerateCandidates(actorId, human, world, 0L, new Random(1), _noReservations);
        var catchCrab = candidates.First(c => c.Action.Id.Value == "catch_crab");

        var outcome = new ActionOutcome(catchCrab.Action.Id, ActionOutcomeType.Success,
            Duration.Seconds(1.0));
        domain.ApplyActionEffects(actorId, outcome, human, world,
            new RandomRngStream(new Random(1)), _noReservations,
            catchCrab.EffectHandler);

        // Crab should be removed from the active list.
        Assert.Empty(world.ActiveCrabActors);

        // CrabSupply should be added to the shared pile.
        var crabSupply = pile.GetSupply<CrabSupply>();
        Assert.NotNull(crabSupply);
        Assert.True(crabSupply.Quantity > 0.0, "Catching a crab should add CrabSupply to the shared pile");
    }

    // ── 8. Crab spawning from CarcassScraps ──────────────────────────────────

    [Fact]
    public void TrySpawnCrab_SpawnsCrabWhenThresholdMet_OverManyTicks()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var pile = world.SharedSupplyPile ?? world.WorldItems.OfType<SupplyPile>().FirstOrDefault();
        Assert.NotNull(pile);

        // Add enough CarcassScraps to trigger spawning.
        pile.Supplies.Add(new CarcassScrapsSupply(10.0));

        var actors = new Dictionary<ActorId, ActorState>();

        // Run many ticks: at a probability of ~1/7200 per tick, over 100 000 ticks
        // we expect several spawns on average.
        for (long tick = 1; tick <= 100_000; tick++)
        {
            domain.TickWorldState(world, actors, tick, _noReservations);
            if (world.ActiveCrabActors.Count > 0)
                break; // a crab has spawned
        }

        Assert.True(world.ActiveCrabActors.Count > 0,
            "At least one crab should spawn over 100 000 ticks with sufficient CarcassScraps");
        Assert.IsType<CrabActorState>(world.ActiveCrabActors[0]);
    }

    [Fact]
    public void TrySpawnCrab_DoesNotSpawnWhenScrapsBelowThreshold()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var pile = world.SharedSupplyPile ?? world.WorldItems.OfType<SupplyPile>().FirstOrDefault();
        Assert.NotNull(pile);

        // Add scraps below threshold.
        pile.Supplies.Add(new CarcassScrapsSupply(2.0));

        var actors = new Dictionary<ActorId, ActorState>();

        for (long tick = 1; tick <= 10_000; tick++)
            domain.TickWorldState(world, actors, tick, _noReservations);

        Assert.Empty(world.ActiveCrabActors);
    }

    [Fact]
    public void TrySpawnCrab_DoesNotExceedMaxCrabs()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var pile = world.SharedSupplyPile ?? world.WorldItems.OfType<SupplyPile>().FirstOrDefault();
        Assert.NotNull(pile);

        pile.Supplies.Add(new CarcassScrapsSupply(100.0));

        // Pre-populate with max crabs.
        for (var i = 0; i < IslandDomainPack.MaxActiveCrabs; i++)
        {
            var c = IslandDomainPack.CreateCrabActorState(new ActorId($"crab_pre{i}"));
            world.ActiveCrabActors.Add(c);
        }

        var actors = new Dictionary<ActorId, ActorState>();
        for (long tick = 1; tick <= 100_000; tick++)
            domain.TickWorldState(world, actors, tick, _noReservations);

        Assert.Equal(IslandDomainPack.MaxActiveCrabs, world.ActiveCrabActors.Count);
    }

    // ── 9. Crab idle/rest behavior ────────────────────────────────────────────

    [Fact]
    public void CrabIdle_RecoversEnergy()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var crabId = new ActorId("crab1");
        var crab = IslandDomainPack.CreateCrabActorState(crabId);
        crab.Energy = 30.0;

        var ctx = MakeCtx(domain, crab, world);
        var output = new List<ActionCandidate>();
        crab.AddCandidates(ctx, output);

        var idleCandidate = output.First(c => c.Action.Id.Value == "crab_idle");
        var initialEnergy = crab.Energy;

        var outcome = new ActionOutcome(idleCandidate.Action.Id, ActionOutcomeType.Success,
            Duration.Seconds(1.0));
        domain.ApplyActionEffects(crabId, outcome, crab, world,
            new RandomRngStream(new Random(1)), _noReservations,
            idleCandidate.EffectHandler);

        Assert.True(crab.Energy > initialEnergy, "Crab energy should increase after idle/rest");
    }

    // ── 10. Crab physiology differences from humans ───────────────────────────

    [Fact]
    public void CrabPhysiologyBuff_DoesNotAlterMorale()
    {
        var crab = IslandDomainPack.CreateCrabActorState(new ActorId("crab1"));
        var initialMorale = crab.Morale;

        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actors = new Dictionary<ActorId, ActorState> { [crab.Id] = crab };

        // Tick many seconds — morale should not change since CrabPhysiologyBuff doesn't touch it.
        for (long tick = EngineConstants.TickHz; tick <= EngineConstants.TickHz * 3600L; tick += EngineConstants.TickHz)
            domain.TickWorldState(world, actors, tick, _noReservations);

        Assert.Equal(initialMorale, crab.Morale,
            precision: 3); // morale unchanged
    }

    [Fact]
    public void CrabPhysiologyBuff_VerySlowEnergyDrain()
    {
        var crab = IslandDomainPack.CreateCrabActorState(new ActorId("crab1"));
        crab.Satiety = 80.0;
        var initialEnergy = crab.Energy;

        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actors = new Dictionary<ActorId, ActorState> { [crab.Id] = crab };

        // Tick one sim-hour (3600 seconds).
        for (long tick = EngineConstants.TickHz; tick <= EngineConstants.TickHz * 3600L; tick += EngineConstants.TickHz)
            domain.TickWorldState(world, actors, tick, _noReservations);

        // Energy drain over 1 hour should be very small (< 1% of total 100 points).
        var energyDrained = initialEnergy - crab.Energy;
        Assert.True(energyDrained < 1.0,
            $"Crab energy drain over 1 hour should be < 1 point (was {energyDrained:F4})");
    }

    [Fact]
    public void CrabPhysiologyBuff_HealthDropsOnStarvation()
    {
        var crab = IslandDomainPack.CreateCrabActorState(new ActorId("crab1"));
        crab.Satiety = 0.0; // starving
        var initialHealth = crab.Health;

        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actors = new Dictionary<ActorId, ActorState> { [crab.Id] = crab };

        // Tick for several hours while starving.
        for (long tick = EngineConstants.TickHz; tick <= EngineConstants.TickHz * 7200L; tick += EngineConstants.TickHz)
            domain.TickWorldState(world, actors, tick, _noReservations);

        Assert.True(crab.Health < initialHealth,
            "Crab health should decrease when starving");
    }

    [Fact]
    public void CrabPhysiologyBuff_HealthRecoveryWhenWellFed()
    {
        var crab = IslandDomainPack.CreateCrabActorState(new ActorId("crab1"));
        crab.Satiety = 80.0;
        crab.Health = 50.0; // injured but well-fed
        var initialHealth = crab.Health;

        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actors = new Dictionary<ActorId, ActorState> { [crab.Id] = crab };

        // Tick for several sim-days.
        for (long tick = EngineConstants.TickHz; tick <= EngineConstants.TickHz * 7200L; tick += EngineConstants.TickHz)
        {
            domain.TickWorldState(world, actors, tick, _noReservations);
            // Keep satiety high so we test recovery not starvation.
            crab.Satiety = 80.0;
        }

        Assert.True(crab.Health > initialHealth,
            "Crab health should slowly recover when well-fed");
    }

    [Fact]
    public void CrabActor_MoraleNotAffectedByActionOutcomes()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var crabId = new ActorId("crab1");
        var crab = IslandDomainPack.CreateCrabActorState(crabId);
        crab.Status = ActorStatus.Ready;
        var initialMorale = crab.Morale;

        var candidates = domain.GenerateCandidates(crabId, crab, world, 0L, new Random(1), _noReservations);
        var idleCandidate = candidates.First(c => c.Action.Id.Value == "crab_idle");

        // Apply the idle effect — morale should remain unchanged.
        var outcome = new ActionOutcome(idleCandidate.Action.Id, ActionOutcomeType.Success,
            Duration.Seconds(1.0));
        domain.ApplyActionEffects(crabId, outcome, crab, world,
            new RandomRngStream(new Random(1)), _noReservations,
            idleCandidate.EffectHandler);

        Assert.Equal(initialMorale, crab.Morale,
            precision: 3); // no morale adjustment for crabs
    }
}
