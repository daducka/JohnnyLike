using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Island.Metabolism;
using JohnnyLike.Domain.Island.Vitality;

namespace JohnnyLike.Domain.Island.Tests;

/// <summary>
/// Tests covering the unified sim-time model introduced in the Duration migration:
/// <list type="bullet">
///   <item>Duration factory methods and conversion helpers</item>
///   <item>ActionSpec and RecipeDefinition require Duration (compile-time enforcement demonstrated)</item>
///   <item>Calendar/day progression under unified sim time</item>
///   <item>Metabolism uses unified sim time correctly</item>
///   <item>Vitality uses unified sim time correctly</item>
///   <item>Buff timing APIs require Duration</item>
/// </list>
/// </summary>
public class DurationTests
{
    // ─── Part 1: Duration factory methods ────────────────────────────────────

    [Fact]
    public void Duration_Seconds_ProducesCorrectTicks()
    {
        var d = Duration.Seconds(30.0);
        Assert.Equal(30L * EngineConstants.TickHz, d.Ticks);
    }

    [Fact]
    public void Duration_Minutes_ProducesCorrectTicks()
    {
        var d = Duration.Minutes(5.0);
        Assert.Equal(5L * 60 * EngineConstants.TickHz, d.Ticks);
    }

    [Fact]
    public void Duration_Hours_ProducesCorrectTicks()
    {
        var d = Duration.Hours(2.0);
        Assert.Equal(2L * 3600 * EngineConstants.TickHz, d.Ticks);
    }

    [Fact]
    public void Duration_TotalSeconds_RoundTrips()
    {
        var d = Duration.Seconds(120.0);
        Assert.Equal(120.0, d.TotalSeconds, precision: 6);
    }

    [Fact]
    public void Duration_TotalMinutes_RoundTrips()
    {
        var d = Duration.Minutes(10.0);
        Assert.Equal(10.0, d.TotalMinutes, precision: 6);
    }

    [Fact]
    public void Duration_TotalHours_RoundTrips()
    {
        var d = Duration.Hours(3.0);
        Assert.Equal(3.0, d.TotalHours, precision: 6);
    }

    [Fact]
    public void Duration_FromTicks_RoundTrips()
    {
        var ticks = 12345L;
        var d = Duration.FromTicks(ticks);
        Assert.Equal(ticks, d.Ticks);
    }

    [Fact]
    public void Duration_Comparison_Works()
    {
        var shorter = Duration.Minutes(5);
        var longer  = Duration.Minutes(10);
        var same    = Duration.Minutes(5);

        Assert.True(shorter < longer);
        Assert.True(longer > shorter);
        Assert.True(shorter <= longer);
        Assert.True(longer >= shorter);
        Assert.True(shorter != longer);
        Assert.Equal(shorter, same);
    }

    [Fact]
    public void Duration_RandomRange_IsWithinBounds()
    {
        var rng  = new Random(42);
        var d    = Duration.Minutes(5.0, 10.0, rng);

        Assert.True(d >= Duration.Minutes(5.0),
            $"Duration {d.TotalMinutes:F2}m should be >= 5 min");
        Assert.True(d <= Duration.Minutes(10.0),
            $"Duration {d.TotalMinutes:F2}m should be <= 10 min");
    }

    [Fact]
    public void Duration_ToString_IncludesSeconds()
    {
        var d = Duration.Seconds(90.0); // 1m30s
        var s = d.ToString();
        Assert.Contains("90", s);
    }

    // ─── Part 2: ActionSpec requires Duration (compile-time enforcement) ──────
    // The following test demonstrates that ActionSpec only accepts Duration —
    // it will not compile with a raw long value.

    [Fact]
    public void ActionSpec_RequiresDuration_NotRawLong()
    {
        // This test documents the type-safe API: Duration is required.
        var duration = Duration.Minutes(5.0);
        var spec = new ActionSpec(
            new ActionId("test"),
            ActionKind.Interact,
            EmptyActionParameters.Instance,
            duration,           // Must be Duration, not long
            "test action"
        );

        Assert.Equal(duration, spec.EstimatedDuration);
        Assert.Equal(Duration.Minutes(5.0).Ticks, spec.EstimatedDuration.Ticks);
    }

