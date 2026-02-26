using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Engine;

namespace JohnnyLike.Domain.Island.Tests;

/// <summary>
/// Verifies that world-tick transitions emit NarrationBeat trace events.
/// </summary>
public class NarrationBeatEmissionTests
{
    private static (Engine.Engine engine, InMemoryTraceSink sink) MakeIslandEngine()
    {
        var domainPack = new IslandDomainPack();
        var sink = new InMemoryTraceSink();
        var engine = new Engine.Engine(domainPack, seed: 42, sink);
        return (engine, sink);
    }

    [Fact]
    public void CalendarItem_DayRollover_EmitsNarrationBeat()
    {
        // Arrange: start just before midnight (TimeOfDay ≈ 0.999)
        var (engine, sink) = MakeIslandEngine();
        var world = (IslandWorldState)engine.WorldState;
        var calendar = world.GetItem<CalendarItem>("calendar")!;
        calendar.TimeOfDay = 0.9999;   // almost at the day boundary
        calendar.DayCount = 0;

        // Act: advance enough time to cross midnight (> 8.64 s = 1/10000 day)
        engine.AdvanceTicks((long)(10.0 * 20));

        // Assert
        var beats = sink.GetEvents()
            .Where(e => e.EventType == "NarrationBeat")
            .ToList();

        Assert.NotEmpty(beats);
        var dayBeat = beats.FirstOrDefault(b =>
            b.Details.TryGetValue("subjectId", out var sid) && sid?.ToString() == "calendar:day");
        Assert.NotNull(dayBeat);
        Assert.Contains("day", dayBeat!.Details["text"].ToString()!.ToLowerInvariant());
    }

    [Fact]
    public void BeachItem_TideChange_EmitsNarrationBeat()
    {
        // Arrange: set time to just before tide-phase boundary (6-hour mark)
        var (engine, sink) = MakeIslandEngine();
        var world = (IslandWorldState)engine.WorldState;
        var calendar = world.GetItem<CalendarItem>("calendar")!;

        // Tide changes at 6h and 12h marks. Set to 5h 59m 50s so next tick crosses.
        // tidePhase = HourOfDay % 12 → crosses 6.0 when HourOfDay crosses 6.0 or 18.0
        calendar.TimeOfDay = 5.9997 / 24.0;

        // Act
        engine.AdvanceTicks((long)(10.0 * 20));

        // Assert
        var beats = sink.GetEvents()
            .Where(e => e.EventType == "NarrationBeat")
            .ToList();

        var tideBeat = beats.FirstOrDefault(b =>
            b.Details.TryGetValue("subjectId", out var sid) && sid?.ToString() == "beach:tide");
        Assert.NotNull(tideBeat);
    }

    [Fact]
    public void FishingPole_QualityThreshold_EmitsNarrationBeat()
    {
        // Arrange: add an actor with a fishing pole just above the 75% threshold
        var (engine, sink) = MakeIslandEngine();
        var world = (IslandWorldState)engine.WorldState;
        var actorId = new ActorId("Johnny");
        engine.AddActor(actorId);

        var domainPack = new IslandDomainPack();
        domainPack.InitializeActorItems(actorId, world);

        var pole = world.WorldItems.OfType<FishingPoleItem>().First();
        // Set to just above 75.0 so a 10-second tick (decay 0.005/s → 0.05 total) crosses the threshold
        pole.Quality = 75.04;

        // Act: tick 10 seconds → quality drops 0.005 * 10 = 0.05, crossing below 75.0
        engine.AdvanceTicks((long)(10.0 * 20));

        // Assert: should have a NarrationBeat about the fishing rod
        var beats = sink.GetEvents()
            .Where(e => e.EventType == "NarrationBeat")
            .ToList();

        var poleBeat = beats.FirstOrDefault(b =>
            b.Details.TryGetValue("subjectId", out var sid) && sid?.ToString() == "item:fishing_pole");
        Assert.NotNull(poleBeat);
        Assert.Contains("fishing rod", poleBeat!.Details["text"].ToString()!.ToLowerInvariant());
    }
}
