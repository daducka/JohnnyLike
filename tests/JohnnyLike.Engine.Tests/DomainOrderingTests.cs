using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.Engine;

namespace JohnnyLike.Engine.Tests;

/// <summary>
/// Tests for Part A: domain-owned attempt ordering via IDomainPack.OrderCandidatesForSelection.
/// </summary>
public class DomainOrderingTests
{
    // ── Helper: private state/world types ────────────────────────────────────

    private class MinActorState : ActorState
    {
        public override string Serialize()   => "{}";
        public override void Deserialize(string json) { }
    }

    private class MinWorldState : WorldState
    {
        public override IReadOnlyList<WorldItem> GetAllItems() => Array.Empty<WorldItem>();
        public override string Serialize()   => "{}";
        public override void Deserialize(string json) { }
    }

    // ── Domain that does NOT override OrderCandidatesForSelection ────────────
    // Default interface implementation must therefore be used (returns list unchanged).

    private class DefaultOrderDomain : IDomainPack
    {
        public string DomainName => "DefaultOrder";

        public WorldState CreateInitialWorldState() => new MinWorldState();

        public ActorState CreateActorState(ActorId actorId, Dictionary<string, object>? initialData = null)
            => new MinActorState { Id = actorId };

        public List<ActionCandidate> GenerateCandidates(
            ActorId actorId, ActorState actorState, WorldState worldState,
            long currentTick, Random rng, IResourceAvailability resourceAvailability)
        {
            // Three candidates with distinct scores; the engine must pick the highest-scored
            // feasible one after variety penalty (none here) and deterministic sort.
            return new List<ActionCandidate>
            {
                new ActionCandidate(
                    new ActionSpec(new ActionId("low_score_action"),  ActionKind.Wait, EmptyActionParameters.Instance, Duration.FromTicks(10L), ""),
                    IntrinsicScore: 0.3,
                    Qualities: new Dictionary<QualityType, double>(),
                    Score: 0.3),
                new ActionCandidate(
                    new ActionSpec(new ActionId("best_action"),       ActionKind.Wait, EmptyActionParameters.Instance, Duration.FromTicks(10L), ""),
                    IntrinsicScore: 1.0,
                    Qualities: new Dictionary<QualityType, double>(),
                    Score: 1.0),
                new ActionCandidate(
                    new ActionSpec(new ActionId("middle_action"),     ActionKind.Wait, EmptyActionParameters.Instance, Duration.FromTicks(10L), ""),
                    IntrinsicScore: 0.6,
                    Qualities: new Dictionary<QualityType, double>(),
                    Score: 0.6),
            };
        }

        public void ApplyActionEffects(ActorId actorId, ActionOutcome outcome, ActorState actorState,
            WorldState worldState, IRngStream rng, IResourceAvailability resourceAvailability, object? effectHandler = null) { }

        public void OnSignal(Signal signal, ActorState? targetActor, WorldState worldState, long currentTick) { }

        public bool ValidateContent(out List<string> errors) { errors = new List<string>(); return true; }

        public Dictionary<string, object> GetActorStateSnapshot(ActorState actorState) => new();

        public List<TraceEvent> TickWorldState(WorldState worldState, long currentTick,
            IResourceAvailability resourceAvailability) => new();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 1: When the domain does NOT override OrderCandidatesForSelection,
    //         the Director must still select the best-scored feasible action.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DefaultOrdering_BestScoredFeasibleActionIsSelected()
    {
        var domain    = new DefaultOrderDomain();
        var traceSink = new InMemoryTraceSink();
        var engine    = new JohnnyLike.Engine.Engine(domain, 42, traceSink);

        engine.AddActor(new ActorId("TestActor"));

        var success = engine.TryGetNextAction(new ActorId("TestActor"), out var action);

        Assert.True(success);
        Assert.NotNull(action);
        // best_action has the highest score (1.0) and no resource requirements.
        Assert.Equal("best_action", action!.Id.Value);
    }
}
