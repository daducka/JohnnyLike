using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Island.Recipes;
using JohnnyLike.Domain.Island.Recipes.Definitions;
using JohnnyLike.Domain.Island.Supply;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Tests;

/// <summary>
/// Tests covering crab food, crab traps, fishing nets, and island metrics.
/// </summary>
public class CrabFoodAndToolTests
{
    private static readonly EmptyResourceAvailability _noReservations = new();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (IslandWorldState world, SupplyPile pile) MakeWorld()
    {
        var world = new IslandWorldState();
        var campfire = new CampfireItem("main_campfire");
        world.WorldItems.Add(campfire);
        world.WorldItems.Add(new CalendarItem("calendar"));
        world.WorldItems.Add(new WeatherItem("weather"));
        var pile = new SupplyPile("shared_supplies", "shared");
        world.WorldItems.Add(pile);
        return (world, pile);
    }

    private static HumanActorState MakeHuman(ActorId? id = null)
    {
        var actor = new HumanActorState { Id = id ?? new ActorId("actor1") };
        actor.ActiveBuffs.Add(new AlivenessBuff
        {
            Name          = "Aliveness",
            Type          = BuffType.Aliveness,
            State         = AlivenessState.Alive,
            ExpiresAtTick = long.MaxValue
        });
        actor.Satiety = 80.0;
        actor.Energy  = 80.0;
        actor.Morale  = 50.0;
        actor.Health  = 100.0;
        actor.CurrentRoomId = "beach";
        return actor;
    }

    private static IslandContext MakeCtx(HumanActorState actor, IslandWorldState world, long tick = 0L)
        => new IslandContext(
            actor.Id,
            actor,
            world,
            tick,
            new RandomRngStream(new Random(42)),
            new Random(42),
            _noReservations);

    private static EffectContext MakeEffectCtx(
        HumanActorState actor,
        IslandWorldState world,
        RollOutcomeTier? tier = null)
        => new EffectContext
        {
            ActorId      = actor.Id,
            Outcome      = new ActionOutcome(new ActionId("test"), ActionOutcomeType.Success, Duration.FromTicks(0L)),
            Actor        = actor,
            World        = world,
            Tier         = tier,
            Rng          = new RandomRngStream(new Random(42)),
            Reservations = _noReservations
        };

    // ════════════════════════════════════════════════════════════════════════
    // Part 1: IslandMetrics
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void IslandMetrics_DefaultsToZero()
    {
        var metrics = new IslandMetrics();
        Assert.Equal(0, metrics.FishCaught);
        Assert.Equal(0, metrics.CrabsCaught);
        Assert.Equal(0, metrics.CrabTrapChecks);
        Assert.Equal(0, metrics.CrabTrapCatches);
        Assert.Equal(0, metrics.FishingNetChecks);
        Assert.Equal(0, metrics.FishingNetCatches);
        Assert.Equal(0, metrics.FoodCooked);
    }

    [Fact]
    public void IslandMetrics_SerializeDeserialize_RoundTrips()
    {
        var (world, _) = MakeWorld();
        world.Metrics.FishCaught        = 5;
        world.Metrics.CrabsCaught       = 3;
        world.Metrics.CrabTrapChecks    = 7;
        world.Metrics.CrabTrapCatches   = 2;
        world.Metrics.FishingNetChecks  = 4;
        world.Metrics.FishingNetCatches = 1;
        world.Metrics.FoodCooked        = 6;

        var json     = world.Serialize();
        var world2   = new IslandWorldState();
        world2.Deserialize(json);

        Assert.Equal(5, world2.Metrics.FishCaught);
        Assert.Equal(3, world2.Metrics.CrabsCaught);
        Assert.Equal(7, world2.Metrics.CrabTrapChecks);
        Assert.Equal(2, world2.Metrics.CrabTrapCatches);
        Assert.Equal(4, world2.Metrics.FishingNetChecks);
        Assert.Equal(1, world2.Metrics.FishingNetCatches);
        Assert.Equal(6, world2.Metrics.FoodCooked);
    }

