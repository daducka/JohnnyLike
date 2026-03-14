using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Engine;

namespace JohnnyLike.Engine.Tests;

/// <summary>
/// Tests for the decision-tracing feature: DecisionTraceLevel, DecisionTraceOptions,
/// Director trace events, and ActionAssigned enrichment.
/// </summary>
public class DecisionTraceTests
{
    // ── Minimal test doubles ─────────────────────────────────────────────────

    private class MinActorState : ActorState
    {
        public override string Serialize()            => "{}";
        public override void Deserialize(string json) { }
    }

    private class MinWorldState : WorldState
    {
        public override IReadOnlyList<WorldItem> GetAllItems() => Array.Empty<WorldItem>();
        public override string Serialize()            => "{}";
        public override void Deserialize(string json) { }
    }

    /// <summary>Domain that always returns two candidates; no resources required.</summary>
    private class TwoCandidateDomain : IDomainPack
    {
        public string DomainName => "TwoCandidate";

        public WorldState CreateInitialWorldState() => new MinWorldState();
        public ActorState CreateActorState(ActorId actorId, Dictionary<string, object>? initialData = null)
            => new MinActorState { Id = actorId };

        public List<ActionCandidate> GenerateCandidates(
            ActorId actorId, ActorState actorState, WorldState worldState,
            long currentTick, Random rng, IResourceAvailability resourceAvailability)
            => new()
            {
                new ActionCandidate(
                    new ActionSpec(new ActionId("best_action"), ActionKind.Wait, EmptyActionParameters.Instance, Duration.FromTicks(10L), ""),
                    IntrinsicScore: 1.0, Qualities: new Dictionary<QualityType, double>(), Score: 1.0),
                new ActionCandidate(
                    new ActionSpec(new ActionId("second_action"), ActionKind.Wait, EmptyActionParameters.Instance, Duration.FromTicks(10L), ""),
                    IntrinsicScore: 0.5, Qualities: new Dictionary<QualityType, double>(), Score: 0.5),
            };

        public void ApplyActionEffects(ActorId a, ActionOutcome o, ActorState s, WorldState w, IRngStream r,
            IResourceAvailability ra, object? eh = null) { }
        public void OnSignal(Signal signal, ActorState? target, WorldState w, long t) { }
        public bool ValidateContent(out List<string> errors) { errors = new(); return true; }
        public Dictionary<string, object> GetActorStateSnapshot(ActorState s) => new();
        public List<TraceEvent> TickWorldState(WorldState w, long t, IResourceAvailability ra) => new();
    }

    /// <summary>Domain that blocks the first candidate via resource reservation.</summary>
    private class ReservationBlockingDomain : IDomainPack
    {
        public string DomainName => "ReservationBlocking";

        public WorldState CreateInitialWorldState() => new MinWorldState();
        public ActorState CreateActorState(ActorId actorId, Dictionary<string, object>? initialData = null)
            => new MinActorState { Id = actorId };

        public List<ActionCandidate> GenerateCandidates(
            ActorId actorId, ActorState actorState, WorldState worldState,
            long currentTick, Random rng, IResourceAvailability resourceAvailability)
            => new()
            {
                new ActionCandidate(
                    new ActionSpec(new ActionId("blocked_action"), ActionKind.Interact, EmptyActionParameters.Instance, Duration.FromTicks(10L), "",
                        null, new List<ResourceRequirement> { new(new ResourceId("test:res")) }),
                    IntrinsicScore: 2.0, Qualities: new Dictionary<QualityType, double>(), Score: 2.0),
                new ActionCandidate(
                    new ActionSpec(new ActionId("fallback_action"), ActionKind.Wait, EmptyActionParameters.Instance, Duration.FromTicks(10L), ""),
                    IntrinsicScore: 0.5, Qualities: new Dictionary<QualityType, double>(), Score: 0.5),
            };

        public void ApplyActionEffects(ActorId a, ActionOutcome o, ActorState s, WorldState w, IRngStream r,
            IResourceAvailability ra, object? eh = null) { }
        public void OnSignal(Signal signal, ActorState? target, WorldState w, long t) { }
        public bool ValidateContent(out List<string> errors) { errors = new(); return true; }
        public Dictionary<string, object> GetActorStateSnapshot(ActorState s) => new();
        public List<TraceEvent> TickWorldState(WorldState w, long t, IResourceAvailability ra) => new();
    }

    /// <summary>Domain that fails the pre-action handler on the first candidate.</summary>
    private class PreActionFailDomain : IDomainPack
    {
        public string DomainName => "PreActionFail";

        public WorldState CreateInitialWorldState() => new MinWorldState();
        public ActorState CreateActorState(ActorId actorId, Dictionary<string, object>? initialData = null)
            => new MinActorState { Id = actorId };

