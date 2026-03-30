using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Island.Vitality;

namespace JohnnyLike.Domain.Island.Tests;

/// <summary>
/// Tests for the downed state, death save mechanic, death transitions, and corpse spawning.
/// </summary>
public class DownedAndDeathTests
{
    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static IslandDomainPack Domain => new();

    private static (IslandActorState actor, IslandWorldState world) MakeActor(
        double health  = 100.0,
        double satiety = 100.0,
        double energy  = 100.0,
        double morale  = 80.0)
    {
        var domain    = Domain;
        var actorId   = new ActorId("TestActor");
        var actor     = (IslandActorState)domain.CreateActorState(actorId, new Dictionary<string, object>
        {
            ["satiety"] = satiety,
            ["energy"]  = energy,
            ["morale"]  = morale
        });
        actor.Health = health;
        var world = new IslandWorldState();
        return (actor, world);
    }

    private static void TickForSeconds(IslandActorState actor, IslandWorldState world, double seconds)
    {
        long ticks = (long)(seconds * EngineConstants.TickHz);
        var domain = Domain;
        domain.TickWorldState(world, new Dictionary<ActorId, ActorState> { [actor.Id] = actor }, ticks,
            new EmptyResourceAvailability());
    }

    private static List<ActionCandidate> GenerateCandidates(IslandActorState actor, IslandWorldState world)
    {
        var domain = Domain;
        domain.InitializeActorItems(actor.Id, world);
        if (!world.WorldItems.OfType<OceanItem>().Any())
            world.WorldItems.Add(new OceanItem("ocean"));
        return domain.GenerateCandidates(actor.Id, actor, world, 0L, new Random(42),
            new EmptyResourceAvailability());
    }

    /// <summary>
    /// Invokes an effect handler directly, simulating a successful action completion.
    /// </summary>
    private static void InvokeEffect(
        ActionCandidate candidate,
        IslandActorState actor,
        IslandWorldState world,
        Random? rng = null)
    {
        var domain = Domain;
        var outcome = new ActionOutcome(
            candidate.Action.Id,
            ActionOutcomeType.Success,
            Duration.FromTicks(100L),
            new Dictionary<string, object>());
        domain.ApplyActionEffects(
            actor.Id, outcome, actor, world,
            new RandomRngStream(rng ?? new Random(42)),
            new EmptyResourceAvailability(),
            candidate.EffectHandler);
    }

    // ─── AlivenessBuff death-save counters ───────────────────────────────────

    [Fact]
    public void AlivenessBuff_InitialDeathSaveCounters_AreZero()
    {
        var (actor, _) = MakeActor();
        var buff = actor.TryGetBuff<AlivenessBuff>()!;
        Assert.Equal(0, buff.DeathSaveSuccesses);
        Assert.Equal(0, buff.DeathSaveFailures);
    }

    [Fact]
    public void AlivenessBuff_Describe_IncludesCountsWhenDowned()
    {
        var (actor, _) = MakeActor();
        var buff = actor.TryGetBuff<AlivenessBuff>()!;
        buff.State = AlivenessState.Downed;
        buff.DeathSaveSuccesses = 1;
        buff.DeathSaveFailures  = 2;

        var description = buff.Describe(0);
        Assert.Contains("1/3", description);
        Assert.Contains("2/3", description);
    }

    [Fact]
    public void AlivenessBuff_Describe_DoesNotIncludeCountsWhenAlive()
    {
        var (actor, _) = MakeActor();
        var buff = actor.TryGetBuff<AlivenessBuff>()!;
        var description = buff.Describe(0);
        Assert.DoesNotContain("saves=", description);
    }

    // ─── Downed transition (health → 0) ──────────────────────────────────────

    [Fact]
    public void VitalityBuff_WhenHealthHitsZero_TransitionsToDownedState()
    {
        // Actor is critically starving with almost no health.
        var (actor, world) = MakeActor(health: 0.001, satiety: 0.0, energy: 100.0, morale: 80.0);

        TickForSeconds(actor, world, 60.0);   // 1 sim-minute of starvation damage

        var aliveness = actor.TryGetBuff<AlivenessBuff>()!;
        Assert.Equal(AlivenessState.Downed, aliveness.State);
        Assert.Equal(0.0, actor.Health);
    }

