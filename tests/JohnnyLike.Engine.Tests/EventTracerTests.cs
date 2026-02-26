using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Engine;

namespace JohnnyLike.Engine.Tests;

/// <summary>
/// Tests for the EventTracer implementation and the NarrationBeat pipeline in Engine.
/// </summary>
public class EventTracerTests
{
    // ── Basic push/drain behaviour ────────────────────────────────────────────

    [Fact]
    public void Beat_WithNoPhasePushed_UsesWorldTick()
    {
        var tracer = new EventTracer();
        tracer.Beat("Something happened.");

        var beats = tracer.Drain();
        Assert.Single(beats);
        Assert.Equal(TracePhase.WorldTick, beats[0].Phase);
        Assert.Equal("Something happened.", beats[0].Text);
    }

    [Fact]
    public void PushPhase_SetsCurrentPhase()
    {
        var tracer = new EventTracer();
        using (tracer.PushPhase(TracePhase.ActionCompleted))
        {
            tracer.Beat("Action done.");
        }

        var beats = tracer.Drain();
        Assert.Single(beats);
        Assert.Equal(TracePhase.ActionCompleted, beats[0].Phase);
    }

    [Fact]
    public void PushPhase_Nested_RestoresPreviousOnDispose()
    {
        var tracer = new EventTracer();
        using (tracer.PushPhase(TracePhase.ActionExecuting))
        {
            tracer.Beat("executing");

            using (tracer.PushPhase(TracePhase.ActionCompleted))
            {
                tracer.Beat("completed");
            }

            // Back to outer scope
            tracer.Beat("still executing");
        }

        var beats = tracer.Drain();
        Assert.Equal(3, beats.Count);
        Assert.Equal(TracePhase.ActionExecuting, beats[0].Phase);
        Assert.Equal(TracePhase.ActionCompleted, beats[1].Phase);
        Assert.Equal(TracePhase.ActionExecuting, beats[2].Phase);
    }

    [Fact]
    public void Drain_ClearsBuffer()
    {
        var tracer = new EventTracer();
        tracer.Beat("first");

        var beats1 = tracer.Drain();
        Assert.Single(beats1);

        var beats2 = tracer.Drain();
        Assert.Empty(beats2);
    }

    [Fact]
    public void Beat_StoresSubjectIdPriorityAndActorId()
    {
        var tracer = new EventTracer();
        tracer.Beat("The tide turns.", subjectId: "beach:tide", priority: 25, actorId: "Johnny");

        var beat = tracer.Drain()[0];
        Assert.Equal("beach:tide", beat.SubjectId);
        Assert.Equal(25, beat.Priority);
        Assert.Equal("Johnny", beat.ActorId);
    }

    [Fact]
    public void Drain_EmptyTracer_ReturnsEmptyList()
    {
        var tracer = new EventTracer();
        Assert.Empty(tracer.Drain());
    }

    // ── Engine integration: NarrationBeat TraceEvents ─────────────────────────

    [Fact]
    public void Engine_WorldTick_EmitsNarrationBeatEvents()
    {
        // Arrange: use a domain that emits a beat during TickWorldState
        var domain = new BeatEmittingDomainPack();
        var traceSink = new InMemoryTraceSink();
        var engine = new Engine(domain, 42, traceSink);

        // Act
        engine.AdvanceTicks(20L);

        // Assert: a NarrationBeat event should appear in the trace
        var events = traceSink.GetEvents();
        var beatEvents = events.Where(e => e.EventType == "NarrationBeat").ToList();
        Assert.NotEmpty(beatEvents);
        Assert.True(beatEvents[0].Details.ContainsKey("text"));
        Assert.Equal("World ticked.", beatEvents[0].Details["text"]);
    }

    [Fact]
    public void Engine_ActionComplete_EmitsNarrationBeatEvents()
    {
        // Arrange
        var domain = new BeatEmittingDomainPack();
        var traceSink = new InMemoryTraceSink();
        var engine = new Engine(domain, 42, traceSink);
        engine.AddActor(new ActorId("TestActor"));

        engine.TryGetNextAction(new ActorId("TestActor"), out var action);
        Assert.NotNull(action);

        // Act
        engine.ReportActionComplete(
            new ActorId("TestActor"),
            new ActionOutcome(action!.Id, ActionOutcomeType.Success, 20L));

        // Assert: a NarrationBeat event should appear
        var events = traceSink.GetEvents();
        var beatEvents = events.Where(e => e.EventType == "NarrationBeat").ToList();
        Assert.NotEmpty(beatEvents);
        Assert.Equal("Effect applied.", beatEvents[0].Details["text"]);
    }

    /// <summary>
    /// Minimal domain pack that emits one NarrationBeat during TickWorldState
    /// and another during ApplyActionEffects.
    /// </summary>
    private class BeatEmittingDomainPack : IDomainPack
    {
        public string DomainName => "BeatTest";

        public WorldState CreateInitialWorldState() => new TestWorldState();

        public ActorState CreateActorState(ActorId actorId, Dictionary<string, object>? initialData = null)
            => new TestActorState { Id = actorId };

        public List<ActionCandidate> GenerateCandidates(
            ActorId actorId, ActorState actorState, WorldState worldState, long currentTick, Random rng, IResourceAvailability resourceAvailability)
            => new List<ActionCandidate>
            {
                new ActionCandidate(
                    new ActionSpec(new ActionId("idle"), ActionKind.Wait, EmptyActionParameters.Instance, 20L),
                    1.0,
                    EffectHandler: new Action<EffectContext<TestActorState, TestWorldState>>(ctx =>
                        ctx.Tracer.Beat("Effect applied."))
                )
            };

        public void ApplyActionEffects(
            ActorId actorId, ActionOutcome outcome, ActorState actorState, WorldState worldState,
            IRngStream rng, IResourceAvailability resourceAvailability, object? effectHandler = null)
        {
            if (effectHandler is Action<EffectContext<TestActorState, TestWorldState>> handler)
            {
                handler(new EffectContext<TestActorState, TestWorldState>
                {
                    ActorId = actorId,
                    Outcome = outcome,
                    Actor = (TestActorState)actorState,
                    World = (TestWorldState)worldState,
                    Rng = rng,
                    Reservations = resourceAvailability,
                    Tracer = worldState.Tracer
                });
            }
        }

        public List<TraceEvent> TickWorldState(WorldState worldState, long currentTick, IResourceAvailability resourceAvailability)
        {
            worldState.Tracer.Beat("World ticked.");
            return new List<TraceEvent>();
        }

        public void OnSignal(Signal signal, ActorState? targetActor, WorldState worldState, long currentTick) { }
        public List<SceneTemplate> GetSceneTemplates() => new List<SceneTemplate>();
        public bool ValidateContent(out List<string> errors) { errors = new(); return true; }
        public Dictionary<string, object> GetActorStateSnapshot(ActorState actorState) => new();
    }

    private class TestActorState : ActorState
    {
        public override string Serialize() => "{}";
        public override void Deserialize(string json) { }
    }

    private class TestWorldState : WorldState
    {
        public override string Serialize() => "{}";
        public override void Deserialize(string json) { }
    }
}