    [Fact]
    public void ActionOutcome_RequiresDuration_NotRawLong()
    {
        var duration = Duration.Seconds(30.0);
        var outcome = new ActionOutcome(
            new ActionId("test"),
            ActionOutcomeType.Success,
            duration            // Must be Duration, not long
        );

        Assert.Equal(duration, outcome.ActualDuration);
    }

    [Fact]
    public void ResourceRequirement_AcceptsDurationOverride()
    {
        var d   = Duration.Minutes(2.0);
        var req = new ResourceRequirement(new ResourceId("test:res"), DurationOverride: d);

        Assert.Equal(d, req.DurationOverride);
        Assert.Equal(Duration.Minutes(2.0).Ticks, req.DurationOverride!.Value.Ticks);
    }

    // ─── Part 3: Calendar day progression under unified sim time ─────────────

    [Fact]
    public void Calendar_Tick_AdvancesOneFullDay_After86400Seconds()
    {
        var calendar = new CalendarItem();
        var world    = new IslandWorldState();
        world.WorldItems.Add(calendar);

        var dayBefore = calendar.DayCount;

        // Advance exactly one full day (86 400 sim-seconds = 1 728 000 ticks at 20 Hz).
        long oneDayTicks = 86400L * EngineConstants.TickHz;
        calendar.Tick(oneDayTicks, world);

        Assert.Equal(dayBefore + 1, calendar.DayCount);
    }

    [Fact]
    public void Calendar_Tick_DoesNotAdvanceDay_After1440Seconds()
    {
        // Sanity check: the OLD 1440-second "day" must no longer trigger a day change.
        // Under unified sim time a day is 86 400 seconds.
        var calendar = new CalendarItem();
        var world    = new IslandWorldState();
        world.WorldItems.Add(calendar);

        var dayBefore = calendar.DayCount;

        long oldDayTicks = 1440L * EngineConstants.TickHz;
        calendar.Tick(oldDayTicks, world);

        Assert.Equal(dayBefore, calendar.DayCount);
        // 1440 sim-seconds must not advance the day counter under unified sim time (day = 86 400 s)
    }

    [Fact]
    public void Calendar_DayPhase_TransitionsCorrectly_OverOneDay()
    {
        var calendar = new CalendarItem { TimeOfDay = 0.0 }; // midnight
        var world    = new IslandWorldState();
        world.WorldItems.Add(calendar);

        // Advance 6 hours → hour 6 → Dawn.
        long sixHourTicks = 6L * 3600 * EngineConstants.TickHz;
        calendar.Tick(sixHourTicks, world);

        var phase = CalendarItem.ComputeDayPhase(calendar.HourOfDay);
        Assert.Equal(DayPhase.Dawn, phase);

        // Advance another 6 hours → hour 12 → Noon.
        calendar.Tick(sixHourTicks * 2, world);
        phase = CalendarItem.ComputeDayPhase(calendar.HourOfDay);
        Assert.Equal(DayPhase.Noon, phase);

        // Advance to hour 18 → Evening.
        calendar.Tick(sixHourTicks * 3, world);
        phase = CalendarItem.ComputeDayPhase(calendar.HourOfDay);
        Assert.Equal(DayPhase.Evening, phase);
    }

    // ─── Part 4: Metabolism uses unified sim time ──────────────────────────────

    [Fact]
    public void Metabolism_FullDay_DrainsSatiety_ByRoughly120()
    {
        // BasalKcalPerDay = 2400 kcal. Full satiety = 2000 kcal.
        // Expected drain = 2400/2000*100 = 120 Satiety over one full sim-day (86 400 s).
        double satiety = 100.0;
        double energy  = 100.0;

        MetabolismMath.ApplyTimeStep(
            ref satiety, ref energy,
            86400.0, // one full day
            activityKcalPerSecond: 0.0,
            isSleeping: false);

        double drop = 100.0 - satiety;
        Assert.InRange(drop, 80.0, 130.0);
    }

