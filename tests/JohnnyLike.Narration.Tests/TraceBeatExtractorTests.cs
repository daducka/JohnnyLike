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

    private static TraceEvent MakeCompleted(double t, string actor, string actionId = "act1",
        string outcomeType = "Success", double? satiety = null, double? energy = null)
    {
        var details = new Dictionary<string, object>
        {
            ["actionId"] = actionId,
            ["actionKind"] = "Interact",
            ["outcomeType"] = outcomeType
        };
        // Use generic actor_* keys — the domain decides what to expose
        if (satiety.HasValue) details["actor_satiety"] = satiety.Value;
        if (energy.HasValue) details["actor_energy"] = energy.Value;
        return new TraceEvent(t, new ActorId(actor), "ActionCompleted", details);
    }

    // ── Basic job emission ────────────────────────────────────────────────────

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
        var evt = MakeCompleted(5.0, "Bob", "sleep", outcomeType: "Failed");

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

        Assert.Null(extractor.Consume(evt));
    }

    // ── Outcome type mapping ──────────────────────────────────────────────────

    [Theory]
    [InlineData("Success", true)]
    [InlineData("CriticalSuccess", true)]
    [InlineData("PartialSuccess", true)]
    [InlineData("Failed", false)]
    [InlineData("Failure", false)]
    [InlineData("TimedOut", false)]
    [InlineData("Cancelled", false)]
    [InlineData("", false)]
    public void IsSuccessOutcome_VariousStrings(string outcomeType, bool expected)
    {
        Assert.Equal(expected, TraceBeatExtractor.IsSuccessOutcome(outcomeType));
    }

    [Fact]
    public void Consume_ActionCompleted_StoresRawOutcomeType()
    {
        var extractor = MakeExtractor();
        extractor.Consume(MakeCompleted(1.0, "Eve", outcomeType: "CriticalSuccess"));

        var beat = extractor.RecentBeats[0];
        Assert.Equal("CriticalSuccess", beat.OutcomeType);
        Assert.True(beat.Success);
    }

    [Fact]
    public void Consume_ActionCompleted_PartialSuccess_TreatedAsSuccess()
    {
        var extractor = MakeExtractor();
        extractor.Consume(MakeCompleted(2.0, "Frank", outcomeType: "PartialSuccess"));

        Assert.True(extractor.RecentBeats[0].Success);
    }

    [Fact]
    public void Consume_ActionCompleted_Failed_TreatedAsFailure()
    {
        var extractor = MakeExtractor();
        extractor.Consume(MakeCompleted(3.0, "Grace", outcomeType: "Failed"));

        Assert.False(extractor.RecentBeats[0].Success);
    }

    // ── Summary cadence ───────────────────────────────────────────────────────

    [Fact]
    public void Consume_SummaryCadence_AppliesToOutcomePrompts()
    {
        // summaryEveryN = 2 so the 2nd beat triggers the summary
        var extractor = MakeExtractor(summaryEveryN: 2);

        // First beat (Assigned) — count = 1, no summary
        var job1 = extractor.Consume(MakeAssigned(1.0, "H"));
        Assert.NotNull(job1);
        Assert.DoesNotContain("updatedSummary\": \"<2-4", job1!.Prompt);

        // Second beat (Completed) — count hits 2, should request summary
        var job2 = extractor.Consume(MakeCompleted(2.0, "H"));
        Assert.NotNull(job2);
        Assert.Contains("updatedSummary", job2!.Prompt);
        Assert.DoesNotContain("null", job2.Prompt.Split('\n')
            .First(l => l.Contains("updatedSummary")));
    }

    [Fact]
    public void Consume_SummaryCadence_ResetAfterTrigger()
    {
        var extractor = MakeExtractor(summaryEveryN: 2);

        extractor.Consume(MakeAssigned(1.0, "I")); // count = 1
        extractor.Consume(MakeCompleted(2.0, "I")); // count = 2 → reset to 0, summary requested
        var job3 = extractor.Consume(MakeAssigned(3.0, "I")); // count = 1 again, no summary

        Assert.NotNull(job3);
        // No summary line for the next beat after reset
        var summaryLine = job3!.Prompt.Split('\n').FirstOrDefault(l => l.Contains("updatedSummary"));
        Assert.NotNull(summaryLine);
        Assert.Contains("null", summaryLine);
    }

    // ── Generic stats / canonical facts ──────────────────────────────────────

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
    public void Consume_UpdatesCurrentSimTime()
    {
        var extractor = MakeExtractor();
        extractor.Consume(MakeAssigned(7.5, "J"));
        Assert.Equal(7.5, extractor.Facts.CurrentSimTime);
    }

    // ── Circular buffer ───────────────────────────────────────────────────────

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

    // ── Guard cases ───────────────────────────────────────────────────────────

    [Fact]
    public void Consume_AssignedWithNoActorId_ReturnsNull()
    {
        var extractor = MakeExtractor();
        var evt = new TraceEvent(1.0, null, "ActionAssigned",
            new Dictionary<string, object> { ["actionId"] = "x", ["actionKind"] = "Wait" });

        Assert.Null(extractor.Consume(evt));
    }

    // ── World-event support ───────────────────────────────────────────────────

    [Fact]
    public void Consume_RegisteredWorldEvent_ReturnsWorldEventJob()
    {
        var extractor = MakeExtractor();

        extractor.RegisterWorldEventHandler("CampfireExtinguished", evt =>
            new Beat(evt.Time, null, "World", evt.EventType, "", "campfire",
                Success: null, StatsAfter: null));

        var worldEvt = new TraceEvent(10.0, null, "CampfireExtinguished",
            new Dictionary<string, object> { ["objectId"] = "campfire" });

        var job = extractor.Consume(worldEvt);

        Assert.NotNull(job);
        Assert.Equal(NarrationJobKind.WorldEvent, job!.Kind);
        Assert.Equal("campfire", job.SubjectId); // beat.Subject used, not beat.ActorId
        Assert.Equal(10.0, job.PlayAtSimTime);
    }

    [Fact]
    public void Consume_UnregisteredWorldEvent_ReturnsNull()
    {
        var extractor = MakeExtractor();
        var evt = new TraceEvent(5.0, null, "WeatherChanged", new Dictionary<string, object>());
        Assert.Null(extractor.Consume(evt));
    }

    [Fact]
    public void Consume_WorldEventHandlerReturnsNull_ReturnsNull()
    {
        var extractor = MakeExtractor();
        extractor.RegisterWorldEventHandler("TideTurned", _ => null);

        var evt = new TraceEvent(7.0, null, "TideTurned", new Dictionary<string, object>());
        Assert.Null(extractor.Consume(evt));
    }
}