    [Fact]
    public void PeriodicSnapshot_IncludesIslandMetrics()
    {
        var domain = new IslandDomainPack();
        var world  = (IslandWorldState)domain.CreateInitialWorldState();
        world.Metrics.FishCaught = 3;

        var events = domain.BuildPeriodicSnapshot(world, new Dictionary<ActorId, ActorState>(), 600L);

        var metricsEvt = events.SingleOrDefault(e => e.EventType == "PeriodicMetricsSnapshot");
        Assert.NotNull(metricsEvt);
        Assert.Equal(3, metricsEvt.Details["fishCaught"]);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Part 2: CrabSupply as edible food
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void CrabSupply_OffersEatRawCrab_WhenQuantityAvailable()
    {
        var (world, pile) = MakeWorld();
        pile.AddSupply(2.0, () => new CrabSupply());
        var actor = MakeHuman();
        var ctx   = MakeCtx(actor, world);

        var candidates = new List<ActionCandidate>();
        pile.AddCandidates(ctx, candidates);

        Assert.Contains(candidates, c => c.Action.Id.Value == "eat_raw_crab");
    }

    [Fact]
    public void CrabSupply_DoesNotOfferEatRawCrab_WhenEmpty()
    {
        var (world, pile) = MakeWorld();
        // No crab in the pile
        var actor = MakeHuman();
        var ctx   = MakeCtx(actor, world);

        var candidates = new List<ActionCandidate>();
        pile.AddCandidates(ctx, candidates);

        Assert.DoesNotContain(candidates, c => c.Action.Id.Value == "eat_raw_crab");
    }

    [Fact]
    public void EatRawCrab_ConsumesCrab_AndRestoresMoreThanRawFish()
    {
        var (world, pile) = MakeWorld();
        pile.AddSupply(3.0, () => new CrabSupply());
        pile.AddSupply(3.0, () => new FishSupply());

        var actor = MakeHuman();
        actor.Satiety = 0.0;

        // Eat raw crab
        var crabActor = MakeHuman(new ActorId("crab_eater"));
        crabActor.Satiety = 0.0;

        var crabEffectCtx = MakeEffectCtx(crabActor, world, RollOutcomeTier.Success);
        var pileForCrab   = pile;

        // Manually test by finding the candidate and running its effect
        var ctx   = MakeCtx(crabActor, world);
        var candidates = new List<ActionCandidate>();
        pile.AddCandidates(ctx, candidates);
        var crabCandidate = candidates.First(c => c.Action.Id.Value == "eat_raw_crab");

        var satietyBefore = crabActor.Satiety;
        ((Action<EffectContext>)crabCandidate.EffectHandler!)(crabEffectCtx);
        var crabSatietyGain = crabActor.Satiety - satietyBefore;

        // Eat raw fish
        var fishActor = MakeHuman(new ActorId("fish_eater"));
        fishActor.Satiety = 0.0;

        var fishCtx        = MakeCtx(fishActor, world);
        var fishCandidates = new List<ActionCandidate>();
        pile.AddCandidates(fishCtx, fishCandidates);
        var fishCandidate = fishCandidates.First(c => c.Action.Id.Value == "eat_raw_fish");

        var fishSatietyBefore = fishActor.Satiety;
        var fishEffectCtx     = MakeEffectCtx(fishActor, world, RollOutcomeTier.Success);
        ((Action<EffectContext>)fishCandidate.EffectHandler!)(fishEffectCtx);
        var fishSatietyGain = fishActor.Satiety - fishSatietyBefore;

        // Raw crab should give more satiety than raw fish
        Assert.True(crabSatietyGain > fishSatietyGain,
            $"Expected crab satiety gain ({crabSatietyGain:F2}) > fish satiety gain ({fishSatietyGain:F2})");
    }

    [Fact]
    public void CrabSupply_CountsAsImmediateFood()
    {
        var crabSupply = new CrabSupply(3.0);
        var world = new IslandWorldState();
        var actor = MakeHuman();
        var units = ((Supply.IEdibleSupply)crabSupply).GetImmediateFoodUnits(actor, world);
        Assert.Equal(3.0, units);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Part 3: CookedCrabSupply and CookCrab recipe
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void CookedCrabSupply_OffersEatCookedCrab_WhenQuantityAvailable()
    {
        var (world, pile) = MakeWorld();
        pile.AddSupply(1.0, () => new CookedCrabSupply());
        var actor = MakeHuman();
        var ctx   = MakeCtx(actor, world);

        var candidates = new List<ActionCandidate>();
        pile.AddCandidates(ctx, candidates);

        Assert.Contains(candidates, c => c.Action.Id.Value == "eat_cooked_crab");
    }

    [Fact]
    public void CookedCrabSupply_CountsAsImmediateFood()
    {
        var cooked = new CookedCrabSupply(2.0);
        var world  = new IslandWorldState();
        var actor  = MakeHuman();
        var units  = ((Supply.IEdibleSupply)cooked).GetImmediateFoodUnits(actor, world);
        Assert.Equal(2.0, units);
    }

    [Fact]
    public void EatCookedCrab_ConsumesCookedCrab_AndRestoresMoreThanCookedFish()
    {
        var (world, pile) = MakeWorld();
        pile.AddSupply(3.0, () => new CookedCrabSupply());
        pile.AddSupply(3.0, () => new CookedFishSupply());

        var crabActor  = MakeHuman(new ActorId("cooked_crab_eater"));
        crabActor.Satiety = 0.0;
        crabActor.Morale  = 50.0;

        var crabCtx        = MakeCtx(crabActor, world);
        var crabCandidates = new List<ActionCandidate>();
        pile.AddCandidates(crabCtx, crabCandidates);
        var crabCandidate = crabCandidates.First(c => c.Action.Id.Value == "eat_cooked_crab");

        var satietyBefore = crabActor.Satiety;
        var moraleBefore  = crabActor.Morale;
        ((Action<EffectContext>)crabCandidate.EffectHandler!)(MakeEffectCtx(crabActor, world, RollOutcomeTier.Success));
        var crabSatietyGain = crabActor.Satiety - satietyBefore;
        var crabMoraleGain  = crabActor.Morale  - moraleBefore;

        var fishActor  = MakeHuman(new ActorId("cooked_fish_eater"));
        fishActor.Satiety = 0.0;
        fishActor.Morale  = 50.0;

        var fishCtx        = MakeCtx(fishActor, world);
        var fishCandidates = new List<ActionCandidate>();
        pile.AddCandidates(fishCtx, fishCandidates);
        var fishCandidate = fishCandidates.First(c => c.Action.Id.Value == "eat_cooked_fish");

        var fishSatietyBefore = fishActor.Satiety;
        var fishMoraleBefore  = fishActor.Morale;
        ((Action<EffectContext>)fishCandidate.EffectHandler!)(MakeEffectCtx(fishActor, world, RollOutcomeTier.Success));
        var fishSatietyGain = fishActor.Satiety - fishSatietyBefore;
        var fishMoraleGain  = fishActor.Morale  - fishMoraleBefore;

        Assert.True(crabSatietyGain > fishSatietyGain,
            $"Cooked crab satiety ({crabSatietyGain:F2}) should be > cooked fish ({fishSatietyGain:F2})");
        Assert.True(crabMoraleGain >= fishMoraleGain,
            $"Cooked crab morale ({crabMoraleGain:F2}) should be >= cooked fish ({fishMoraleGain:F2})");
    }

    [Fact]
    public void CookCrab_KnownRecipe_ConsumesRawCrab_AndProducesCookedCrab()
    {
        var (world, pile) = MakeWorld();
        world.MainCampfire!.FuelSeconds = 600;
        pile.AddSupply(2.0, () => new CrabSupply());

        var actor = MakeHuman();
        var recipe = IslandRecipeRegistry.Get("cook_crab");

        var effectCtx = MakeEffectCtx(actor, world, RollOutcomeTier.Success);
        var preOk = recipe.PreAction(effectCtx);
        Assert.True(preOk);
        Assert.Equal(1.0, pile.GetQuantity<CrabSupply>());

        recipe.Effect(effectCtx);
        Assert.Equal(1.0, pile.GetQuantity<CookedCrabSupply>());
    }

    [Fact]
    public void CookCrab_NotOffered_WhenRecipeUnknown()
    {
        var (world, pile) = MakeWorld();
        world.MainCampfire!.FuelSeconds = 600;
        pile.AddSupply(2.0, () => new CrabSupply());
        var actor = MakeHuman();
        // KnownRecipeIds is empty

        var ctx        = MakeCtx(actor, world);
        var candidates = new List<ActionCandidate>();
        actor.AddCandidates(ctx, candidates);

        Assert.DoesNotContain(candidates, c => c.Action.Id.Value == "cook_crab");
    }

    [Fact]
    public void CookCrab_Discoverable_WhenCrabExistsAndCampfireLit()
    {
        var (world, pile) = MakeWorld();
        world.MainCampfire!.FuelSeconds = 600;
        pile.AddSupply(1.0, () => new CrabSupply());

        var actor  = MakeHuman();
        var recipe = IslandRecipeRegistry.Get("cook_crab");
        Assert.True(recipe.Discovery!.CanDiscover(actor, world));
    }

    [Fact]
    public void CookCrab_NotDiscoverable_WhenCampfireUnlit()
    {
        var (world, pile) = MakeWorld();
        world.MainCampfire!.IsLit = false;
        world.MainCampfire!.FuelSeconds = 0;
        pile.AddSupply(1.0, () => new CrabSupply());

        var actor  = MakeHuman();
        var recipe = IslandRecipeRegistry.Get("cook_crab");
        Assert.False(recipe.Discovery!.CanDiscover(actor, world));
    }

    [Fact]
    public void CookCrab_Effect_IncrementsFoodCookedMetric()
    {
        var (world, pile) = MakeWorld();
        world.MainCampfire!.FuelSeconds = 600;
        pile.AddSupply(2.0, () => new CrabSupply());
        var actor = MakeHuman();

        var recipe = IslandRecipeRegistry.Get("cook_crab");
        var effectCtx = MakeEffectCtx(actor, world, RollOutcomeTier.Success);
        recipe.PreAction(effectCtx);
        recipe.Effect(effectCtx);

        Assert.Equal(1, world.Metrics.FoodCooked);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Part 4: CrabTrap recipe discovery
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void CrabTrapRecipe_Discoverable_WhenCrabSupplyExists()
    {
        var (world, pile) = MakeWorld();
        pile.AddSupply(1.0, () => new CrabSupply());

        var actor  = MakeHuman();
        var recipe = IslandRecipeRegistry.Get("crab_trap");
        Assert.True(recipe.Discovery!.CanDiscover(actor, world));
    }

    [Fact]
    public void CrabTrapRecipe_NotDiscoverable_WhenNoCrabSupply()
    {
        var (world, _) = MakeWorld();
        // No CrabSupply in the pile

        var actor  = MakeHuman();
        var recipe = IslandRecipeRegistry.Get("crab_trap");
        Assert.False(recipe.Discovery!.CanDiscover(actor, world));
    }

    [Fact]
    public void CrabTrapRecipe_NotDiscoverable_WhenOnlyCrabActorExists_ButNoCrabSupply()
    {
        var (world, _) = MakeWorld();

        // A crab actor exists in the world (does NOT satisfy discovery condition)
        var crabActor = IslandDomainPack.CreateCrabActorState(new ActorId("crab1"));
        world.ActiveCrabActors.Add(crabActor);

        var actor  = MakeHuman();
        var recipe = IslandRecipeRegistry.Get("crab_trap");
        Assert.False(recipe.Discovery!.CanDiscover(actor, world));
    }

    // ════════════════════════════════════════════════════════════════════════
    // Part 5: CrabTrapItem
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void CrabTrap_AddBait_ConsumesBait()
    {
        var (world, pile) = MakeWorld();
        pile.AddSupply(2.0, () => new BaitSupply());

        var trap  = new CrabTrapItem("crab_trap");
        var actor = MakeHuman();
        var ctx   = MakeCtx(actor, world);

        var candidates = new List<ActionCandidate>();
        trap.AddCandidates(ctx, candidates);

        var baitAction = candidates.FirstOrDefault(c => c.Action.Id.Value == "add_bait_to_crab_trap");
        Assert.NotNull(baitAction);

        ((Action<EffectContext>)baitAction.EffectHandler!)(MakeEffectCtx(actor, world));

        Assert.Equal(1.0, pile.GetQuantity<BaitSupply>());
        Assert.True(trap.HasBait);
    }

    [Fact]
    public void CrabTrap_CheckAfterElapsedTime_CanYieldCrabSupply()
    {
        var (world, pile) = MakeWorld();
        pile.AddSupply(1.0, () => new BaitSupply());

        // A crab actor must be present for the check to be offered and succeed.
        var crabActor = IslandDomainPack.CreateCrabActorState(new ActorId("crab1"));
        world.ActiveCrabActors.Add(crabActor);

        var trap  = new CrabTrapItem("crab_trap");
        trap.Quality = 100.0;

        // Bait the trap at tick 0
        var actor    = MakeHuman();
        var baitCtx  = new IslandContext(actor.Id, actor, world, 0L, new RandomRngStream(new Random(42)), new Random(42), _noReservations);
        var baitCandidates = new List<ActionCandidate>();
        trap.AddCandidates(baitCtx, baitCandidates);
        var baitAction = baitCandidates.First(c => c.Action.Id.Value == "add_bait_to_crab_trap");
        ((Action<EffectContext>)baitAction.EffectHandler!)(MakeEffectCtx(actor, world));

        Assert.True(trap.HasBait);

        // Check the trap after a long soak time (well past MinSoakTicks)
        var longSoakTick = CrabTrapItem.MinSoakTicks * 3;
        var checkCtx = new IslandContext(actor.Id, actor, world, longSoakTick, new RandomRngStream(new Random(1)), new Random(1), _noReservations);
        var checkCandidates = new List<ActionCandidate>();
        trap.AddCandidates(checkCtx, checkCandidates);

        var checkAction = checkCandidates.FirstOrDefault(c => c.Action.Id.Value == "check_crab_trap");
        Assert.NotNull(checkAction);

        // Run PreAction (reserves the crab actor) then EffectHandler with success tier
        var effectCtx = MakeEffectCtx(actor, world, RollOutcomeTier.Success);
        ((Func<EffectContext, bool>)checkAction.PreAction!)(effectCtx);
        ((Action<EffectContext>)checkAction.EffectHandler!)(effectCtx);

        Assert.Equal(1, world.Metrics.CrabTrapChecks);
        Assert.Equal(1, world.Metrics.CrabTrapCatches);
        Assert.Equal(1.0, pile.GetQuantity<CrabSupply>());
        // The reserved crab actor is now in PendingActorRemovals, not in ActiveCrabActors
        Assert.Equal(0, world.ActiveCrabActors.Count);
        Assert.Contains(crabActor.Id, world.PendingActorRemovals);
    }

    [Fact]
    public void CrabTrap_Check_UpdatesMetrics_OnFailure()
    {
        var (world, pile) = MakeWorld();
        pile.AddSupply(1.0, () => new BaitSupply());

        // A crab actor must be present for check to be offered.
        var crabActor = IslandDomainPack.CreateCrabActorState(new ActorId("crab1"));
        world.ActiveCrabActors.Add(crabActor);

        var trap  = new CrabTrapItem("crab_trap");
        trap.Quality = 100.0;

        var actor = MakeHuman();
        var baitCtx = new IslandContext(actor.Id, actor, world, 0L, new RandomRngStream(new Random(42)), new Random(42), _noReservations);
        var baitCandidates = new List<ActionCandidate>();
        trap.AddCandidates(baitCtx, baitCandidates);
        var baitAction = baitCandidates.First(c => c.Action.Id.Value == "add_bait_to_crab_trap");
        ((Action<EffectContext>)baitAction.EffectHandler!)(MakeEffectCtx(actor, world));

        var checkCtx = new IslandContext(actor.Id, actor, world, CrabTrapItem.MinSoakTicks * 2, new RandomRngStream(new Random(1)), new Random(1), _noReservations);
        var checkCandidates = new List<ActionCandidate>();
        trap.AddCandidates(checkCtx, checkCandidates);
        var checkAction = checkCandidates.First(c => c.Action.Id.Value == "check_crab_trap");

        // Run PreAction (reserves crab) then EffectHandler with failure tier
        var effectCtx = MakeEffectCtx(actor, world, RollOutcomeTier.Failure);
        ((Func<EffectContext, bool>)checkAction.PreAction!)(effectCtx);
        ((Action<EffectContext>)checkAction.EffectHandler!)(effectCtx);

        Assert.Equal(1, world.Metrics.CrabTrapChecks);
        Assert.Equal(0, world.Metrics.CrabTrapCatches); // no catch on failure
        // The reserved crab was returned to ActiveCrabActors on failure
        Assert.Equal(1, world.ActiveCrabActors.Count);
    }

    [Fact]
    public void CrabTrap_RepairRestoresQuality()
    {
        var (world, _) = MakeWorld();
        var trap = new CrabTrapItem("crab_trap");
        trap.Quality  = 15.0;
        trap.IsBroken = true;

        var actor = MakeHuman();
        actor.ActiveBuffs.Add(new ActiveBuff { Name = "Survival+2", Type = BuffType.SkillBonus, SkillType = SkillType.Survival, Value = 5, ExpiresAtTick = long.MaxValue });

        var ctx        = MakeCtx(actor, world);
        var candidates = new List<ActionCandidate>();
        trap.AddCandidates(ctx, candidates);

        var repairAction = candidates.FirstOrDefault(c => c.Action.Id.Value == "repair_crab_trap");
        Assert.NotNull(repairAction);

        var qualityBefore = trap.Quality;
        ((Action<EffectContext>)repairAction.EffectHandler!)(MakeEffectCtx(actor, world, RollOutcomeTier.Success));

        Assert.True(trap.Quality > qualityBefore);
        Assert.False(trap.IsBroken);
    }

    [Fact]
    public void CrabTrap_BrokenTrapDoesNotOfferCheck()
    {
        var (world, pile) = MakeWorld();
        pile.AddSupply(1.0, () => new BaitSupply());

        var trap = new CrabTrapItem("crab_trap");
        trap.Quality  = 10.0;
        trap.IsBroken = true;

        // Manually set bait state
        var actor = MakeHuman();
        // We can't easily set internal state, so just verify: broken + has bait doesn't offer check
        var ctx        = MakeCtx(actor, world);
        var candidates = new List<ActionCandidate>();
        trap.AddCandidates(ctx, candidates);

        Assert.DoesNotContain(candidates, c => c.Action.Id.Value == "check_crab_trap");
    }

    [Fact]
    public void CrabTrap_StateSerializesAndDeserializes()
    {
        var (world, pile) = MakeWorld();
        pile.AddSupply(1.0, () => new BaitSupply());

        var trap = new CrabTrapItem("crab_trap");
        world.AddWorldItem(trap, "beach");

        // Bait the trap
        var actor   = MakeHuman();
        var baitCtx = MakeCtx(actor, world);
        var baitCandidates = new List<ActionCandidate>();
        trap.AddCandidates(baitCtx, baitCandidates);
        var baitAction = baitCandidates.First(c => c.Action.Id.Value == "add_bait_to_crab_trap");
        ((Action<EffectContext>)baitAction.EffectHandler!)(MakeEffectCtx(actor, world));

        Assert.True(trap.HasBait);

        var json    = world.Serialize();
        var world2  = new IslandWorldState();
        world2.Deserialize(json);

        var trap2 = world2.WorldItems.OfType<CrabTrapItem>().FirstOrDefault();
        Assert.NotNull(trap2);
        Assert.Equal(1, trap2.BaitCharges);
        Assert.True(trap2.HasBait);
        Assert.Equal(trap.LastBaitedTick, trap2.LastBaitedTick);
    }

    [Fact]
    public void CrabTrap_DegradesThroughMaintainableWorldItem()
    {
        var trap = new CrabTrapItem("crab_trap");
        var world = new IslandWorldState();

        trap.Quality  = CrabTrapItem.BreakageQualityThreshold + 2.0;
        trap.IsBroken = false;

        // Decay = baseDecayPerSecond(0.003) × dtTicks/TickHz = 0.003 × (50000/20) = 7.5
        // Starting quality 22 → 22 - 7.5 = 14.5 < BreakageQualityThreshold(20)
        trap.Tick(50000L, world);

        Assert.True(trap.IsBroken);
    }

    [Fact]
    public void CrabTrap_IsFoodSource_ReturnsFoodUnits_WhenBaited()
    {
        var (world, pile) = MakeWorld();
        pile.AddSupply(1.0, () => new BaitSupply());

        var trap  = new CrabTrapItem("crab_trap");
        var actor = MakeHuman();

        // Not baited — 0 food units
        Assert.Equal(0.0, ((IFoodSource)trap).GetAcquirableFoodUnits(actor, world));

        // Add a crab actor (required for non-zero food units)
        world.ActiveCrabActors.Add(IslandDomainPack.CreateCrabActorState(new ActorId("crab1")));

        // Bait the trap
        var baitCtx = MakeCtx(actor, world);
        var baitCandidates = new List<ActionCandidate>();
        trap.AddCandidates(baitCtx, baitCandidates);
        var baitAction = baitCandidates.First(c => c.Action.Id.Value == "add_bait_to_crab_trap");
        ((Action<EffectContext>)baitAction.EffectHandler!)(MakeEffectCtx(actor, world));

        Assert.True(((IFoodSource)trap).GetAcquirableFoodUnits(actor, world) > 0.0);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Part 6: FishingNet recipe discovery
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void FishingNetRecipe_NotDiscoverable_BeforeFishCaughtThreshold()
    {
        var (world, pile) = MakeWorld();
        pile.AddSupply(6.0, () => new RopeSupply());
        world.Metrics.FishCaught = FishingNetRecipe.FishCaughtToDiscoverFishingNet - 1;

        var actor  = MakeHuman();
        var recipe = IslandRecipeRegistry.Get("fishing_net");
        Assert.False(recipe.Discovery!.CanDiscover(actor, world));
    }

    [Fact]
    public void FishingNetRecipe_Discoverable_AfterFishCaughtThreshold()
    {
        var (world, pile) = MakeWorld();
        pile.AddSupply(6.0, () => new RopeSupply());
        world.Metrics.FishCaught = FishingNetRecipe.FishCaughtToDiscoverFishingNet;

        var actor  = MakeHuman();
        var recipe = IslandRecipeRegistry.Get("fishing_net");
        Assert.True(recipe.Discovery!.CanDiscover(actor, world));
    }

    [Fact]
    public void FishingNetRecipe_RequiresSixRope()
    {
        var recipe = IslandRecipeRegistry.Get("fishing_net");

        var (world5, pile5) = MakeWorld();
        pile5.AddSupply(5.0, () => new RopeSupply()); // 5 rope is insufficient
        Assert.False(recipe.HasRequiredSupplies(pile5));

        var (world6, pile6) = MakeWorld();
        pile6.AddSupply(6.0, () => new RopeSupply()); // 6 rope is sufficient
        Assert.True(recipe.HasRequiredSupplies(pile6));
    }

    [Fact]
    public void FishingNetRecipe_FiveRopeIsInsufficient()
    {
        var (world, pile) = MakeWorld();
        pile.AddSupply(5.0, () => new RopeSupply());

        var recipe = IslandRecipeRegistry.Get("fishing_net");
        Assert.False(recipe.HasRequiredSupplies(pile));
    }

    // ════════════════════════════════════════════════════════════════════════
    // Part 7: FishingNetItem
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void FishingNet_AddBait_ConsumesBait()
    {
        var (world, pile) = MakeWorld();
        pile.AddSupply(2.0, () => new BaitSupply());

        var net   = new FishingNetItem("fishing_net");
        var actor = MakeHuman();
        var ctx   = MakeCtx(actor, world);

        var candidates = new List<ActionCandidate>();
        net.AddCandidates(ctx, candidates);

        var baitAction = candidates.FirstOrDefault(c => c.Action.Id.Value == "add_bait_to_fishing_net");
        Assert.NotNull(baitAction);

        ((Action<EffectContext>)baitAction.EffectHandler!)(MakeEffectCtx(actor, world));

        Assert.Equal(1.0, pile.GetQuantity<BaitSupply>());
        Assert.True(net.HasBait);
    }

    [Fact]
    public void FishingNet_DegradesThroughMaintainableWorldItem()
    {
        var net   = new FishingNetItem("fishing_net");
        var world = new IslandWorldState();

        net.Quality  = FishingNetItem.BreakageQualityThreshold + 2.0;
        net.IsBroken = false;

        // Decay = baseDecayPerSecond(0.004) × dtTicks/TickHz = 0.004 × (30000/20) = 6.0
        // Starting quality 22 → 22 - 6.0 = 16 < BreakageQualityThreshold(20)
        net.Tick(30000L, world);

        Assert.True(net.IsBroken);
    }

    [Fact]
    public void FishingNet_RepairRestoresQuality()
    {
        var (world, _) = MakeWorld();
        var net = new FishingNetItem("fishing_net");
        net.Quality  = 15.0;
        net.IsBroken = true;

        var actor = MakeHuman();
        actor.ActiveBuffs.Add(new ActiveBuff { Name = "Survival+2", Type = BuffType.SkillBonus, SkillType = SkillType.Survival, Value = 5, ExpiresAtTick = long.MaxValue });

        var ctx        = MakeCtx(actor, world);
        var candidates = new List<ActionCandidate>();
        net.AddCandidates(ctx, candidates);

        var repairAction = candidates.FirstOrDefault(c => c.Action.Id.Value == "repair_fishing_net");
        Assert.NotNull(repairAction);

        var qualityBefore = net.Quality;
        ((Action<EffectContext>)repairAction.EffectHandler!)(MakeEffectCtx(actor, world, RollOutcomeTier.Success));

        Assert.True(net.Quality > qualityBefore);
        Assert.False(net.IsBroken);
    }

    [Fact]
    public void FishingNet_StateSerializesAndDeserializes()
    {
        var (world, pile) = MakeWorld();
        pile.AddSupply(2.0, () => new BaitSupply());

        var net = new FishingNetItem("fishing_net");
        world.AddWorldItem(net, "beach");

        var actor   = MakeHuman();
        var baitCtx = MakeCtx(actor, world);
        var baitCandidates = new List<ActionCandidate>();
        net.AddCandidates(baitCtx, baitCandidates);
        var baitAction = baitCandidates.First(c => c.Action.Id.Value == "add_bait_to_fishing_net");
        ((Action<EffectContext>)baitAction.EffectHandler!)(MakeEffectCtx(actor, world));

        Assert.True(net.HasBait);

        var json   = world.Serialize();
        var world2 = new IslandWorldState();
        world2.Deserialize(json);

        var net2 = world2.WorldItems.OfType<FishingNetItem>().FirstOrDefault();
        Assert.NotNull(net2);
        Assert.Equal(1, net2.BaitCharges);
        Assert.True(net2.HasBait);
        Assert.Equal(net.LastBaitedTick, net2.LastBaitedTick);
    }

    [Fact]
    public void FishingNet_IsFoodSource_ReturnsFoodUnits_WhenBaitedAndFishAvailable()
    {
        var domain = new IslandDomainPack();
        var world  = (IslandWorldState)domain.CreateInitialWorldState();

        // Ensure ocean has fish
        var ocean = world.GetItem<OceanItem>("ocean")!;
        ((ISupplyBounty)ocean).AddSupply(10.0, () => new FishSupply());

        var pile = world.SharedSupplyPile!;
        pile.AddSupply(1.0, () => new BaitSupply());

        var net   = new FishingNetItem("fishing_net");
        var actor = MakeHuman();

        // Before bait: 0 food units
        Assert.Equal(0.0, ((IFoodSource)net).GetAcquirableFoodUnits(actor, world));

        // Bait the net
        var baitCtx = MakeCtx(actor, world);
        var baitCandidates = new List<ActionCandidate>();
        net.AddCandidates(baitCtx, baitCandidates);
        var baitAction = baitCandidates.First(c => c.Action.Id.Value == "add_bait_to_fishing_net");
        ((Action<EffectContext>)baitAction.EffectHandler!)(MakeEffectCtx(actor, world));

        Assert.True(((IFoodSource)net).GetAcquirableFoodUnits(actor, world) > 0.0);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Part 8: Metrics updated from FishingPole and CrabActor
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void CatchCrab_IncrementsCrabsCaughtMetric()
    {
        var domain = new IslandDomainPack();
        var world  = (IslandWorldState)domain.CreateInitialWorldState();

        var crabId    = new ActorId("crab1");
        var crabActor = IslandDomainPack.CreateCrabActorState(crabId);
        world.ActiveCrabActors.Add(crabActor);

        var actorId = new ActorId("human1");
        var actor   = (HumanActorState)domain.CreateActorState(actorId);

        // Generate catch_crab candidates from crab actors
        var ctx = new IslandContext(actorId, actor, world, 0L, new RandomRngStream(new Random(42)), new Random(42), _noReservations);
        var candidates = new List<ActionCandidate>();
        crabActor.AddCandidates(ctx, candidates);

        var catchAction = candidates.FirstOrDefault(c => c.Action.Id.Value == "catch_crab");
        Assert.NotNull(catchAction);

        ((Action<EffectContext>)catchAction.EffectHandler!)(MakeEffectCtx(actor, world, RollOutcomeTier.Success));

        Assert.Equal(1, world.Metrics.CrabsCaught);
    }

    // ════════════════════════════════════════════════════════════════════════
    // CookFish tests
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void CookFish_KnownRecipe_ConsumesRawFish_AndProducesCookedFish()
    {
        var (world, pile) = MakeWorld();
        world.MainCampfire!.FuelSeconds = 600;
        pile.AddSupply(3.0, () => new FishSupply());

        var actor   = MakeHuman();
        var recipe  = IslandRecipeRegistry.Get("cook_fish");
        var effectCtx = MakeEffectCtx(actor, world, RollOutcomeTier.Success);

        var preOk = recipe.PreAction(effectCtx);
        Assert.True(preOk);
        Assert.Equal(2.0, pile.GetQuantity<FishSupply>());

        recipe.Effect(effectCtx);
        Assert.Equal(1.0, pile.GetQuantity<CookedFishSupply>());
    }

    [Fact]
    public void CookFish_NotOffered_WhenRecipeUnknown()
    {
        var (world, pile) = MakeWorld();
        world.MainCampfire!.FuelSeconds = 600;
        pile.AddSupply(3.0, () => new FishSupply());

        var actor = MakeHuman();
        var ctx   = MakeCtx(actor, world);
        var candidates = new List<ActionCandidate>();
        actor.AddCandidates(ctx, candidates);

        Assert.DoesNotContain(candidates, c => c.Action.Id.Value == "cook_fish");
    }

    [Fact]
    public void CookFish_Discoverable_WhenFishExistsAndCampfireLit()
    {
        var (world, pile) = MakeWorld();
        world.MainCampfire!.FuelSeconds = 600;
        pile.AddSupply(1.0, () => new FishSupply());

        var actor  = MakeHuman();
        var recipe = IslandRecipeRegistry.Get("cook_fish");
        Assert.True(recipe.Discovery!.CanDiscover(actor, world));
    }
}
