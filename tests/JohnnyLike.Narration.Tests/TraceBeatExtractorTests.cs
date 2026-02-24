using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Narration;

namespace JohnnyLike.Narration.Tests;

public class TraceBeatExtractorTests
{
    private static TraceBeatExtractor MakeExtractor(int summaryEveryN = 5)
    {
        var facts = new CanonicalFacts { Domain = "test" };
        var builder = new NarrationPromptBuilder(NarrationTone.Documentary);
        return new TraceBeatExtractor(facts, builder, summaryRefreshEveryN: summaryEveryN);
    }

    private static TraceEvent MakeAssigned(double t, string actor, string actionId = "act1", string kind = "Interact") =>
        new(t, new ActorId(actor), "ActionAssigned",
            new Dictionary<string, object> { ["actionId"] = actionId, ["actionKind"] = kind });

    private static TraceEvent MakeCompleted(double t, string actor, string actionId = "act1", bool success = true,
        double? satiety = null, double? energy = null)
    {
        var details = new Dictionary<string, object>
        {
            ["actionId"] = actionId,
            ["actionKind"] = "Interact",
            ["outcomeType"] = success ? "Success" : "Failed"
        };
        // Use generic actor_* keys — the domain decides what to expose
        if (satiety.HasValue) details["actor_satiety"] = satiety.Value;
        if (energy.HasValue) details["actor_energy"] = energy.Value;
        return new TraceEvent(t, new ActorId(actor), "ActionCompleted", details);
    }

    [Fact]
    public void Consume_ActionAssigned_ReturnsAttemptJob()
    {
        var extractor = MakeExtractor();
        var evt = MakeAssigned(1.0, "Alice", "eat");

        var job = extractor.Consume(evt);

        Assert.NotNull(job);
        Assert.Equal(NarrationJobKind.Attempt, job!.Kind);
        Assert.Equal("Alice", job.SubjectId);
        Assert.Equal(1.0, job.PlayAtSimTime);
    }

    [Fact]
    public void Consume_ActionCompleted_ReturnsOutcomeJob()
    {
        var extractor = MakeExtractor();
        var evt = MakeCompleted(5.0, "Bob", "sleep", success: false);

        var job = extractor.Consume(evt);

        Assert.NotNull(job);
        Assert.Equal(NarrationJobKind.Outcome, job!.Kind);
        Assert.Equal("Bob", job.SubjectId);
        Assert.Equal(5.0, job.PlayAtSimTime);
    }

    [Fact]
    public void Consume_UnknownEvent_ReturnsNull()
    {
        var extractor = MakeExtractor();
        var evt = new TraceEvent(0, new ActorId("X"), "ActorAdded",
            new Dictionary<string, object>());

        var job = extractor.Consume(evt);

        Assert.Null(job);
    }

    [Fact]
    public void Consume_ActionCompleted_UpdatesCanonicalFacts_WithGenericStats()
    {
        var extractor = MakeExtractor();
        extractor.Consume(MakeCompleted(3.0, "Carol", satiety: 42.0, energy: 77.0));

        var facts = extractor.Facts.GetActor("Carol");
        Assert.NotNull(facts);
        // Stats are keyed by the part after "actor_" — domain-defined names
        Assert.True(facts!.Stats.ContainsKey("satiety"));
        Assert.Equal("42", facts.Stats["satiety"]);
        Assert.Equal("77", facts.Stats["energy"]);
    }

    [Fact]
    public void Consume_MaxRecentBeats_CircularBuffer()
    {
        var facts = new CanonicalFacts { Domain = "test" };
        var builder = new NarrationPromptBuilder(NarrationTone.Gritty);
        var small = new TraceBeatExtractor(facts, builder, maxRecentBeats: 3);

        for (int i = 0; i < 5; i++)
            small.Consume(MakeAssigned(i, "D"));

        Assert.Equal(3, small.RecentBeats.Count);
        Assert.Equal(2.0, small.RecentBeats[0].SimTime);  // oldest kept = index 2
    }

    [Fact]
    public void Consume_AssignedWithNoActorId_ReturnsNull()
    {
        var extractor = MakeExtractor();
        var evt = new TraceEvent(1.0, null, "ActionAssigned",
            new Dictionary<string, object> { ["actionId"] = "x", ["actionKind"] = "Wait" });

        var job = extractor.Consume(evt);
        Assert.Null(job);
    }

    [Fact]
    public void Consume_RegisteredWorldEvent_ReturnsWorldEventJob()
    {
        var extractor = MakeExtractor();

        // A domain registers its own world event (e.g. campfire going out)
        extractor.RegisterWorldEventHandler("CampfireExtinguished", evt =>
            new Beat(evt.Time, null, "World", evt.EventType, "", "campfire",
                Success: null, StatsAfter: null));

        var worldEvt = new TraceEvent(10.0, null, "CampfireExtinguished",
            new Dictionary<string, object> { ["objectId"] = "campfire" });

        var job = extractor.Consume(worldEvt);

        Assert.NotNull(job);
        Assert.Equal(NarrationJobKind.WorldEvent, job!.Kind);
        Assert.Null(job.SubjectId);   // no actor
        Assert.Equal(10.0, job.PlayAtSimTime);
    }

    [Fact]
    public void Consume_UnregisteredWorldEvent_ReturnsNull()
    {
        var extractor = MakeExtractor();
        var evt = new TraceEvent(5.0, null, "WeatherChanged",
            new Dictionary<string, object>());

        Assert.Null(extractor.Consume(evt));
    }

    [Fact]
    public void Consume_WorldEventHandlerReturnsNull_ReturnsNull()
    {
        var extractor = MakeExtractor();
        // Handler explicitly suppresses this beat
        extractor.RegisterWorldEventHandler("TideTurned", _ => null);

        var evt = new TraceEvent(7.0, null, "TideTurned", new Dictionary<string, object>());
        Assert.Null(extractor.Consume(evt));
    }
}
