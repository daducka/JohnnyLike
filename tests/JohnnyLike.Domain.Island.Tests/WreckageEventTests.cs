using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Events;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Island.Loot;
using JohnnyLike.Domain.Island.Supply;

namespace JohnnyLike.Domain.Island.Tests;

/// <summary>
/// Tests for the progressive WreckageEvent script and the LootItem system.
/// Covers chapter triggering, loot item lifecycle, supply drops, serialization,
/// and concurrency guards.
/// </summary>
public class WreckageEventTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// A fixed <see cref="Random"/> replacement that always returns the same double.
    /// Used to control the trigger-chance roll in <see cref="WorldEventScript.TryTick"/>.
    /// </summary>
    private sealed class FixedRandom : Random
    {
        private readonly double _value;
        public FixedRandom(double value) => _value = value;
        public override double NextDouble() => _value;
    }

    /// <summary>
    /// An <see cref="IResourceAvailability"/> that reserves a resource on the first
    /// <see cref="TryReserve"/> call and returns it as reserved thereafter.
    /// </summary>
    private sealed class SingleUseResourceAvailability : IResourceAvailability
    {
        private readonly HashSet<string> _reserved = new();
        public bool IsReserved(ResourceId resourceId) => _reserved.Contains(resourceId.Value);
        public bool TryReserve(ResourceId resourceId, string utilityId, long until)
        {
            if (_reserved.Contains(resourceId.Value))
                return false;
            _reserved.Add(resourceId.Value);
            return true;
        }
        public void Release(ResourceId resourceId) => _reserved.Remove(resourceId.Value);
    }

    private static IslandWorldState MakeWorldWithCalendarAndWeather(int dayCount = 0,
        PrecipitationBand precipitation = PrecipitationBand.Clear)
    {
        var world = new IslandWorldState();
        var calendar = new CalendarItem("calendar") { DayCount = dayCount };
        world.AddWorldItem(calendar, "beach");
        var weather = new WeatherItem("weather") { Precipitation = precipitation };
        world.AddWorldItem(weather, "beach");
        var supplies = new SupplyPile("shared_supplies", "shared");
        world.AddWorldItem(supplies, "beach");
        return world;
    }

    private static (IslandDomainPack domain, HumanActorState actor, ActorId actorId)
        MakeActor(IslandWorldState world)
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actor = (HumanActorState)domain.CreateActorState(actorId);
        return (domain, actor, actorId);
    }

    private static void InvokeLootEffectHandler(
        IslandDomainPack domain,
        ActorId actorId,
        HumanActorState actor,
        IslandWorldState world,
        ActionCandidate candidate)
    {
        var outcome = new ActionOutcome(
            candidate.Action.Id,
            ActionOutcomeType.Success,
            Duration.FromTicks(1L),
            new Dictionary<string, object>()
        );
        domain.ApplyActionEffects(
            actorId, outcome, actor, world,
            new RandomRngStream(new Random(42)),
            new EmptyResourceAvailability(),
            candidate.EffectHandler);
    }

    // ── Chapter trigger tests ─────────────────────────────────────────────────

    [Fact]
    public void Chapter1_DoesNotTrigger_BeforeDay3()
    {
        var world = MakeWorldWithCalendarAndWeather(dayCount: 0);
        var progress = new WorldEventProgress();
        var script = new WreckageEventScript();

        // Use rng that always passes the chance roll
        var alwaysPass = new FixedRandom(0.0);

        for (var tick = 0L; tick < 600L * 10; tick += 600L)
            script.TryTick(world, progress, tick, alwaysPass);

        Assert.False(progress.HasTriggered("wreckage_chapter_1"));
        Assert.Empty(world.WorldItems.OfType<LootItem>());
    }

    [Fact]
    public void Chapter1_Triggers_WhenDay3AndChancePassses()
    {
        var world = MakeWorldWithCalendarAndWeather(dayCount: 3);
        var progress = new WorldEventProgress();
        var script = new WreckageEventScript();

        var alwaysPass = new FixedRandom(0.0); // 0.0 < TriggerChancePerCheck (0.005) → triggers

        script.TryTick(world, progress, 600L, alwaysPass);

        Assert.True(progress.HasTriggered("wreckage_chapter_1"));
    }

    [Fact]
    public void Chapter1_DoesNotTrigger_WhenChanceFails()
    {
        var world = MakeWorldWithCalendarAndWeather(dayCount: 3);
        var progress = new WorldEventProgress();
        var script = new WreckageEventScript();

        var alwaysFail = new FixedRandom(1.0); // 1.0 >= TriggerChancePerCheck → never triggers

        for (var tick = 600L; tick <= 600L * 10; tick += 600L)
            script.TryTick(world, progress, tick, alwaysFail);

        Assert.False(progress.HasTriggered("wreckage_chapter_1"));
    }

    [Fact]
    public void Chapter1_DoesNotRecheck_BeforeInterval()
    {
        var world = MakeWorldWithCalendarAndWeather(dayCount: 3);
        var progress = new WorldEventProgress();
        var script = new WreckageEventScript();

        // First call at tick 600 marks the chapter as checked.
        var alwaysFail = new FixedRandom(1.0);
        script.TryTick(world, progress, 600L, alwaysFail);
        Assert.Equal(600L, progress.GetLastCheckedTick("wreckage_chapter_1"));

        // Call at tick 601 — interval (600) not yet elapsed → should not re-check.
        var alwaysPass = new FixedRandom(0.0);
        script.TryTick(world, progress, 601L, alwaysPass);
        Assert.False(progress.HasTriggered("wreckage_chapter_1"));
    }

    // ── Chapter 1 spawns LootItem, not direct supplies ──────────────────────

    [Fact]
    public void Chapter1_Triggers_SpawnsLootItem_NotDirectSupplies()
    {
        var world = MakeWorldWithCalendarAndWeather(dayCount: 3);
        var progress = new WorldEventProgress();
        var script = new WreckageEventScript();

        script.TryTick(world, progress, 600L, new FixedRandom(0.0));

        // A LootItem should be present
        var loot = world.WorldItems.OfType<LootItem>().FirstOrDefault();
        Assert.NotNull(loot);
        Assert.Equal(LootKind.WreckageMetalScrap, loot.Kind);
        Assert.False(loot.IsConsumed);

        // The shared supply pile should NOT yet contain metal scraps
        var pile = world.SharedSupplyPile;
        Assert.NotNull(pile);
        var metalScrap = pile.GetSupply<MetalScrapSupply>();
        Assert.Null(metalScrap);
    }

    [Fact]
    public void LootItem_SpawnedInBeachRoom()
    {
        var world = MakeWorldWithCalendarAndWeather(dayCount: 3);
        var progress = new WorldEventProgress();
        var script = new WreckageEventScript();

        script.TryTick(world, progress, 600L, new FixedRandom(0.0));

        var loot = world.WorldItems.OfType<LootItem>().First();
        var roomId = world.GetItemRoomId(loot.Id);
        Assert.Equal("beach", roomId);
    }

    // ── Metal scrap only appears after investigation ──────────────────────────

    [Fact]
    public void MetalScrap_DoesNotAppear_UntilActorInvestigates()
    {
        var world = MakeWorldWithCalendarAndWeather(dayCount: 3);
        var progress = new WorldEventProgress();
        var script = new WreckageEventScript();
        script.TryTick(world, progress, 600L, new FixedRandom(0.0));

        var pile = world.SharedSupplyPile!;
        Assert.Null(pile.GetSupply<MetalScrapSupply>());
    }

    [Fact]
    public void InvestigatingLoot_AddsMetalScrapToSharedPile()
    {
        var world = MakeWorldWithCalendarAndWeather(dayCount: 3);
        var progress = new WorldEventProgress();
        var script = new WreckageEventScript();
        script.TryTick(world, progress, 600L, new FixedRandom(0.0));

        var (domain, actor, actorId) = MakeActor(world);

        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L,
            new Random(42), new EmptyResourceAvailability());
        var investigateCandidate = candidates.FirstOrDefault(c =>
            c.Action.Id.Value == "investigate_wreckage");

        Assert.NotNull(investigateCandidate);

        InvokeLootEffectHandler(domain, actorId, actor, world, investigateCandidate);

        var pile = world.SharedSupplyPile!;
        var metalScrap = pile.GetSupply<MetalScrapSupply>();
        Assert.NotNull(metalScrap);
        Assert.Equal(12.0, metalScrap.Quantity);
    }

    // ── Loot removal after consumption ───────────────────────────────────────

    [Fact]
    public void LootItem_RemovedFromWorld_AfterConsumption()
    {
        var world = MakeWorldWithCalendarAndWeather(dayCount: 3);
        var progress = new WorldEventProgress();
        var script = new WreckageEventScript();
        script.TryTick(world, progress, 600L, new FixedRandom(0.0));

        var (domain, actor, actorId) = MakeActor(world);
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L,
            new Random(42), new EmptyResourceAvailability());
        var candidate = candidates.First(c => c.Action.Id.Value == "investigate_wreckage");

        InvokeLootEffectHandler(domain, actorId, actor, world, candidate);

        Assert.Empty(world.WorldItems.OfType<LootItem>());
    }

    [Fact]
    public void LootItem_RemovedFromRoomMembership_AfterConsumption()
    {
        var world = MakeWorldWithCalendarAndWeather(dayCount: 3);
        var progress = new WorldEventProgress();
        var script = new WreckageEventScript();
        script.TryTick(world, progress, 600L, new FixedRandom(0.0));

        var lootId = world.WorldItems.OfType<LootItem>().First().Id;

        var (domain, actor, actorId) = MakeActor(world);
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L,
            new Random(42), new EmptyResourceAvailability());
        var candidate = candidates.First(c => c.Action.Id.Value == "investigate_wreckage");

        InvokeLootEffectHandler(domain, actorId, actor, world, candidate);

        // Item must not appear in any room
        Assert.Null(world.GetItemRoomId(lootId));
    }

    // ── Consumed loot offers no further action ───────────────────────────────

    [Fact]
    public void ConsumedLoot_DoesNotOfferInvestigateAction()
    {
        var world = MakeWorldWithCalendarAndWeather(dayCount: 3);
        var loot = new LootItem("loot_test", LootKind.WreckageMetalScrap) { IsConsumed = true };
        world.AddWorldItem(loot, "beach");

        var (domain, actor, actorId) = MakeActor(world);
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L,
            new Random(42), new EmptyResourceAvailability());

        Assert.DoesNotContain(candidates, c => c.Action.Id.Value == "investigate_wreckage");
    }

    // ── Concurrency: two actors cannot consume the same loot ─────────────────

    [Fact]
    public void TwoActors_CannotConsumeSameLoot_Concurrently()
    {
        var world = MakeWorldWithCalendarAndWeather(dayCount: 3);
        var loot = new LootItem("loot_concurrent", LootKind.WreckageMetalScrap);
        world.AddWorldItem(loot, "beach");

        // Single resource availability shared between both actors
        var sharedResources = new SingleUseResourceAvailability();

        var domain = new IslandDomainPack();
        var actor1Id = new ActorId("Actor1");
        var actor2Id = new ActorId("Actor2");
        var actor1 = (HumanActorState)domain.CreateActorState(actor1Id);
        var actor2 = (HumanActorState)domain.CreateActorState(actor2Id);

        // First actor gets the candidate
        var candidates1 = domain.GenerateCandidates(actor1Id, actor1, world, 0L,
            new Random(1), sharedResources);
        var c1 = candidates1.FirstOrDefault(c => c.Action.Id.Value == "investigate_wreckage");
        Assert.NotNull(c1);

        // Simulate first actor pre-action (reserve the resource)
        var lootResource = new ResourceId($"loot:{loot.Id}");
        Assert.True(sharedResources.TryReserve(lootResource, "actor1", long.MaxValue));

        // Second actor generates candidates — resource is now reserved
        var candidates2 = domain.GenerateCandidates(actor2Id, actor2, world, 0L,
            new Random(2), sharedResources);
        Assert.DoesNotContain(candidates2, c => c.Action.Id.Value == "investigate_wreckage");
    }

    // ── Chapter 2 requirements ────────────────────────────────────────────────

    [Fact]
    public void Chapter2_DoesNotTrigger_IfChapter1NotTriggered()
    {
        var world = MakeWorldWithCalendarAndWeather(dayCount: 5);
        var progress = new WorldEventProgress();
        // Do NOT mark chapter 1 as triggered
        var script = new WreckageEventScript();

        // Advance past chapter 1 check by marking it checked already so chapter 2 check occurs
        progress.MarkChecked("wreckage_chapter_1", 600L);
        progress.MarkTriggered("wreckage_chapter_1", 600L); // trigger chapter 1

        // But now test chapter 2 without chapter 1 triggered...
        var freshProgress = new WorldEventProgress();
        // Fresh progress: chapter 1 NOT triggered
        // Make chapter 1 already checked so it's skipped, and chapter 2 can be evaluated
        freshProgress.MarkChecked("wreckage_chapter_1", 600L);
        // But don't mark chapter 1 as triggered

        var alwaysPass = new FixedRandom(0.0);
        script.TryTick(world, freshProgress, 1200L, alwaysPass);

        // Chapter 2 should not have triggered because chapter 1 is not triggered
        Assert.False(freshProgress.HasTriggered("wreckage_chapter_2"));
    }

    [Fact]
    public void Chapter2_RequiresChapter1Triggered()
    {
        // Verify requirement directly
        var world = MakeWorldWithCalendarAndWeather(dayCount: 5);
        var progress = new WorldEventProgress();

        var chapter2Req = new ChapterTriggeredRequirement { ChapterId = "wreckage_chapter_1" };

        Assert.False(chapter2Req.IsSatisfied(world, progress));

        progress.MarkTriggered("wreckage_chapter_1", 600L);

        Assert.True(chapter2Req.IsSatisfied(world, progress));
    }

    [Fact]
    public void Chapter2_Triggers_WhenDay5AndChapter1Triggered()
    {
        var world = MakeWorldWithCalendarAndWeather(dayCount: 5);
        var progress = new WorldEventProgress();
        progress.MarkTriggered("wreckage_chapter_1", 600L);
        progress.MarkChecked("wreckage_chapter_1", 600L);

        var script = new WreckageEventScript();
        script.TryTick(world, progress, 1200L, new FixedRandom(0.0));

        Assert.True(progress.HasTriggered("wreckage_chapter_2"));
    }

    // ── Chapter 3 requires rain ───────────────────────────────────────────────

    [Fact]
    public void Chapter3_RequiresRain()
    {
        var world = MakeWorldWithCalendarAndWeather(dayCount: 7, precipitation: PrecipitationBand.Clear);
        var progress = new WorldEventProgress();
        progress.MarkTriggered("wreckage_chapter_1", 600L);
        progress.MarkChecked("wreckage_chapter_1", 600L);
        progress.MarkTriggered("wreckage_chapter_2", 1200L);
        progress.MarkChecked("wreckage_chapter_2", 1200L);

        var script = new WreckageEventScript();
        script.TryTick(world, progress, 1800L, new FixedRandom(0.0));

        // Chapter 3 should NOT trigger when it's clear
        Assert.False(progress.HasTriggered("wreckage_chapter_3"));
    }

    [Fact]
    public void Chapter3_Triggers_WhenRainyAndChapter2Triggered()
    {
        var world = MakeWorldWithCalendarAndWeather(dayCount: 7, precipitation: PrecipitationBand.Rainy);
        var progress = new WorldEventProgress();
        progress.MarkTriggered("wreckage_chapter_1", 600L);
        progress.MarkChecked("wreckage_chapter_1", 600L);
        progress.MarkTriggered("wreckage_chapter_2", 1200L);
        progress.MarkChecked("wreckage_chapter_2", 1200L);

        var script = new WreckageEventScript();
        script.TryTick(world, progress, 1800L, new FixedRandom(0.0));

        Assert.True(progress.HasTriggered("wreckage_chapter_3"));
    }

    // ── Serialization ─────────────────────────────────────────────────────────

    [Fact]
    public void WorldEventProgress_SurvivesSerializeDeserialize()
    {
        var world = MakeWorldWithCalendarAndWeather(dayCount: 5);
        var progress = world.EventProgress;

        progress.MarkTriggered("wreckage_chapter_1", 1000L);
        progress.MarkChecked("wreckage_chapter_2", 1200L);

        var json = world.Serialize();

        var newWorld = new IslandWorldState();
        newWorld.Deserialize(json);

        Assert.True(newWorld.EventProgress.HasTriggered("wreckage_chapter_1"));
        Assert.False(newWorld.EventProgress.HasTriggered("wreckage_chapter_2"));
        Assert.Equal(1200L, newWorld.EventProgress.GetLastCheckedTick("wreckage_chapter_2"));
    }

    [Fact]
    public void UnconsumedLootItem_SurvivesSerializeDeserialize()
    {
        var world = MakeWorldWithCalendarAndWeather(dayCount: 3);
        var loot = new LootItem("loot_wreckage_day3", LootKind.WreckageMetalScrap);
        world.AddWorldItem(loot, "beach");

        var json = world.Serialize();

        var newWorld = new IslandWorldState();
        newWorld.Deserialize(json);

        var restoredLoot = newWorld.WorldItems.OfType<LootItem>().FirstOrDefault();
        Assert.NotNull(restoredLoot);
        Assert.Equal("loot_wreckage_day3", restoredLoot.Id);
        Assert.Equal(LootKind.WreckageMetalScrap, restoredLoot.Kind);
        Assert.False(restoredLoot.IsConsumed);
    }

    [Fact]
    public void ConsumedLootItem_SurvivesSerializeDeserialize()
    {
        var world = MakeWorldWithCalendarAndWeather(dayCount: 3);
        var loot = new LootItem("loot_wreckage_day3", LootKind.WreckageMetalScrap) { IsConsumed = true };
        world.AddWorldItem(loot, "beach");

        var json = world.Serialize();

        var newWorld = new IslandWorldState();
        newWorld.Deserialize(json);

        var restoredLoot = newWorld.WorldItems.OfType<LootItem>().FirstOrDefault();
        Assert.NotNull(restoredLoot);
        Assert.True(restoredLoot.IsConsumed);
    }

    // ── Chapter 2 loot drops world item ──────────────────────────────────────

    [Fact]
    public void Chapter2Loot_Investigation_SpawnsBrokenRadio()
    {
        var world = MakeWorldWithCalendarAndWeather(dayCount: 5);
        var loot = new LootItem("loot_wreckage_day5", LootKind.WreckageRadioCache);
        world.AddWorldItem(loot, "beach");

        var (domain, actor, actorId) = MakeActor(world);
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L,
            new Random(42), new EmptyResourceAvailability());
        var candidate = candidates.FirstOrDefault(c => c.Action.Id.Value == "investigate_wreckage");
        Assert.NotNull(candidate);

        InvokeLootEffectHandler(domain, actorId, actor, world, candidate);

        // BrokenRadioItem should have been spawned
        var radio = world.WorldItems.OfType<BrokenRadioItem>().FirstOrDefault();
        Assert.NotNull(radio);
        Assert.Equal("broken_radio", radio.Id);
    }

    [Fact]
    public void Chapter2Loot_Investigation_AddsMetalScrap()
    {
        var world = MakeWorldWithCalendarAndWeather(dayCount: 5);
        var loot = new LootItem("loot_wreckage_day5", LootKind.WreckageRadioCache);
        world.AddWorldItem(loot, "beach");

        var (domain, actor, actorId) = MakeActor(world);
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L,
            new Random(42), new EmptyResourceAvailability());
        var candidate = candidates.First(c => c.Action.Id.Value == "investigate_wreckage");

        InvokeLootEffectHandler(domain, actorId, actor, world, candidate);

        var pile = world.SharedSupplyPile!;
        var metalScrap = pile.GetSupply<MetalScrapSupply>();
        Assert.NotNull(metalScrap);
        Assert.Equal(8.0, metalScrap.Quantity);
    }

    // ── MinDayRequirement ────────────────────────────────────────────────────

    [Fact]
    public void MinDayRequirement_SatisfiedOnOrAfterTargetDay()
    {
        var world = MakeWorldWithCalendarAndWeather(dayCount: 3);
        var progress = new WorldEventProgress();
        var req = new MinDayRequirement { MinDay = 3 };

        Assert.True(req.IsSatisfied(world, progress));

        world.GetItem<CalendarItem>("calendar")!.DayCount = 2;
        Assert.False(req.IsSatisfied(world, progress));
    }

    // ── WeatherRequirement ───────────────────────────────────────────────────

    [Fact]
    public void WeatherRequirement_SatisfiedWhenPrecipitationMatches()
    {
        var world = MakeWorldWithCalendarAndWeather(precipitation: PrecipitationBand.Rainy);
        var progress = new WorldEventProgress();
        var req = new WeatherRequirement { Required = PrecipitationBand.Rainy };

        Assert.True(req.IsSatisfied(world, progress));

        world.GetItem<WeatherItem>("weather")!.Precipitation = PrecipitationBand.Clear;
        Assert.False(req.IsSatisfied(world, progress));
    }

    // ── ItemExistsRequirement ─────────────────────────────────────────────────

    [Fact]
    public void ItemExistsRequirement_SatisfiedWhenItemPresent()
    {
        var world = new IslandWorldState();
        var progress = new WorldEventProgress();
        var req = new ItemExistsRequirement { ItemId = "my_item" };

        Assert.False(req.IsSatisfied(world, progress));

        world.WorldItems.Add(new LootItem("my_item", LootKind.WreckageMetalScrap));
        Assert.True(req.IsSatisfied(world, progress));
    }
}