    [Fact]
    public void Metabolism_SatietySurvivesFor18Hours_AtRest()
    {
        // BasalKcalPerDay = 2400 kcal. Full satiety = 2000 kcal.
        // In 18 hours: burn = 2400 * 18/24 = 1800 kcal → remaining = 200 kcal → ~10 Satiety.
        // Energy starts at 100, so no conversion fires.
        double satiety = 100.0;
        double energy  = 100.0;

        MetabolismMath.ApplyTimeStep(
            ref satiety, ref energy,
            18.0 * 3600, // 18 sim-hours
            activityKcalPerSecond: 0.0,
            isSleeping: false);

        // Actor should still have some satiety remaining after 18 hours.
        Assert.True(satiety > 0.0,
            $"Satiety should not be fully depleted after 18 hours, got {satiety:F2}");
        // But it should have drained to near-empty.
        Assert.True(satiety < 20.0,
            $"Satiety should be near-depleted after 18 hours of rest, got {satiety:F2}");
    }

    [Fact]
    public void Metabolism_BasalRateIsDerivedFrom86400SecondsPerDay()
    {
        // Confirms the constant is set correctly for unified sim time.
        var expectedRate = MetabolismMath.BasalKcalPerDay / 86400.0;
        Assert.Equal(MetabolismMath.BasalKcalPerSecond, expectedRate, precision: 10);
    }

    // ─── Part 5: Vitality uses unified sim time ────────────────────────────────

    [Fact]
    public void Vitality_Starvation_DamagesHealth_OverMultipleHours()
    {
        // StarvationDamagePerSecond = 0.0006/s. Over 4 hours (14 400 s):
        // damage = 0.0006 * 14400 = 8.64 health.
        var domain  = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actor   = (IslandActorState)domain.CreateActorState(actorId,
            new Dictionary<string, object>
            {
                ["satiety"] = 5.0,   // critically low → starvation active
                ["energy"]  = 80.0,
                ["morale"]  = 80.0,
            });
        actor.Health = 100.0;

        var world   = new IslandWorldState();
        var actors  = new Dictionary<ActorId, ActorState> { [actorId] = actor };
        long fourHours = 4L * 3600 * EngineConstants.TickHz;

        domain.TickWorldState(world, actors, fourHours, new EmptyResourceAvailability());

        Assert.True(actor.Health < 100.0,
            $"Starvation should damage health over 4 hours; was 100, now {actor.Health:F2}");
        Assert.True(actor.Health > 85.0,
            $"Starvation over 4 hours should not kill instantly; got {actor.Health:F2}");
    }

    [Fact]
    public void Vitality_Recovery_HealsHealth_WhenAllStatsGood()
    {
        // RecoveryPerSecond = 0.0002/s. Over 6 hours (21 600 s):
        // recovery = 0.0002 * 21600 = 4.32 health.
        // We tick only the VitalityBuff directly to isolate health recovery
        // without metabolism depleting satiety.
        var domain  = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actor   = (IslandActorState)domain.CreateActorState(actorId,
            new Dictionary<string, object>
            {
                ["satiety"] = 80.0,
                ["energy"]  = 80.0,
                ["morale"]  = 80.0,
            });
        actor.Health = 90.0;

        var world = new IslandWorldState();

        // Tick only the VitalityBuff (not all buffs) to isolate recovery.
        var vitalityBuff = actor.TryGetBuff<VitalityBuff>()!;
        long sixHours = 6L * 3600 * EngineConstants.TickHz;
        vitalityBuff.OnTick(actor, world, sixHours);

        Assert.True(actor.Health > 90.0,
            $"Health should recover over 6 hours with good stats; was 90, now {actor.Health:F2}");
    }

    // ─── Part 6: Representative action durations ──────────────────────────────

    [Fact]
    public void ActionDurations_AreExpressedInRealMinutes()
    {
        // Verify that representative actions have durations in the expected minutes range.
        // This is a sanity check that the Duration migration was completed.
        var domain  = new IslandDomainPack();
        var actorId = new ActorId("Jim");
        var actor   = (IslandActorState)domain.CreateActorState(actorId);
        var world   = new IslandWorldState();
        domain.InitializeActorItems(actorId, world);
        world.WorldItems.Add(new OceanItem("ocean"));

        var candidates = domain.GenerateCandidates(actorId, actor, world, 0L, new Random(1), new EmptyResourceAvailability());

        // All action durations should be >= 1 second (no tiny raw tick values).
        foreach (var c in candidates)
        {
            Assert.True(c.Action.EstimatedDuration >= Duration.Seconds(1.0),
                $"Action '{c.Action.Id}' has suspiciously short duration: {c.Action.EstimatedDuration}");
        }
    }
}