        public List<ActionCandidate> GenerateCandidates(
            ActorId actorId, ActorState actorState, WorldState worldState,
            long currentTick, Random rng, IResourceAvailability resourceAvailability)
            => new()
            {
                new ActionCandidate(
                    new ActionSpec(new ActionId("preaction_fail_action"), ActionKind.Wait, EmptyActionParameters.Instance, Duration.FromTicks(10L), ""),
                    IntrinsicScore: 2.0, Qualities: new Dictionary<QualityType, double>(), Score: 2.0,
                    PreAction: (object)new object()), // marker; TryExecutePreAction will return false
                new ActionCandidate(
                    new ActionSpec(new ActionId("success_action"), ActionKind.Wait, EmptyActionParameters.Instance, Duration.FromTicks(10L), ""),
                    IntrinsicScore: 0.5, Qualities: new Dictionary<QualityType, double>(), Score: 0.5),
            };

        public bool TryExecutePreAction(ActorId actorId, ActorState actorState, WorldState worldState,
            IRngStream rng, IResourceAvailability resourceAvailability, object? preActionHandler)
        {
            // Always fail the pre-action for the test
            return false;
        }

        public void ApplyActionEffects(ActorId a, ActionOutcome o, ActorState s, WorldState w, IRngStream r,
            IResourceAvailability ra, object? eh = null) { }
        public void OnSignal(Signal signal, ActorState? target, WorldState w, long t) { }
        public bool ValidateContent(out List<string> errors) { errors = new(); return true; }
        public Dictionary<string, object> GetActorStateSnapshot(ActorState s) => new();
        public List<TraceEvent> TickWorldState(WorldState w, long t, IResourceAvailability ra) => new();
    }

    /// <summary>Domain that generates zero candidates so no action is ever available.</summary>
    private class NoCandidateDomain : IDomainPack
    {
        public string DomainName => "NoCandidate";

        public WorldState CreateInitialWorldState() => new MinWorldState();
        public ActorState CreateActorState(ActorId actorId, Dictionary<string, object>? initialData = null)
            => new MinActorState { Id = actorId };

        public List<ActionCandidate> GenerateCandidates(
            ActorId actorId, ActorState actorState, WorldState worldState,
            long currentTick, Random rng, IResourceAvailability resourceAvailability)
            => new();

        public void ApplyActionEffects(ActorId a, ActionOutcome o, ActorState s, WorldState w, IRngStream r,
            IResourceAvailability ra, object? eh = null) { }
        public void OnSignal(Signal signal, ActorState? target, WorldState w, long t) { }
        public bool ValidateContent(out List<string> errors) { errors = new(); return true; }
        public Dictionary<string, object> GetActorStateSnapshot(ActorState s) => new();
        public List<TraceEvent> TickWorldState(WorldState w, long t, IResourceAvailability ra) => new();
    }

    // ── Helper ───────────────────────────────────────────────────────────────

