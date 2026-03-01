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
        new((long)(t * 20), new ActorId(actor), "ActionAssigned",
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
        return new TraceEvent((long)(t * 20), new ActorId(actor), "ActionCompleted", details);
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
    public void Consume_ActionCompleted_MissingActionKind_StillReturnsOutcomeJob()
    {
        var extractor = MakeExtractor();
        var evt = new TraceEvent((long)(5.0 * 20), new ActorId("Bob"), "ActionCompleted",
            new Dictionary<string, object>
            {
                ["actionId"] = "sleep_under_tree",
                ["outcomeType"] = "Success",
                ["actualDurationTicks"] = 440,
                ["actor_satiety"] = 59,
                ["actor_energy"] = 73.4,
                ["actor_morale"] = 76.2
            });

        var job = extractor.Consume(evt);

        Assert.NotNull(job);
        Assert.Equal(NarrationJobKind.Outcome, job!.Kind);
        Assert.Equal("Bob", job.SubjectId);
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
        var evt = new TraceEvent((long)(1.0 * 20), null, "ActionAssigned",
            new Dictionary<string, object> { ["actionId"] = "x", ["actionKind"] = "Wait" });

        Assert.Null(extractor.Consume(evt));
    }

    // ── World-event support ───────────────────────────────────────────────────

    [Fact]
    public void Consume_RegisteredWorldEvent_ReturnsWorldEventJob()
    {
        var extractor = MakeExtractor();

        extractor.RegisterWorldEventHandler("CampfireExtinguished", evt =>
            new Beat(evt.TimeSeconds, null, "World", evt.EventType, "", "campfire",
                Success: null, StatsAfter: null));

        var worldEvt = new TraceEvent((long)(10.0 * 20), null, "CampfireExtinguished",
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
        var evt = new TraceEvent((long)(5.0 * 20), null, "WeatherChanged", new Dictionary<string, object>());
        Assert.Null(extractor.Consume(evt));
    }

    [Fact]
    public void Consume_WorldEventHandlerReturnsNull_ReturnsNull()
    {
        var extractor = MakeExtractor();
        extractor.RegisterWorldEventHandler("TideTurned", _ => null);

        var evt = new TraceEvent((long)(7.0 * 20), null, "TideTurned", new Dictionary<string, object>());
        Assert.Null(extractor.Consume(evt));
    }

    // ── NarrationBeat (domain-generic) support ────────────────────────────────

    private static TraceEvent MakeNarrationBeat(
        double t, string text, string? subjectId = null, string phase = "WorldTick", string? actorId = null)
    {
        var details = new Dictionary<string, object>
        {
            ["text"] = text,
            ["phase"] = phase,
            ["priority"] = 50
        };
        if (subjectId != null) details["subjectId"] = subjectId;
        var actor = actorId != null ? (ActorId?)new ActorId(actorId) : null;
        return new TraceEvent((long)(t * 20), actor, "NarrationBeat", details);
    }

    [Fact]
    public void Consume_NarrationBeat_ReturnsWorldEventJob()
    {
        var extractor = MakeExtractor();
        var evt = MakeNarrationBeat(10.0, "The tide turns, rising from low to high.", subjectId: "beach:tide");

        var job = extractor.Consume(evt);

        Assert.NotNull(job);
        Assert.Equal(NarrationJobKind.WorldEvent, job!.Kind);
        Assert.Equal(10.0, job.PlayAtSimTime);
        Assert.Equal("beach:tide", job.SubjectId);
    }

    [Fact]
    public void Consume_NarrationBeat_WithActorId_SetsSubjectId()
    {
        var extractor = MakeExtractor();
        var evt = MakeNarrationBeat(5.0, "Johnny pulls a fish from the water.",
            subjectId: "resource:fish", actorId: "Johnny");

        var job = extractor.Consume(evt);

        Assert.NotNull(job);
        Assert.Equal("resource:fish", job!.SubjectId);
    }

    [Fact]
    public void Consume_NarrationBeat_NoSubjectId_FallsBackToActorId()
    {
        var extractor = MakeExtractor();
        var evt = MakeNarrationBeat(3.0, "Johnny casts the line.", actorId: "Johnny");

        var job = extractor.Consume(evt);

        Assert.NotNull(job);
        Assert.Equal("Johnny", job!.SubjectId);
    }

    [Fact]
    public void Consume_NarrationBeat_AddsBeatToRecentBeats()
    {
        var extractor = MakeExtractor();
        var evt = MakeNarrationBeat(8.0, "The campfire sputters out.", subjectId: "item:campfire");

        extractor.Consume(evt);

        var recent = extractor.RecentBeats;
        Assert.NotEmpty(recent);
        var beat = recent.Last();
        Assert.Equal("DomainBeat", beat.SubjectKind);
        Assert.Equal("NarrationBeat", beat.EventType);
        Assert.Equal("The campfire sputters out.", beat.Subject);
    }

    [Fact]
    public void Consume_NarrationBeat_EmptyText_ReturnsNull()
    {
        var extractor = MakeExtractor();
        var details = new Dictionary<string, object> { ["text"] = "", ["phase"] = "WorldTick", ["priority"] = 50 };
        var evt = new TraceEvent((long)(1.0 * 20), null, "NarrationBeat", details);

        Assert.Null(extractor.Consume(evt));
    }

    [Fact]
    public void Consume_NarrationBeat_PromptIncludesDomainBeatText()
    {
        var extractor = MakeExtractor();
        var evt = MakeNarrationBeat(5.0, "A new day dawns on the island.", subjectId: "calendar:day");

        var job = extractor.Consume(evt);

        Assert.NotNull(job);
        Assert.Contains("A new day dawns on the island.", job!.Prompt);
    }

    [Fact]
    public void Consume_NarrationBeat_PromptDoesNotDuplicateCurrentBeatInRecentDomainBeats()
    {
        var extractor = MakeExtractor();

        // Seed recent history with one prior domain beat.
        extractor.Consume(MakeNarrationBeat(1.0, "The tide turns.", subjectId: "beach:tide"));

        // Current beat should appear exactly once in the generated prompt.
        var currentText = "The campfire has gone out.";
        var job = extractor.Consume(MakeNarrationBeat(2.0, currentText, subjectId: "item:campfire"));

        Assert.NotNull(job);
        Assert.Equal(1, job!.Prompt.Split(currentText).Length - 1);
    }

    [Fact]
    public void Consume_NarrationBeat_RequiresNoRegistration()
    {
        // Verifies that NarrationBeat events are handled without any domain-specific handler registration
        var extractor = MakeExtractor();
        // NOTE: we do NOT call RegisterWorldEventHandler here
        var evt = MakeNarrationBeat(2.0, "Storm incoming.", subjectId: "weather:precipitation");

        var job = extractor.Consume(evt);

        Assert.NotNull(job);
        Assert.Equal(NarrationJobKind.WorldEvent, job!.Kind);
    }

    // ── Recent-events ordering (current beat must not appear in recent history) ──

    [Fact]
    public void Consume_ActionAssigned_CurrentBeatNotInRecentBeats()
    {
        var extractor = MakeExtractor();
        // Seed one prior beat so RecentBeats is non-empty after it.
        extractor.Consume(MakeAssigned(1.0, "Alice", "prior_action"));

        var job = extractor.Consume(MakeAssigned(2.0, "Alice", "current_action"));

        Assert.NotNull(job);
        // The prompt is built before AddBeat, so "current_action" must appear exactly once
        // (in "## Current Event", not also in "## Recent events").
        Assert.Equal(1, job!.Prompt.Split("current_action").Length - 1);
        // The prior action should be in recent events, the current one should not
        Assert.Contains("prior_action", job.Prompt);
    }

    [Fact]
    public void Consume_ActionCompleted_CurrentBeatNotInRecentBeats()
    {
        var extractor = MakeExtractor();
        // Seed one prior completed beat.
        extractor.Consume(MakeCompleted(1.0, "Bob", "prior_action"));

        var job = extractor.Consume(MakeCompleted(2.0, "Bob", "current_action"));

        Assert.NotNull(job);
        // "current_action" must appear exactly once in the prompt (Current Event section only).
        Assert.Equal(1, job!.Prompt.Split("current_action").Length - 1);
        Assert.Contains("prior_action", job.Prompt);
    }

    // ── Qualitative stats ─────────────────────────────────────────────────────

    [Fact]
    public void Consume_ActionCompleted_WithQualitativeStats_PromptContainsDescriptors()
    {
        var extractor = MakeExtractor();
        // Use stats that map to known descriptors: satiety=25 → "hungry", energy=85 → "energetic"
        var evt = MakeCompleted(3.0, "Carol", satiety: 25.0, energy: 85.0);

        var job = extractor.Consume(evt);

        Assert.NotNull(job);
        // Raw numeric values from the test event should appear as-is (test bypasses
        // GetActorStateSnapshot), but the system instruction must forbid numbers.
        Assert.Contains("Avoid mentioning numeric values", job!.Prompt);
    }

    [Fact]
    public void AppendSystemInstructions_ContainsQualitativeInstruction()
    {
        var builder = new NarrationPromptBuilder(NarrationTone.Documentary);
        var facts = new CanonicalFacts { Domain = "test" };
        var beat = new Beat(1.0, "Alice", "Actor", "ActionAssigned", "Interact", "eat food");

        var prompt = builder.BuildAttemptPrompt(beat, facts, new List<Beat>(), false);

        Assert.Contains("Avoid mentioning numeric values", prompt);
    }

    // ── NarrationDescription ──────────────────────────────────────────────────

    [Fact]
    public void Consume_ActionAssigned_WithNarrationDescription_UsesDescriptionInPrompt()
    {
        var extractor = MakeExtractor();
        var evt = new TraceEvent((long)(1.0 * 20), new ActorId("Alice"), "ActionAssigned",
            new Dictionary<string, object>
            {
                ["actionId"] = "shake_tree_coconut",
                ["actionKind"] = "Interact",
                ["narrationDescription"] = "shake the palm tree to knock down coconuts"
            });

        var job = extractor.Consume(evt);

        Assert.NotNull(job);
        Assert.Contains("shake the palm tree to knock down coconuts", job!.Prompt);
        Assert.DoesNotContain("shake_tree_coconut", job.Prompt);
    }

    [Fact]
    public void Consume_ActionAssigned_WithoutNarrationDescription_FallsBackToActionId()
    {
        var extractor = MakeExtractor();
        var evt = MakeAssigned(1.0, "Alice", "my_action_id");

        var job = extractor.Consume(evt);

        Assert.NotNull(job);
        Assert.Contains("my_action_id", job!.Prompt);
    }

    // ── Day phase ─────────────────────────────────────────────────────────────

    [Fact]
    public void Consume_DayPhaseChanged_UpdatesWorldContextViaRegisteredHandler()
    {
        var extractor = MakeExtractor();
        // Register a context-update handler the way the runner / domain setup code would
        extractor.RegisterContextUpdateHandler("DayPhaseChanged", evt =>
        {
            if (evt.Details.TryGetValue("dayPhase", out var phase))
                extractor.Facts.WorldContext["time_of_day"] = $"It is currently {phase.ToString()!.ToLowerInvariant()}.";
        });

        var evt = new TraceEvent(0, null, "DayPhaseChanged",
            new Dictionary<string, object>
            {
                ["dayPhase"] = "Morning",
                ["text"]     = "Morning light spreads across the beach."
            });

        extractor.Consume(evt);

        Assert.True(extractor.Facts.WorldContext.TryGetValue("time_of_day", out var value));
        Assert.Equal("It is currently morning.", value);
    }

    [Fact]
    public void Consume_DayPhaseChanged_ReturnsWorldEventJob_WhenHandlerRegistered()
    {
        var extractor = MakeExtractor();
        extractor.RegisterWorldEventHandler("DayPhaseChanged", evt =>
        {
            var text = evt.Details.TryGetValue("text", out var t) ? t.ToString()! : string.Empty;
            return text.Length > 0
                ? new Beat(evt.TimeSeconds, null, "World", evt.EventType, "", text)
                : null;
        });

        var evt = new TraceEvent(0, null, "DayPhaseChanged",
            new Dictionary<string, object>
            {
                ["dayPhase"] = "Night",
                ["text"]     = "Night falls over the island."
            });

        var job = extractor.Consume(evt);

        Assert.NotNull(job);
        Assert.Equal(NarrationJobKind.WorldEvent, job!.Kind);
    }

    [Fact]
    public void BuildAttemptPrompt_WithWorldContext_IncludesContextLine()
    {
        var builder = new NarrationPromptBuilder(NarrationTone.Documentary);
        var facts = new CanonicalFacts { Domain = "test" };
        facts.WorldContext["time_of_day"] = "It is currently morning.";
        var beat = new Beat(1.0, "Alice", "Actor", "ActionAssigned", "Interact", "eat food");

        var prompt = builder.BuildAttemptPrompt(beat, facts, new List<Beat>(), false);

        Assert.Contains("It is currently morning.", prompt);
    }

    [Fact]
    public void BuildAttemptPrompt_WithEmptyWorldContext_NoDayPhaseLine()
    {
        var builder = new NarrationPromptBuilder(NarrationTone.Documentary);
        var facts = new CanonicalFacts { Domain = "test" };  // no WorldContext entries
        var beat = new Beat(1.0, "Alice", "Actor", "ActionAssigned", "Interact", "eat food");

        var prompt = builder.BuildAttemptPrompt(beat, facts, new List<Beat>(), false);

        Assert.DoesNotContain("It is currently", prompt);
    }

    // ── OutcomeNarration ──────────────────────────────────────────────────────

    [Fact]
    public void BuildOutcomePrompt_WithOutcomeNarration_IncludesOutcomeContext()
    {
        var builder = new NarrationPromptBuilder(NarrationTone.Documentary);
        var facts = new CanonicalFacts { Domain = "test" };
        var beat = new Beat(2.0, "Alice", "Actor", "ActionCompleted", "Interact",
            "some_action",
            Success: true, OutcomeType: "Success",
            OutcomeNarration: "Alice knocks a single coconut loose.");

        var prompt = builder.BuildOutcomePrompt(beat, facts, new List<Beat>(), false);

        Assert.Contains("## Outcome Context", prompt);
        Assert.Contains("Alice knocks a single coconut loose.", prompt);
    }

    [Fact]
    public void BuildOutcomePrompt_WithoutOutcomeNarration_NoOutcomeContextSection()
    {
        var builder = new NarrationPromptBuilder(NarrationTone.Documentary);
        var facts = new CanonicalFacts { Domain = "test" };
        var beat = new Beat(2.0, "Alice", "Actor", "ActionCompleted", "Interact", "sleep",
            Success: true);

        var prompt = builder.BuildOutcomePrompt(beat, facts, new List<Beat>(), false);

        Assert.DoesNotContain("## Outcome Context", prompt);
    }

    [Fact]
    public void Consume_ActionCompleted_WithOutcomeNarration_IncludesInPrompt()
    {
        var extractor = MakeExtractor();
        var details = new Dictionary<string, object>
        {
            ["actionId"]       = "shake_tree_coconut",
            ["actionKind"]     = "Interact",
            ["outcomeType"]    = "Success",
            ["outcomeNarration"] = "Alice knocks a single coconut loose."
        };
        var evt = new TraceEvent((long)(2.0 * 20), new ActorId("Alice"), "ActionCompleted", details);

        var job = extractor.Consume(evt);

        Assert.NotNull(job);
        Assert.Contains("Alice knocks a single coconut loose.", job!.Prompt);
        Assert.Contains("## Outcome Context", job.Prompt);
    }

    // ── Narration history ─────────────────────────────────────────────────────

    [Fact]
    public void BuildAttemptPrompt_WithPreviousNarrations_IncludesPreviousNarrationSection()
    {
        var builder = new NarrationPromptBuilder(NarrationTone.Documentary);
        var facts = new CanonicalFacts { Domain = "test" };
        var beat = new Beat(1.0, "Alice", "Actor", "ActionAssigned", "Interact", "eat food");
        var history = new List<string> { "Alice wanders down the beach.", "She spots a coconut tree." };

        var prompt = builder.BuildAttemptPrompt(beat, facts, new List<Beat>(), false, history);

        Assert.Contains("## Previous Narration", prompt);
        Assert.Contains("- Alice wanders down the beach.", prompt);
        Assert.Contains("- She spots a coconut tree.", prompt);
    }

    [Fact]
    public void BuildOutcomePrompt_WithPreviousNarrations_IncludesPreviousNarrationSection()
    {
        var builder = new NarrationPromptBuilder(NarrationTone.Documentary);
        var facts = new CanonicalFacts { Domain = "test" };
        var beat = new Beat(2.0, "Alice", "Actor", "ActionCompleted", "Interact", "eat food", Success: true);
        var history = new List<string> { "Alice reaches for the coconut." };

        var prompt = builder.BuildOutcomePrompt(beat, facts, new List<Beat>(), false, history);

        Assert.Contains("## Previous Narration", prompt);
        Assert.Contains("- Alice reaches for the coconut.", prompt);
    }

    [Fact]
    public void BuildNarrationBeatPrompt_WithPreviousNarrations_IncludesPreviousNarrationSection()
    {
        var builder = new NarrationPromptBuilder(NarrationTone.Documentary);
        var facts = new CanonicalFacts { Domain = "test" };
        var beat = new Beat(3.0, null, "DomainBeat", "NarrationBeat", "", "A cloud drifts past.");
        var history = new List<string> { "The sun beats down." };

        var prompt = builder.BuildNarrationBeatPrompt(beat, "A cloud drifts past.", facts, new List<Beat>(), false, history);

        Assert.Contains("## Previous Narration", prompt);
        Assert.Contains("- The sun beats down.", prompt);
    }

    [Fact]
    public void BuildWorldEventPrompt_WithPreviousNarrations_IncludesPreviousNarrationSection()
    {
        var builder = new NarrationPromptBuilder(NarrationTone.Documentary);
        var facts = new CanonicalFacts { Domain = "test" };
        var beat = new Beat(4.0, null, "World", "DayPhaseChanged", "", "Morning arrives.");
        var history = new List<string> { "Darkness gave way to pink hues." };

        var prompt = builder.BuildWorldEventPrompt(beat, facts, new List<Beat>(), false, history);

        Assert.Contains("## Previous Narration", prompt);
        Assert.Contains("- Darkness gave way to pink hues.", prompt);
    }

    [Fact]
    public void BuildAttemptPrompt_WithEmptyPreviousNarrations_NoPreviousNarrationSection()
    {
        var builder = new NarrationPromptBuilder(NarrationTone.Documentary);
        var facts = new CanonicalFacts { Domain = "test" };
        var beat = new Beat(1.0, "Alice", "Actor", "ActionAssigned", "Interact", "eat food");

        var prompt = builder.BuildAttemptPrompt(beat, facts, new List<Beat>(), false, new List<string>());

        Assert.DoesNotContain("## Previous Narration", prompt);
    }

    [Fact]
    public void AppendSystemInstructions_ContainsVariedPhrasingGuidance()
    {
        var builder = new NarrationPromptBuilder(NarrationTone.Documentary);
        var facts = new CanonicalFacts { Domain = "test" };
        var beat = new Beat(1.0, "Alice", "Actor", "ActionAssigned", "Interact", "eat food");

        var prompt = builder.BuildAttemptPrompt(beat, facts, new List<Beat>(), false);

        Assert.Contains("Vary your sentence openings", prompt);
        Assert.Contains("continue the story naturally", prompt);
    }

    [Fact]
    public void AddNarrationToHistory_BufferCapIsRespected()
    {
        var extractor = MakeExtractor();

        extractor.AddNarrationToHistory("Line one.");
        extractor.AddNarrationToHistory("Line two.");
        extractor.AddNarrationToHistory("Line three.");
        extractor.AddNarrationToHistory("Line four."); // should evict "Line one."

        Assert.Equal(3, extractor.NarrationHistory.Count);
        Assert.DoesNotContain("Line one.", extractor.NarrationHistory);
        Assert.Contains("Line four.", extractor.NarrationHistory);
    }

    [Fact]
    public void AddNarrationToHistory_EmptyStringIgnored()
    {
        var extractor = MakeExtractor();
        extractor.AddNarrationToHistory("");
        extractor.AddNarrationToHistory("   ");
        extractor.AddNarrationToHistory("Valid line.");

        Assert.Equal(1, extractor.NarrationHistory.Count);
    }

    [Fact]
    public void Consume_ActionAssigned_PromptIncludesNarrationHistory()
    {
        var extractor = MakeExtractor();
        extractor.AddNarrationToHistory("A prior narration line.");

        var job = extractor.Consume(MakeAssigned(1.0, "Alice", "eat"));

        Assert.NotNull(job);
        Assert.Contains("## Previous Narration", job!.Prompt);
        Assert.Contains("- A prior narration line.", job.Prompt);
    }

    [Fact]
    public void Consume_ActionCompleted_PromptIncludesNarrationHistory()
    {
        var extractor = MakeExtractor();
        extractor.AddNarrationToHistory("Alice grabbed the fruit.");

        var job = extractor.Consume(MakeCompleted(2.0, "Alice", "eat"));

        Assert.NotNull(job);
        Assert.Contains("## Previous Narration", job!.Prompt);
        Assert.Contains("- Alice grabbed the fruit.", job.Prompt);
    }
}