    [Fact]
    public void VitalityBuff_WhenAlreadyDowned_DoesNotDamageHealthFurther()
    {
        var (actor, world) = MakeActor(health: 0.0, satiety: 0.0, energy: 0.0, morale: 0.0);
        var aliveness = actor.TryGetBuff<AlivenessBuff>()!;
        aliveness.State = AlivenessState.Downed;

        TickForSeconds(actor, world, 3600.0); // 1 hour — health should stay at 0

        Assert.Equal(0.0, actor.Health);
    }

    [Fact]
    public void VitalityBuff_WhenAlreadyDead_DoesNotDamageHealth()
    {
        var (actor, world) = MakeActor(health: 0.0, satiety: 0.0, energy: 0.0, morale: 0.0);
        var aliveness = actor.TryGetBuff<AlivenessBuff>()!;
        aliveness.State = AlivenessState.Dead;

        TickForSeconds(actor, world, 3600.0); // 1 hour — health should stay at 0

        Assert.Equal(0.0, actor.Health);
    }

    [Fact]
    public void VitalityBuff_WhenDownedTransition_ResetsDeathSaveCounters()
    {
        var (actor, world) = MakeActor(health: 0.001, satiety: 0.0, energy: 100.0, morale: 80.0);
        var aliveness = actor.TryGetBuff<AlivenessBuff>()!;
        aliveness.DeathSaveSuccesses = 2;
        aliveness.DeathSaveFailures  = 1;

        TickForSeconds(actor, world, 60.0); // trigger the downed transition

        Assert.Equal(AlivenessState.Downed, aliveness.State);
        Assert.Equal(0, aliveness.DeathSaveSuccesses);
        Assert.Equal(0, aliveness.DeathSaveFailures);
    }

    // ─── Downed candidates ────────────────────────────────────────────────────

    [Fact]
    public void GenerateCandidates_DownedActor_HasDeathSaveCandidate()
    {
        var (actor, world) = MakeActor();
        var aliveness = actor.TryGetBuff<AlivenessBuff>()!;
        aliveness.State = AlivenessState.Downed;

        var candidates = GenerateCandidates(actor, world);
        Assert.Contains(candidates, c => c.Action.Id.Value == "death_save");
    }

    [Fact]
    public void GenerateCandidates_DownedActor_HasFlavorCandidates()
    {
        var (actor, world) = MakeActor();
        var aliveness = actor.TryGetBuff<AlivenessBuff>()!;
        aliveness.State = AlivenessState.Downed;

        var candidates = GenerateCandidates(actor, world);
        Assert.Contains(candidates, c => c.Action.Id.Value == "whimper");
        Assert.Contains(candidates, c => c.Action.Id.Value == "stare_blankly");
        Assert.Contains(candidates, c => c.Action.Id.Value == "crawl_weakly");
    }

    [Fact]
    public void GenerateCandidates_DownedActor_DoesNotHaveAliveOnlyCandidates()
    {
        var (actor, world) = MakeActor();
        var aliveness = actor.TryGetBuff<AlivenessBuff>()!;
        aliveness.State = AlivenessState.Downed;

        var candidates = GenerateCandidates(actor, world);

        // Standard alive-only actions should not appear
        Assert.DoesNotContain(candidates, c => c.Action.Id.Value == "idle");
        Assert.DoesNotContain(candidates, c => c.Action.Id.Value == "go_fishing");
        Assert.DoesNotContain(candidates, c => c.Action.Id.Value == "sleep_under_tree");
    }

    [Fact]
    public void GenerateCandidates_AliveActor_DoesNotHaveDownedCandidates()
    {
        var (actor, world) = MakeActor();
        // Actor is Alive by default
        var candidates = GenerateCandidates(actor, world);

        Assert.DoesNotContain(candidates, c => c.Action.Id.Value == "death_save");
        Assert.DoesNotContain(candidates, c => c.Action.Id.Value == "whimper");
        Assert.DoesNotContain(candidates, c => c.Action.Id.Value == "stare_blankly");
        Assert.DoesNotContain(candidates, c => c.Action.Id.Value == "crawl_weakly");
    }

    // ─── Death save candidate requirements ───────────────────────────────────

    [Fact]
    public void CandidateRequirements_DownedOnly_PassesForDownedActor()
    {
        var (actor, _) = MakeActor();
        actor.TryGetBuff<AlivenessBuff>()!.State = AlivenessState.Downed;
        Assert.True(CandidateRequirements.DownedOnly(actor));
    }

    [Fact]
    public void CandidateRequirements_DownedOnly_FailsForAliveActor()
    {
        var (actor, _) = MakeActor();
        Assert.False(CandidateRequirements.DownedOnly(actor));
    }