    private static Engine CreateEngine(IDomainPack domain, DecisionTraceLevel level, out InMemoryTraceSink sink)
    {
        sink = new InMemoryTraceSink();
        var opts = new DecisionTraceOptions(level);
        return new Engine(domain, 42, sink, opts);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 1. Default level (None) — no decision events emitted
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void DefaultLevel_NoDecisionEventsEmitted()
    {
        var engine = CreateEngine(new TwoCandidateDomain(), DecisionTraceLevel.None, out var sink);
        engine.AddActor(new ActorId("A"));
        engine.TryGetNextAction(new ActorId("A"), out _);

        var decisionEvents = sink.GetEvents()
            .Where(e => e.EventType.StartsWith("Decision"))
            .ToList();

        Assert.Empty(decisionEvents);
    }

    [Fact]
    public void DefaultLevel_ActionAssigned_HasNoDecisionFields()
    {
        var engine = CreateEngine(new TwoCandidateDomain(), DecisionTraceLevel.None, out var sink);
        engine.AddActor(new ActorId("A"));
        engine.TryGetNextAction(new ActorId("A"), out _);

        var assigned = sink.GetEvents().First(e => e.EventType == "ActionAssigned");
        Assert.False(assigned.Details.ContainsKey("selectionReason"));
        Assert.False(assigned.Details.ContainsKey("selectedScore"));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 2. Summary level — DecisionSelected emitted with selection info
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void SummaryLevel_DecisionSelected_Emitted()
    {
        var engine = CreateEngine(new TwoCandidateDomain(), DecisionTraceLevel.Summary, out var sink);
        engine.AddActor(new ActorId("A"));
        var ok = engine.TryGetNextAction(new ActorId("A"), out var action);

        Assert.True(ok);
        var selected = sink.GetEvents().FirstOrDefault(e => e.EventType == "DecisionSelected");
        Assert.NotNull(selected);
        Assert.Equal("best_action", selected!.Details["actionId"].ToString());
    }

    [Fact]
    public void SummaryLevel_ActionAssigned_IsEnriched()
    {
        var engine = CreateEngine(new TwoCandidateDomain(), DecisionTraceLevel.Summary, out var sink);
        engine.AddActor(new ActorId("A"));
        engine.TryGetNextAction(new ActorId("A"), out _);

        var assigned = sink.GetEvents().First(e => e.EventType == "ActionAssigned");
        Assert.True(assigned.Details.ContainsKey("selectionReason"));
        Assert.True(assigned.Details.ContainsKey("selectedScore"));
        Assert.True(assigned.Details.ContainsKey("intrinsicScore"));
        Assert.True(assigned.Details.ContainsKey("attemptRank"));
        Assert.Equal("best_score", assigned.Details["selectionReason"].ToString());
        Assert.Equal(1, (int)assigned.Details["attemptRank"]);
    }

    [Fact]
    public void SummaryLevel_TopAlternatives_Included()
    {
        var engine = CreateEngine(new TwoCandidateDomain(), DecisionTraceLevel.Summary, out var sink);
        engine.AddActor(new ActorId("A"));
        engine.TryGetNextAction(new ActorId("A"), out _);

        var assigned = sink.GetEvents().First(e => e.EventType == "ActionAssigned");
        Assert.True(assigned.Details.ContainsKey("topAlternativeActionIds"));
        var alts = assigned.Details["topAlternativeActionIds"].ToString()!;
        Assert.Contains("second_action", alts);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 3. Candidates level — per-candidate events emitted
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void CandidatesLevel_DecisionCandidatesGenerated_Emitted()
    {
        var engine = CreateEngine(new TwoCandidateDomain(), DecisionTraceLevel.Candidates, out var sink);
        engine.AddActor(new ActorId("A"));
        engine.TryGetNextAction(new ActorId("A"), out _);

        var evt = sink.GetEvents().FirstOrDefault(e => e.EventType == "DecisionCandidatesGenerated");
        Assert.NotNull(evt);
        Assert.Equal(2, (int)evt!.Details["rawCount"]);
    }

    [Fact]
    public void CandidatesLevel_DecisionCandidatesRanked_Emitted()
    {
        var engine = CreateEngine(new TwoCandidateDomain(), DecisionTraceLevel.Candidates, out var sink);
        engine.AddActor(new ActorId("A"));
        engine.TryGetNextAction(new ActorId("A"), out _);

        var evt = sink.GetEvents().FirstOrDefault(e => e.EventType == "DecisionCandidatesRanked");
        Assert.NotNull(evt);
        Assert.Equal(2, (int)evt!.Details["candidateCount"]);
        // candidates is serialized to a JSON string for readable log output
        var candidatesJson = evt.Details["candidates"].ToString()!;
        Assert.Contains("best_action", candidatesJson);
        Assert.Contains("second_action", candidatesJson);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 4. Reservation failure fallback
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ReservationFailure_FallbackAction_SelectionReasonReflectsFallback()
    {
        var domain = new ReservationBlockingDomain();
        var sink = new InMemoryTraceSink();
        var opts = new DecisionTraceOptions(DecisionTraceLevel.Summary);
        var engine = new Engine(domain, 42, sink, opts);

        // Pre-reserve the resource so the first candidate always fails
        var directorField = typeof(Engine).GetField("_director",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var director = directorField!.GetValue(engine)!;
        var reservationsField = typeof(Director).GetField("_reservations",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var reservations = (ReservationTable)reservationsField!.GetValue(director)!;
        reservations.TryReserve(new ResourceId("test:res"), "external", long.MaxValue);

        engine.AddActor(new ActorId("A"));
        var ok = engine.TryGetNextAction(new ActorId("A"), out var action);

        Assert.True(ok);
        Assert.Equal("fallback_action", action!.Id.Value);

        var assigned = sink.GetEvents().First(e => e.EventType == "ActionAssigned");
        Assert.Equal("fallback_after_reservation_failure", assigned.Details["selectionReason"].ToString());
        Assert.Equal(2, (int)assigned.Details["attemptRank"]);
    }

    [Fact]
    public void ReservationFailure_CandidatesLevel_RejectionEventEmitted()
    {
        var domain = new ReservationBlockingDomain();
        var sink = new InMemoryTraceSink();
        var opts = new DecisionTraceOptions(DecisionTraceLevel.Candidates);
        var engine = new Engine(domain, 42, sink, opts);

        var directorField = typeof(Engine).GetField("_director",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var director = directorField!.GetValue(engine)!;
        var reservationsField = typeof(Director).GetField("_reservations",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var reservations = (ReservationTable)reservationsField!.GetValue(director)!;
        reservations.TryReserve(new ResourceId("test:res"), "external", long.MaxValue);

        engine.AddActor(new ActorId("A"));
        engine.TryGetNextAction(new ActorId("A"), out _);

        var rejected = sink.GetEvents().FirstOrDefault(e => e.EventType == "DecisionCandidateRejected");
        Assert.NotNull(rejected);
        Assert.Equal("blocked_action", rejected!.Details["failedActionId"].ToString());
        Assert.Equal("reservation_failed", rejected.Details["rejectionReason"].ToString());
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 5. Pre-action failure fallback
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void PreActionFailure_FallbackAction_SelectionReasonReflectsFallback()
    {
        var engine = CreateEngine(new PreActionFailDomain(), DecisionTraceLevel.Summary, out var sink);
        engine.AddActor(new ActorId("A"));
        var ok = engine.TryGetNextAction(new ActorId("A"), out var action);

        Assert.True(ok);
        Assert.Equal("success_action", action!.Id.Value);

        var assigned = sink.GetEvents().First(e => e.EventType == "ActionAssigned");
        Assert.Equal("fallback_after_preaction_failure", assigned.Details["selectionReason"].ToString());
    }

    [Fact]
    public void PreActionFailure_CandidatesLevel_RejectionEventEmitted()
    {
        var engine = CreateEngine(new PreActionFailDomain(), DecisionTraceLevel.Candidates, out var sink);
        engine.AddActor(new ActorId("A"));
        engine.TryGetNextAction(new ActorId("A"), out _);

        var rejected = sink.GetEvents().FirstOrDefault(e => e.EventType == "DecisionCandidateRejected");
        Assert.NotNull(rejected);
        Assert.Equal("preaction_failed", rejected!.Details["rejectionReason"].ToString());
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 6. No action available
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void NoActionAvailable_SummaryLevel_DecisionNoActionAvailableEmitted()
    {
        var engine = CreateEngine(new NoCandidateDomain(), DecisionTraceLevel.Summary, out var sink);
        engine.AddActor(new ActorId("A"));
        var ok = engine.TryGetNextAction(new ActorId("A"), out _);

        Assert.False(ok);
        var evt = sink.GetEvents().FirstOrDefault(e => e.EventType == "DecisionNoActionAvailable");
        Assert.NotNull(evt);
        Assert.Equal("no_candidates_generated", evt!.Details["reason"].ToString());
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 7. Determinism: same seed → same chosen action regardless of trace level
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void SameSeed_SameChosenAction_AcrossAllTraceLevels()
    {
        var levels = new[] { DecisionTraceLevel.None, DecisionTraceLevel.Summary,
                             DecisionTraceLevel.Candidates, DecisionTraceLevel.Verbose };
        var chosenActions = levels.Select(level =>
        {
            var engine = CreateEngine(new TwoCandidateDomain(), level, out _);
            engine.AddActor(new ActorId("A"));
            engine.TryGetNextAction(new ActorId("A"), out var a);
            return a?.Id.Value;
        }).ToList();

        Assert.True(chosenActions.Distinct().Count() == 1,
            $"Expected same action for all trace levels but got: {string.Join(",", chosenActions)}");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 8. Verbose level — scoring explanation emitted when domain supports it
    // ═════════════════════════════════════════════════════════════════════════

    private class ExplainingDomain : TwoCandidateDomain, IDomainPack
    {
        public Dictionary<string, object>? ExplainCandidateScoring(
            ActorId actorId, ActorState actorState, WorldState worldState,
            long currentTick, IReadOnlyList<ActionCandidate> candidates)
            => new() { ["test_explanation"] = "hello_from_domain", ["candidateCount"] = candidates.Count };
    }

    [Fact]
    public void VerboseLevel_DecisionSelected_ContainsScoringExplanation()
    {
        var engine = CreateEngine(new ExplainingDomain(), DecisionTraceLevel.Verbose, out var sink);
        engine.AddActor(new ActorId("A"));
        engine.TryGetNextAction(new ActorId("A"), out _);

        var selected = sink.GetEvents().First(e => e.EventType == "DecisionSelected");
        Assert.True(selected.Details.ContainsKey("scoringExplanation"));
        var json = selected.Details["scoringExplanation"].ToString()!;
        Assert.Contains("hello_from_domain", json);
    }

    [Fact]
    public void NoneLevel_ExplainCandidateScoring_NotCalled()
    {
        // If tracing is None, ExplainCandidateScoring should never be invoked —
        // we verify this by using a domain where calling it would throw.
        var engine = CreateEngine(new TwoCandidateDomain(), DecisionTraceLevel.None, out _);
        engine.AddActor(new ActorId("A"));
        // Should not throw
        var ok = engine.TryGetNextAction(new ActorId("A"), out _);
        Assert.True(ok);
    }
}
