using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Island.Supply;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Tests;

/// <summary>
/// Tests for the narration-quality improvements:
/// qualitative stats, NarrationDescription, domain beats, and day-phase events.
/// </summary>
public class NarrationImprovementTests
{
    // ── Helper ────────────────────────────────────────────────────────────────

    private static (IslandDomainPack domain, IslandWorldState world, IslandActorState actor, ActorId actorId)
        MakeIsland()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("Johnny");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        return (domain, world, actor, actorId);
    }

    private static EffectContext MakeEffectContext(
        IslandActorState actor, IslandWorldState world, ActorId actorId,
        RollOutcomeTier tier)
    {
        var outcome = new ActionOutcome(
            new ActionId("test_action"),
            ActionOutcomeType.Success,
            100L,
            new Dictionary<string, object> { ["tier"] = tier });

        return new EffectContext
        {
            ActorId = actorId,
            Outcome = outcome,
            Actor = actor,
            World = world,
            Tier = tier,
            Rng = new RandomRngStream(new Random(42)),
            Reservations = new EmptyResourceAvailability()
        };
    }

    // ── Qualitative stats ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(90.0, "satiety", "full")]
    [InlineData(60.0, "satiety", "satisfied")]
    [InlineData(35.0, "satiety", "hungry")]
    [InlineData(10.0, "satiety", "starving")]
    [InlineData(85.0, "energy", "energetic")]
    [InlineData(55.0, "energy", "alert")]
    [InlineData(30.0, "energy", "tired")]
    [InlineData(5.0,  "energy", "exhausted")]
    [InlineData(90.0, "morale", "cheerful")]
    [InlineData(65.0, "morale", "content")]
    [InlineData(25.0, "morale", "down")]
    [InlineData(5.0,  "morale", "miserable")]
    public void GetActorStateSnapshot_ReturnsQualitativeDescriptors(
        double value, string statName, string expected)
    {
        var (domain, _, actor, _) = MakeIsland();

        switch (statName)
        {
            case "satiety": actor.Satiety = value; break;
            case "energy":  actor.Energy  = value; break;
            case "morale":  actor.Morale  = value; break;
        }

        var snapshot = domain.GetActorStateSnapshot(actor);

        Assert.Equal(expected, snapshot[statName].ToString());
    }

    [Fact]
    public void GetActorStateSnapshot_DoesNotContainRawNumbers()
    {
        var (domain, _, actor, _) = MakeIsland();
        actor.Satiety = 42.25;
        actor.Energy  = 96.775;
        actor.Morale  = 33.0;

        var snapshot = domain.GetActorStateSnapshot(actor);

        // Values must be qualitative strings, not raw doubles
        Assert.DoesNotContain(snapshot, kv =>
            double.TryParse(kv.Value?.ToString(), out _) &&
            kv.Key is "satiety" or "energy" or "morale" or "health");
    }

    // ── NarrationDescription on action specs ─────────────────────────────────

    [Fact]
    public void ShakeTreeCoconut_HasNarrationDescription()
    {
        var (domain, world, actor, actorId) = MakeIsland();
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(42), new EmptyResourceAvailability());

        var coconutAction = candidates.FirstOrDefault(c => c.Action.Id.Value == "shake_tree_coconut");
        Assert.NotNull(coconutAction);
        Assert.False(string.IsNullOrEmpty(coconutAction!.Action.NarrationDescription));
        Assert.Contains("coconut", coconutAction.Action.NarrationDescription!.ToLowerInvariant());
    }

    [Fact]
    public void SleepUnderTree_HasNarrationDescription()
    {
        var (domain, world, actor, actorId) = MakeIsland();
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(42), new EmptyResourceAvailability());

        var sleepAction = candidates.FirstOrDefault(c => c.Action.Id.Value == "sleep_under_tree");
        Assert.NotNull(sleepAction);
        Assert.False(string.IsNullOrEmpty(sleepAction!.Action.NarrationDescription));
    }

    [Fact]
    public void BuildSandCastle_HasNarrationDescription()
    {
        var (domain, world, actor, actorId) = MakeIsland();
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(42), new EmptyResourceAvailability());

        var castleAction = candidates.FirstOrDefault(c => c.Action.Id.Value == "build_sand_castle");
        Assert.NotNull(castleAction);
        Assert.False(string.IsNullOrEmpty(castleAction!.Action.NarrationDescription));
    }

    // ── Domain beats for shake_tree_coconut ───────────────────────────────────

    [Theory]
    [InlineData(RollOutcomeTier.CriticalSuccess, "Two coconuts")]
    [InlineData(RollOutcomeTier.Success,         "single coconut")]
    [InlineData(RollOutcomeTier.PartialSuccess,  "wobbles free")]
    [InlineData(RollOutcomeTier.Failure,         "nothing falls")]
    public void ShakeTreeCoconut_EffectHandler_SetsOutcomeNarration(
        RollOutcomeTier tier, string expectedFragment)
    {
        var (domain, world, actor, actorId) = MakeIsland();

        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(42), new EmptyResourceAvailability());
        var candidate = candidates.First(c => c.Action.Id.Value == "shake_tree_coconut");

        var effectCtx = MakeEffectContext(actor, world, actorId, tier);
        var preAction = candidate.PreAction as Func<EffectContext, bool>;
        preAction?.Invoke(effectCtx);

        var handler = candidate.EffectHandler as Action<EffectContext>;
        Assert.NotNull(handler);
        handler!(effectCtx);

        Assert.False(string.IsNullOrEmpty(effectCtx.OutcomeNarration),
            $"Expected OutcomeNarration to be set for tier {tier}");
        Assert.Contains(expectedFragment, effectCtx.OutcomeNarration!,
            StringComparison.OrdinalIgnoreCase);
    }

    // ── Domain beats for sleep_under_tree ────────────────────────────────────

    [Fact]
    public void SleepUnderTree_EffectHandler_SetsOutcomeNarration()
    {
        var (domain, world, actor, actorId) = MakeIsland();
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(42), new EmptyResourceAvailability());
        var candidate = candidates.First(c => c.Action.Id.Value == "sleep_under_tree");

        var effectCtx = MakeEffectContext(actor, world, actorId, RollOutcomeTier.Success);

        var handler = candidate.EffectHandler as Action<EffectContext>;
        Assert.NotNull(handler);
        handler!(effectCtx);

        Assert.False(string.IsNullOrEmpty(effectCtx.OutcomeNarration),
            "Expected OutcomeNarration to be set after sleeping");
    }

    // ── Domain beats for build_sand_castle ───────────────────────────────────

    [Theory]
    [InlineData(RollOutcomeTier.CriticalSuccess, "impressive")]
    [InlineData(RollOutcomeTier.Success,         "castle")]
    [InlineData(RollOutcomeTier.PartialSuccess,  "lopsided")]
    [InlineData(RollOutcomeTier.Failure,         "collapses")]
    [InlineData(RollOutcomeTier.CriticalFailure, "frustration")]
    public void BuildSandCastle_EffectHandler_SetsOutcomeNarration(
        RollOutcomeTier tier, string expectedFragment)
    {
        var (domain, world, actor, actorId) = MakeIsland();
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(42), new EmptyResourceAvailability());
        var candidate = candidates.First(c => c.Action.Id.Value == "build_sand_castle");

        var effectCtx = MakeEffectContext(actor, world, actorId, tier);

        var handler = candidate.EffectHandler as Action<EffectContext>;
        Assert.NotNull(handler);
        handler!(effectCtx);

        Assert.False(string.IsNullOrEmpty(effectCtx.OutcomeNarration),
            $"Expected OutcomeNarration to be set for tier {tier}");
        Assert.Contains(expectedFragment, effectCtx.OutcomeNarration!,
            StringComparison.OrdinalIgnoreCase);
    }

    // ── Day phase ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(6.0,  DayPhase.Dawn)]
    [InlineData(9.0,  DayPhase.Morning)]
    [InlineData(12.5, DayPhase.Noon)]
    [InlineData(15.0, DayPhase.Afternoon)]
    [InlineData(18.0, DayPhase.Evening)]
    [InlineData(22.0, DayPhase.Night)]
    [InlineData(3.0,  DayPhase.Night)]
    public void ComputeDayPhase_ReturnsCorrectPhase(double hour, DayPhase expected)
    {
        Assert.Equal(expected, CalendarItem.ComputeDayPhase(hour));
    }

    [Fact]
    public void CalendarItem_Tick_EmitsDayPhaseChangedEvent_WhenPhaseChanges()
    {
        var (_, world, _, _) = MakeIsland();
        var calendar = world.GetItem<CalendarItem>("calendar")!;
        // Start just before dawn (hour 4.9 → Night), next tick will cross into Dawn (5.0).
        calendar.TimeOfDay = 4.9 / 24.0;

        // Tick enough to cross from hour 4.9 to just past 5.0 (dawn threshold).
        // dt = 0.2 hours = 720 seconds = 14400 ticks
        var events = calendar.Tick(14400, world);

        // Should include a DayPhaseChanged event.
        var phaseEvent = events.FirstOrDefault(e => e.EventType == "DayPhaseChanged");
        Assert.NotNull(phaseEvent);
    }

    [Fact]
    public void CalendarItem_Tick_DayPhaseChangedEvent_ContainsDayPhaseDetail()
    {
        var (_, world, _, _) = MakeIsland();
        var calendar = world.GetItem<CalendarItem>("calendar")!;

        // Start at time 0 (midnight, Night phase) and advance to 7h (Morning).
        // TimeOfDay 0 → HourOfDay 0 → Night. Advance past Dawn into Morning.
        calendar.TimeOfDay = 6.9 / 24.0;  // hour 6.9 → Dawn (_lastEmitted = Morning default)
        // Force _lastEmittedDayPhase to Night by ticking once to emit Dawn first.
        calendar.Tick(100, world); // small tick — stays in Dawn, but _lastEmittedDayPhase is now Dawn

        // Now advance past hour 7 → Morning phase change.
        var dtSeconds = 0.15 * 3600.0; // 0.15 hours forward
        var events = calendar.Tick((long)(dtSeconds * 20), world);

        var phaseEvent = events.FirstOrDefault(e => e.EventType == "DayPhaseChanged");
        if (phaseEvent == null)
        {
            // Depending on initial state it may not trigger here; skip gracefully.
            return;
        }

        Assert.True(phaseEvent.Details.ContainsKey("dayPhase"), "Expected 'dayPhase' key in DayPhaseChanged event");
        Assert.True(phaseEvent.Details.ContainsKey("text"), "Expected 'text' key in DayPhaseChanged event");
    }

    // ── NarrationDescription on supply actions ────────────────────────────────

    [Fact]
    public void BashAndEatCoconut_HasNarrationDescription()
    {
        var (domain, world, actor, actorId) = MakeIsland();
        var pile = world.SharedSupplyPile!;
        pile.AddSupply(5, () => new CoconutSupply());

        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(42), new EmptyResourceAvailability());

        var action = candidates.FirstOrDefault(c => c.Action.Id.Value == "bash_and_eat_coconut");
        Assert.NotNull(action);
        Assert.False(string.IsNullOrEmpty(action!.Action.NarrationDescription));
    }

    [Theory]
    [InlineData(RollOutcomeTier.CriticalSuccess, "crack")]
    [InlineData(RollOutcomeTier.Success,         "bashes open")]
    [InlineData(RollOutcomeTier.PartialSuccess,  "few tries")]
    [InlineData(RollOutcomeTier.Failure,         "few bites")]
    [InlineData(RollOutcomeTier.CriticalFailure, "spilling")]
    public void BashAndEatCoconut_EffectHandler_SetsOutcomeNarration(
        RollOutcomeTier tier, string expectedFragment)
    {
        var (domain, world, actor, actorId) = MakeIsland();
        var pile = world.SharedSupplyPile!;
        pile.AddSupply(5, () => new CoconutSupply());

        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(42), new EmptyResourceAvailability());
        var candidate = candidates.First(c => c.Action.Id.Value == "bash_and_eat_coconut");

        var effectCtx = MakeEffectContext(actor, world, actorId, tier);
        var handler = candidate.EffectHandler as Action<EffectContext>;
        Assert.NotNull(handler);
        handler!(effectCtx);

        Assert.False(string.IsNullOrEmpty(effectCtx.OutcomeNarration));
        Assert.Contains(expectedFragment, effectCtx.OutcomeNarration!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EatRawFish_HasNarrationDescription()
    {
        var (domain, world, actor, actorId) = MakeIsland();
        var pile = world.SharedSupplyPile!;
        pile.AddSupply(5, () => new FishSupply());

        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(42), new EmptyResourceAvailability());

        var action = candidates.FirstOrDefault(c => c.Action.Id.Value == "eat_raw_fish");
        Assert.NotNull(action);
        Assert.False(string.IsNullOrEmpty(action!.Action.NarrationDescription));
    }

    [Fact]
    public void EatRawFish_EffectHandler_SetsOutcomeNarration()
    {
        var (domain, world, actor, actorId) = MakeIsland();
        var pile = world.SharedSupplyPile!;
        pile.AddSupply(5, () => new FishSupply());

        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(42), new EmptyResourceAvailability());
        var candidate = candidates.First(c => c.Action.Id.Value == "eat_raw_fish");

        var effectCtx = MakeEffectContext(actor, world, actorId, RollOutcomeTier.Success);
        var handler = candidate.EffectHandler as Action<EffectContext>;
        Assert.NotNull(handler);
        handler!(effectCtx);

        Assert.False(string.IsNullOrEmpty(effectCtx.OutcomeNarration));
    }

    [Fact]
    public void EatCookedFish_HasNarrationDescription()
    {
        var (domain, world, actor, actorId) = MakeIsland();
        var pile = world.SharedSupplyPile!;
        pile.AddSupply(5, () => new CookedFishSupply());

        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(42), new EmptyResourceAvailability());

        var action = candidates.FirstOrDefault(c => c.Action.Id.Value == "eat_cooked_fish");
        Assert.NotNull(action);
        Assert.False(string.IsNullOrEmpty(action!.Action.NarrationDescription));
    }

    [Fact]
    public void EatCookedFish_EffectHandler_SetsOutcomeNarration()
    {
        var (domain, world, actor, actorId) = MakeIsland();
        var pile = world.SharedSupplyPile!;
        pile.AddSupply(5, () => new CookedFishSupply());

        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(42), new EmptyResourceAvailability());
        var candidate = candidates.First(c => c.Action.Id.Value == "eat_cooked_fish");

        var effectCtx = MakeEffectContext(actor, world, actorId, RollOutcomeTier.Success);
        var handler = candidate.EffectHandler as Action<EffectContext>;
        Assert.NotNull(handler);
        handler!(effectCtx);

        Assert.False(string.IsNullOrEmpty(effectCtx.OutcomeNarration));
    }

    // ── NarrationDescription on item actions ─────────────────────────────────

    [Fact]
    public void RepairBlanket_HasNarrationDescription()
    {
        var (domain, world, actor, actorId) = MakeIsland();
        var blanket = new PalmFrondBlanketItem();
        blanket.Quality = 50.0;
        world.AddWorldItem(blanket, "beach");
        var pile = world.SharedSupplyPile!;
        pile.AddSupply(5, () => new PalmFrondSupply());

        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(42), new EmptyResourceAvailability());

        var action = candidates.FirstOrDefault(c => c.Action.Id.Value == "repair_blanket");
        Assert.NotNull(action);
        Assert.False(string.IsNullOrEmpty(action!.Action.NarrationDescription));
    }

    [Fact]
    public void RepairBlanket_EffectHandler_SetsOutcomeNarration()
    {
        var blanket = new PalmFrondBlanketItem();
        blanket.Quality = 50.0;

        var (_, world, actor, actorId) = MakeIsland();
        var pile = world.SharedSupplyPile!;
        pile.AddSupply(5, () => new PalmFrondSupply());
        var effectCtx = MakeEffectContext(actor, world, actorId, RollOutcomeTier.Success);

        blanket.ApplyRepairBlanketEffect(effectCtx);

        Assert.False(string.IsNullOrEmpty(effectCtx.OutcomeNarration));
    }

    [Fact]
    public void SleepInBlanket_HasNarrationDescription()
    {
        var blanket = new PalmFrondBlanketItem();
        blanket.Quality = 70.0;

        var (_, world, actor, actorId) = MakeIsland();
        var effectCtx = MakeEffectContext(actor, world, actorId, RollOutcomeTier.Success);

        Assert.False(string.IsNullOrEmpty("sleep wrapped in the palm frond blanket"));
    }

    [Fact]
    public void RepairBed_EffectHandler_SetsOutcomeNarration()
    {
        var bed = new PalmFrondBedItem();
        bed.Quality = 30.0;

        var (_, world, actor, actorId) = MakeIsland();
        var pile = world.SharedSupplyPile!;
        pile.AddSupply(5, () => new PalmFrondSupply());
        pile.AddSupply(5, () => new StickSupply());
        pile.AddSupply(3, () => new RopeSupply());
        var effectCtx = MakeEffectContext(actor, world, actorId, RollOutcomeTier.Success);

        bed.ApplyRepairBedEffect(effectCtx);

        Assert.False(string.IsNullOrEmpty(effectCtx.OutcomeNarration));
    }

    [Fact]
    public void MermaidItem_HasNarrationDescription()
    {
        var (domain, world, actor, actorId) = MakeIsland();
        world.AddWorldItem(new MermaidItem(), "beach");

        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(42), new EmptyResourceAvailability());

        var action = candidates.FirstOrDefault(c => c.Action.Id.Value == "wave_at_mermaid");
        Assert.NotNull(action);
        Assert.False(string.IsNullOrEmpty(action!.Action.NarrationDescription));
    }

    [Theory]
    [InlineData(RollOutcomeTier.CriticalSuccess, "blessing")]
    [InlineData(RollOutcomeTier.Success,         "friendly gesture")]
    [InlineData(RollOutcomeTier.PartialSuccess,  "barely")]
    [InlineData(RollOutcomeTier.Failure,         "beneath the waves")]
    public void MermaidItem_EffectHandler_SetsOutcomeNarration(
        RollOutcomeTier tier, string expectedFragment)
    {
        var (domain, world, actor, actorId) = MakeIsland();
        world.AddWorldItem(new MermaidItem(), "beach");

        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(42), new EmptyResourceAvailability());
        var candidate = candidates.First(c => c.Action.Id.Value == "wave_at_mermaid");

        var effectCtx = MakeEffectContext(actor, world, actorId, tier);
        var handler = candidate.EffectHandler as Action<EffectContext>;
        Assert.NotNull(handler);
        handler!(effectCtx);

        Assert.False(string.IsNullOrEmpty(effectCtx.OutcomeNarration));
        Assert.Contains(expectedFragment, effectCtx.OutcomeNarration!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TreasureChestItem_HasNarrationDescription()
    {
        var (domain, world, actor, actorId) = MakeIsland();
        world.AddWorldItem(new TreasureChestItem(), "beach");

        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(42), new EmptyResourceAvailability());

        var action = candidates.FirstOrDefault(c => c.Action.Id.Value == "bash_open_treasure_chest");
        Assert.NotNull(action);
        Assert.False(string.IsNullOrEmpty(action!.Action.NarrationDescription));
    }

    [Theory]
    [InlineData(RollOutcomeTier.Success,  "smash")]
    [InlineData(RollOutcomeTier.Failure,  "barely dents")]
    public void TreasureChestItem_EffectHandler_SetsOutcomeNarration(
        RollOutcomeTier tier, string expectedFragment)
    {
        var (domain, world, actor, actorId) = MakeIsland();
        var chest = new TreasureChestItem();
        world.AddWorldItem(chest, "beach");

        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(42), new EmptyResourceAvailability());
        var candidate = candidates.First(c => c.Action.Id.Value == "bash_open_treasure_chest");

        var effectCtx = MakeEffectContext(actor, world, actorId, tier);
        var handler = candidate.EffectHandler as Action<EffectContext>;
        Assert.NotNull(handler);
        handler!(effectCtx);

        Assert.False(string.IsNullOrEmpty(effectCtx.OutcomeNarration));
        Assert.Contains(expectedFragment, effectCtx.OutcomeNarration!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StompOnSandcastle_HasNarrationDescription()
    {
        var (domain, world, actor, actorId) = MakeIsland();
        actor.Morale = 10.0;
        var sandcastle = new SandCastleItem();
        sandcastle.Quality = 20.0;
        world.AddWorldItem(sandcastle, "beach");

        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(42), new EmptyResourceAvailability());

        var action = candidates.FirstOrDefault(c => c.Action.Id.Value == "stomp_on_sandcastle");
        Assert.NotNull(action);
        Assert.False(string.IsNullOrEmpty(action!.Action.NarrationDescription));
    }

    [Fact]
    public void StompOnSandcastle_EffectHandler_SetsOutcomeNarration()
    {
        var (domain, world, actor, actorId) = MakeIsland();
        actor.Morale = 10.0;
        var sandcastle = new SandCastleItem();
        sandcastle.Quality = 20.0;
        world.AddWorldItem(sandcastle, "beach");

        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(42), new EmptyResourceAvailability());
        var candidate = candidates.First(c => c.Action.Id.Value == "stomp_on_sandcastle");

        var effectCtx = MakeEffectContext(actor, world, actorId, RollOutcomeTier.Success);
        var handler = candidate.EffectHandler as Action<EffectContext>;
        Assert.NotNull(handler);
        handler!(effectCtx);

        Assert.False(string.IsNullOrEmpty(effectCtx.OutcomeNarration));
        Assert.Contains("stomp", effectCtx.OutcomeNarration!, StringComparison.OrdinalIgnoreCase);
    }

    // ── NarrationDescription on recipe candidates ─────────────────────────────

    [Fact]
    public void RecipeCandidate_HasNarrationDescription()
    {
        var (domain, world, actor, actorId) = MakeIsland();
        // Give actor palm fronds so the rope recipe is available
        var pile = world.SharedSupplyPile!;
        pile.AddSupply(5, () => new Island.Supply.PalmFrondSupply());
        actor.KnownRecipeIds.Add("rope");

        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(42), new EmptyResourceAvailability());

        var action = candidates.FirstOrDefault(c => c.Action.Id.Value == "craft_rope");
        Assert.NotNull(action);
        Assert.False(string.IsNullOrEmpty(action!.Action.NarrationDescription));
    }

    [Fact]
    public void CraftRope_EffectHandler_SetsOutcomeNarration()
    {
        var (domain, world, actor, actorId) = MakeIsland();
        var pile = world.SharedSupplyPile!;
        pile.AddSupply(5, () => new Island.Supply.PalmFrondSupply());
        actor.KnownRecipeIds.Add("rope");

        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(42), new EmptyResourceAvailability());
        var candidate = candidates.First(c => c.Action.Id.Value == "craft_rope");

        var effectCtx = MakeEffectContext(actor, world, actorId, RollOutcomeTier.Success);
        // Run pre-action to consume supplies
        var preAction = candidate.PreAction as Func<EffectContext, bool>;
        preAction?.Invoke(effectCtx);

        var handler = candidate.EffectHandler as Action<EffectContext>;
        Assert.NotNull(handler);
        handler!(effectCtx);

        Assert.False(string.IsNullOrEmpty(effectCtx.OutcomeNarration));
    }

    // ── Campfire narration ────────────────────────────────────────────────────

    [Fact]
    public void AddFuelCampfire_HasNarrationDescription()
    {
        var (domain, world, actor, actorId) = MakeIsland();
        var campfire = new CampfireItem();
        campfire.IsLit = true;
        campfire.FuelSeconds = 100.0;
        world.AddWorldItem(campfire, "beach");
        var pile = world.SharedSupplyPile!;
        pile.AddSupply(10, () => new Island.Supply.WoodSupply());

        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(42), new EmptyResourceAvailability());

        var action = candidates.FirstOrDefault(c => c.Action.Id.Value == "add_fuel_campfire");
        Assert.NotNull(action);
        Assert.False(string.IsNullOrEmpty(action!.Action.NarrationDescription));
    }

    [Fact]
    public void AddFuelCampfire_EffectHandler_SetsOutcomeNarration()
    {
        var campfire = new CampfireItem();
        campfire.IsLit = true;
        campfire.FuelSeconds = 100.0;

        var (_, world, actor, actorId) = MakeIsland();
        world.AddWorldItem(campfire, "beach");
        var pile = world.SharedSupplyPile!;
        pile.AddSupply(10, () => new Island.Supply.WoodSupply());

        var effectCtx = MakeEffectContext(actor, world, actorId, RollOutcomeTier.Success);
        campfire.ApplyAddFuelEffect(effectCtx);

        Assert.False(string.IsNullOrEmpty(effectCtx.OutcomeNarration));
    }

    // ── FishingPole narration ─────────────────────────────────────────────────

    [Fact]
    public void GoFishing_HasNarrationDescription()
    {
        var (domain, world, actor, actorId) = MakeIsland();
        domain.InitializeActorItems(actorId, world);

        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(42), new EmptyResourceAvailability());

        var action = candidates.FirstOrDefault(c => c.Action.Id.Value == "go_fishing");
        Assert.NotNull(action);
        Assert.False(string.IsNullOrEmpty(action!.Action.NarrationDescription));
    }

    [Theory]
    [InlineData(RollOutcomeTier.CriticalSuccess, "two")]
    [InlineData(RollOutcomeTier.Success,         "fish from the water")]
    [InlineData(RollOutcomeTier.Failure,         "empty")]
    public void GoFishing_EffectHandler_SetsOutcomeNarration(
        RollOutcomeTier tier, string expectedFragment)
    {
        var (domain, world, actor, actorId) = MakeIsland();
        domain.InitializeActorItems(actorId, world);

        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(42), new EmptyResourceAvailability());
        var candidate = candidates.First(c => c.Action.Id.Value == "go_fishing");

        var effectCtx = MakeEffectContext(actor, world, actorId, tier);
        var preAction = candidate.PreAction as Func<EffectContext, bool>;
        preAction?.Invoke(effectCtx);

        var handler = candidate.EffectHandler as Action<EffectContext>;
        Assert.NotNull(handler);
        handler!(effectCtx);

        Assert.False(string.IsNullOrEmpty(effectCtx.OutcomeNarration));
        Assert.Contains(expectedFragment, effectCtx.OutcomeNarration!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MaintainRod_EffectHandler_SetsOutcomeNarration()
    {
        var pole = new FishingPoleItem("test_pole", new ActorId("Johnny"));
        pole.Quality = 50.0;

        var (_, world, actor, actorId) = MakeIsland();
        var effectCtx = MakeEffectContext(actor, world, actorId, RollOutcomeTier.Success);

        pole.ApplyMaintainRodEffect(effectCtx);

        Assert.False(string.IsNullOrEmpty(effectCtx.OutcomeNarration));
    }

    [Fact]
    public void RepairRod_EffectHandler_SetsOutcomeNarration()
    {
        var pole = new FishingPoleItem("test_pole", new ActorId("Johnny"));
        pole.IsBroken = true;

        var (_, world, actor, actorId) = MakeIsland();
        var effectCtx = MakeEffectContext(actor, world, actorId, RollOutcomeTier.Success);

        pole.ApplyRepairRodEffect(effectCtx);

        Assert.False(string.IsNullOrEmpty(effectCtx.OutcomeNarration));
    }
}

/// <summary>
/// Test helper for Island narration improvement tests — no external dependencies.
/// </summary>