    [Fact]
    public void CandidateRequirements_DownedOnly_FailsForDeadActor()
    {
        var (actor, _) = MakeActor();
        actor.TryGetBuff<AlivenessBuff>()!.State = AlivenessState.Dead;
        Assert.False(CandidateRequirements.DownedOnly(actor));
    }

    [Fact]
    public void CandidateRequirements_DeadOnly_PassesForDeadActor()
    {
        var (actor, _) = MakeActor();
        actor.TryGetBuff<AlivenessBuff>()!.State = AlivenessState.Dead;
        Assert.True(CandidateRequirements.DeadOnly(actor));
    }

    [Fact]
    public void CandidateRequirements_DeadOnly_FailsForAliveActor()
    {
        var (actor, _) = MakeActor();
        Assert.False(CandidateRequirements.DeadOnly(actor));
    }

    // ─── Death save mechanic ──────────────────────────────────────────────────

    [Fact]
    public void DeathSave_SuccessfulRoll_IncrementsSuccessCount()
    {
        var (actor, world) = MakeActor();
        var aliveness = actor.TryGetBuff<AlivenessBuff>()!;
        aliveness.State = AlivenessState.Downed;

        var candidates = GenerateCandidates(actor, world);
        var deathSave  = candidates.First(c => c.Action.Id.Value == "death_save");

        // A roll of 0.9 is above 0.6 (success threshold).
        var rng = new FixedValueRng(0.9);
        InvokeEffect(deathSave, actor, world, null);

        // We can't guarantee 0.9 from Random(42) but we can check the counter increases
        // after running the effect if the save succeeded.
        // Use a seeded RNG that we know produces a success to make this deterministic:
        aliveness.DeathSaveSuccesses = 0;
        aliveness.DeathSaveFailures  = 0;
        var effectHandler = deathSave.EffectHandler as Action<EffectContext>;
        Assert.NotNull(effectHandler);

        var successCtx = new EffectContext
        {
            ActorId      = actor.Id,
            Outcome      = new ActionOutcome(deathSave.Action.Id, ActionOutcomeType.Success, Duration.FromTicks(100L), null),
            Actor        = actor,
            World        = world,
            Rng          = new FixedD20Rng(12),   // roll 12, DC 10, modifier 0 → Success
            Reservations = new EmptyResourceAvailability()
        };

        effectHandler(successCtx);

        Assert.Equal(1, aliveness.DeathSaveSuccesses);
        Assert.Equal(0, aliveness.DeathSaveFailures);
    }

    [Fact]
    public void DeathSave_FailingRoll_IncrementsFailureCount()
    {
        var (actor, world) = MakeActor();
        var aliveness = actor.TryGetBuff<AlivenessBuff>()!;
        aliveness.State = AlivenessState.Downed;

        var candidates     = GenerateCandidates(actor, world);
        var deathSave      = candidates.First(c => c.Action.Id.Value == "death_save");
        var effectHandler  = deathSave.EffectHandler as Action<EffectContext>;
        Assert.NotNull(effectHandler);

        var failCtx = new EffectContext
        {
            ActorId      = actor.Id,
            Outcome      = new ActionOutcome(deathSave.Action.Id, ActionOutcomeType.Success, Duration.FromTicks(100L), null),
            Actor        = actor,
            World        = world,
            Rng          = new FixedD20Rng(5),   // roll 5, DC 10, modifier 0 → Failure (+1 failure)
            Reservations = new EmptyResourceAvailability()
        };

        effectHandler(failCtx);

        Assert.Equal(0, aliveness.DeathSaveSuccesses);
        Assert.Equal(1, aliveness.DeathSaveFailures);
    }

    [Fact]
    public void DeathSave_NeutralRoll_DoesNotChangeCounters()
    {
        var (actor, world) = MakeActor();
        var aliveness = actor.TryGetBuff<AlivenessBuff>()!;
        aliveness.State = AlivenessState.Downed;

        var candidates    = GenerateCandidates(actor, world);
        var deathSave     = candidates.First(c => c.Action.Id.Value == "death_save");
        var effectHandler = deathSave.EffectHandler as Action<EffectContext>;
        Assert.NotNull(effectHandler);

        var neutralCtx = new EffectContext
        {
            ActorId      = actor.Id,
            Outcome      = new ActionOutcome(deathSave.Action.Id, ActionOutcomeType.Success, Duration.FromTicks(100L), null),
            Actor        = actor,
            World        = world,
            Rng          = new FixedD20Rng(9),   // roll 9, DC 10, modifier 0 → PartialSuccess (no change)
            Reservations = new EmptyResourceAvailability()
        };

        effectHandler(neutralCtx);

        Assert.Equal(0, aliveness.DeathSaveSuccesses);
        Assert.Equal(0, aliveness.DeathSaveFailures);
    }

