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
        double? satiety = null, double? energy = null, double? morale = null)
    {
        var details = new Dictionary<string, object>
        {
            ["actionId"] = actionId,
            ["actionKind"] = "Interact",
            ["outcomeType"] = success ? "Success" : "Failed"
        };
        if (satiety.HasValue) details["actor_satiety"] = satiety.Value;
        if (energy.HasValue) details["actor_energy"] = energy.Value;
        if (morale.HasValue) details["actor_morale"] = morale.Value;
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
        Assert.Equal("Alice", job.ActorId);
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
        Assert.Equal("Bob", job.ActorId);
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
    public void Consume_ActionCompleted_UpdatesCanonicalFacts()
    {
        var extractor = MakeExtractor();
        extractor.Consume(MakeCompleted(3.0, "Carol", satiety: 42.0, energy: 77.0));

        var facts = extractor.Facts.GetActor("Carol");
        Assert.NotNull(facts);
        Assert.Equal(42.0, facts!.Satiety);
        Assert.Equal(77.0, facts.Energy);
    }

    [Fact]
    public void Consume_MaxRecentBeats_CircularBuffer()
    {
        var extractor = MakeExtractor();
        // Use maxRecentBeats = 3 (passed via constructor)
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
}