    [Fact]
    public void DeathSave_ThreeSuccesses_RevivesActor()
    {
        var (actor, world) = MakeActor(health: 0.0);
        var aliveness = actor.TryGetBuff<AlivenessBuff>()!;
        aliveness.State              = AlivenessState.Downed;
        aliveness.DeathSaveSuccesses = 2;   // one more success triggers revive

        var candidates    = GenerateCandidates(actor, world);
        var deathSave     = candidates.First(c => c.Action.Id.Value == "death_save");
        var effectHandler = deathSave.EffectHandler as Action<EffectContext>;
        Assert.NotNull(effectHandler);

        var ctx = new EffectContext
        {
            ActorId      = actor.Id,
            Outcome      = new ActionOutcome(deathSave.Action.Id, ActionOutcomeType.Success, Duration.FromTicks(100L), null),
            Actor        = actor,
            World        = world,
            Rng          = new FixedD20Rng(12),   // roll 12, DC 10, modifier 0 → Success → 3rd success revives
            Reservations = new EmptyResourceAvailability()
        };

        effectHandler(ctx);

        Assert.Equal(AlivenessState.Alive, aliveness.State);
        Assert.Equal(IslandActorState.ReviveHealth, actor.Health);
        Assert.Equal(0, aliveness.DeathSaveSuccesses);
        Assert.Equal(0, aliveness.DeathSaveFailures);
    }

    [Fact]
    public void DeathSave_ThreeFailures_ActorDies()
    {
        var (actor, world) = MakeActor(health: 0.0);
        var aliveness = actor.TryGetBuff<AlivenessBuff>()!;
        aliveness.State             = AlivenessState.Downed;
        aliveness.DeathSaveFailures = 2;   // one more failure triggers death

        var candidates    = GenerateCandidates(actor, world);
        var deathSave     = candidates.First(c => c.Action.Id.Value == "death_save");
        var effectHandler = deathSave.EffectHandler as Action<EffectContext>;
        Assert.NotNull(effectHandler);

        var ctx = new EffectContext
        {
            ActorId      = actor.Id,
            Outcome      = new ActionOutcome(deathSave.Action.Id, ActionOutcomeType.Success, Duration.FromTicks(100L), null),
            Actor        = actor,
            World        = world,
            Rng          = new FixedValueRng(0.2),   // failure
            Reservations = new EmptyResourceAvailability()
        };

        effectHandler(ctx);

        Assert.Equal(AlivenessState.Dead, aliveness.State);
    }

    // ─── Corpse spawning ──────────────────────────────────────────────────────

    [Fact]
    public void DeathSave_ThreeFailures_SpawnsCorpseInWorld()
    {
        var (actor, world) = MakeActor(health: 0.0);
        var aliveness = actor.TryGetBuff<AlivenessBuff>()!;
        aliveness.State             = AlivenessState.Downed;
        aliveness.DeathSaveFailures = 2;

        var candidates    = GenerateCandidates(actor, world);
        var deathSave     = candidates.First(c => c.Action.Id.Value == "death_save");
        var effectHandler = deathSave.EffectHandler as Action<EffectContext>;
        Assert.NotNull(effectHandler);

        var ctx = new EffectContext
        {
            ActorId      = actor.Id,
            Outcome      = new ActionOutcome(deathSave.Action.Id, ActionOutcomeType.Success, Duration.FromTicks(100L), null),
            Actor        = actor,
            World        = world,
            Rng          = new FixedValueRng(0.2),   // failure
            Reservations = new EmptyResourceAvailability()
        };

        effectHandler(ctx);

        var corpse = world.WorldItems.OfType<CorpseItem>().FirstOrDefault();
        Assert.NotNull(corpse);
        Assert.Equal("TestActor", corpse.ActorName);
    }

    [Fact]
    public void DeathSave_ThreeFailures_CorpseHasFullQuality()
    {
        var (actor, world) = MakeActor(health: 0.0);
        var aliveness = actor.TryGetBuff<AlivenessBuff>()!;
        aliveness.State             = AlivenessState.Downed;
        aliveness.DeathSaveFailures = 2;

        var candidates    = GenerateCandidates(actor, world);
        var deathSave     = candidates.First(c => c.Action.Id.Value == "death_save");
        var effectHandler = deathSave.EffectHandler as Action<EffectContext>;
        Assert.NotNull(effectHandler);

        var ctx = new EffectContext
        {
            ActorId      = actor.Id,
            Outcome      = new ActionOutcome(deathSave.Action.Id, ActionOutcomeType.Success, Duration.FromTicks(100L), null),
            Actor        = actor,
            World        = world,
            Rng          = new FixedValueRng(0.2),
            Reservations = new EmptyResourceAvailability()
        };

        effectHandler(ctx);

        var corpse = world.WorldItems.OfType<CorpseItem>().Single();
        Assert.Equal(100.0, corpse.Quality);
    }

    // ─── Dead actor candidates ────────────────────────────────────────────────

    [Fact]
    public void GenerateCandidates_DeadActor_HasOnlyLieStillCandidate()
    {
        var (actor, world) = MakeActor(health: 0.0);
        actor.TryGetBuff<AlivenessBuff>()!.State = AlivenessState.Dead;

        var candidates = GenerateCandidates(actor, world);
        Assert.Single(candidates);
        Assert.Equal("lie_still", candidates[0].Action.Id.Value);
    }

    [Fact]
    public void GenerateCandidates_DeadActor_DoesNotHaveDeathSaveCandidate()
    {
        var (actor, world) = MakeActor(health: 0.0);
        actor.TryGetBuff<AlivenessBuff>()!.State = AlivenessState.Dead;

        var candidates = GenerateCandidates(actor, world);
        Assert.DoesNotContain(candidates, c => c.Action.Id.Value == "death_save");
    }

    // ─── CorpseItem decay ─────────────────────────────────────────────────────

    [Fact]
    public void CorpseItem_DecaysOver14SimDays()
    {
        var corpse = new CorpseItem("test_corpse");
        Assert.Equal(100.0, corpse.Quality);

        // After exactly 14 sim-days, quality should be ~0.
        var world    = new IslandWorldState();
        var totalSeconds = 14.0 * 86_400.0;
        var dtTicks      = (long)(totalSeconds * EngineConstants.TickHz);
        corpse.Tick(dtTicks, world);

        Assert.True(corpse.Quality < 0.01, $"Quality should be nearly 0 after 14 days, was {corpse.Quality}");
    }

    [Fact]
    public void CorpseItem_IsRegisteredInWorldItemTypeRegistry()
    {
        Assert.Contains("corpse", WorldItemTypeRegistry.RegisteredTypes);
        var corpse = WorldItemTypeRegistry.Create("corpse", "test_corpse");
        Assert.IsType<CorpseItem>(corpse);
    }

    [Fact]
    public void CorpseItem_SerializesAndDeserializesActorName()
    {
        var original = new CorpseItem("c1") { ActorName = "Johnny" };
        var dict     = original.SerializeToDict();

        Assert.Equal("corpse", dict["Type"]);
        Assert.Equal("Johnny", dict["ActorName"]);

        var restored = new CorpseItem("c1");
        var jsonDict = dict.ToDictionary(
            kvp => kvp.Key,
            kvp => System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                System.Text.Json.JsonSerializer.Serialize(kvp.Value)));
        restored.DeserializeFromDict(jsonDict);

        Assert.Equal("Johnny", restored.ActorName);
    }
}

/// <summary>
/// An <see cref="IRngStream"/> that always returns a fixed value, used to make
/// death-save roll tests fully deterministic.
/// </summary>
internal sealed class FixedValueRng : IRngStream
{
    private readonly double _value;
    public FixedValueRng(double value) => _value = value;

    public double NextDouble() => _value;
    public int    Next(int min, int max) => min;
    public int    Next(int max) => 0;
}

/// <summary>
/// An <see cref="IRngStream"/> that returns a fixed d20 result from <see cref="Next(int, int)"/>,
/// suitable for tests that drive skill-check resolution through <c>SkillCheckResolver</c>.
/// </summary>
internal sealed class FixedD20Rng : IRngStream
{
    private readonly int _roll;
    public FixedD20Rng(int roll) => _roll = roll;

    public double NextDouble() => 0.5;
    public int    Next(int min, int max) => _roll;
    public int    Next(int max) => _roll % max;
}
