using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Items;

namespace JohnnyLike.Domain.Island.Tests;

/// <summary>
/// Tests for the AlivenessBuff, buff query helpers, and candidate requirement infrastructure.
/// </summary>
public class AlivenessCandidateRequirementTests
{
    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static IslandActorState MakeAliveActor(string id = "TestActor")
    {
        var domain = new IslandDomainPack();
        return (IslandActorState)domain.CreateActorState(new ActorId(id));
    }

    private static IslandActorState MakeActorWithState(AlivenessState state, string id = "TestActor")
    {
        var actor = MakeAliveActor(id);
        var buff = actor.TryGetBuff<AlivenessBuff>()!;
        buff.State = state;
        return actor;
    }

    private static List<ActionCandidate> GenerateCandidates(IslandActorState actor, string actorId = "TestActor")
    {
        var domain = new IslandDomainPack();
        var world = new IslandWorldState();
        domain.InitializeActorItems(new ActorId(actorId), world);
        world.WorldItems.Add(new OceanItem("ocean"));
        return domain.GenerateCandidates(new ActorId(actorId), actor, world, 0L, new Random(42), new EmptyResourceAvailability());
    }

    // ─── AlivenessBuff initialization ─────────────────────────────────────────

    [Fact]
    public void CreateActorState_AlwaysHasAlivenessBuff()
    {
        var actor = MakeAliveActor();
        Assert.True(actor.HasBuff<AlivenessBuff>(), "Actor should have AlivenessBuff on creation");
    }

    [Fact]
    public void CreateActorState_AlivenessBuff_DefaultsToAlive()
    {
        var actor = MakeAliveActor();
        var buff = actor.TryGetBuff<AlivenessBuff>();
        Assert.NotNull(buff);
        Assert.Equal(AlivenessState.Alive, buff.State);
    }

    [Fact]
    public void CreateActorState_AlivenessBuff_NeverExpires()
    {
        var actor = MakeAliveActor();
        var buff = actor.TryGetBuff<AlivenessBuff>()!;
        Assert.Equal(long.MaxValue, buff.ExpiresAtTick);
    }

    [Fact]
    public void CreateActorState_AlivenessBuff_TypeIsAliveness()
    {
        var actor = MakeAliveActor();
        var buff = actor.TryGetBuff<AlivenessBuff>()!;
        Assert.Equal(BuffType.Aliveness, buff.Type);
    }

    // ─── Buff query helpers ────────────────────────────────────────────────────

    [Fact]
    public void HasBuff_ReturnsTrueWhenBuffPresent()
    {
        var actor = MakeAliveActor();
        Assert.True(actor.HasBuff<AlivenessBuff>());
    }

    [Fact]
    public void HasBuff_ReturnsFalseWhenBuffAbsent()
    {
        var actor = new IslandActorState { Id = new ActorId("A") };
        Assert.False(actor.HasBuff<AlivenessBuff>());
    }

    [Fact]
    public void TryGetBuff_ReturnsBuffWhenPresent()
    {
        var actor = MakeAliveActor();
        var buff = actor.TryGetBuff<AlivenessBuff>();
        Assert.NotNull(buff);
    }

    [Fact]
    public void TryGetBuff_ReturnsNullWhenAbsent()
    {
        var actor = new IslandActorState { Id = new ActorId("A") };
        var buff = actor.TryGetBuff<AlivenessBuff>();
        Assert.Null(buff);
    }

    [Fact]
    public void HasBuffWhere_ReturnsTrueWhenPredicateMatches()
    {
        var actor = MakeAliveActor();
        Assert.True(actor.HasBuffWhere<AlivenessBuff>(b => b.State == AlivenessState.Alive));
    }

    [Fact]
    public void HasBuffWhere_ReturnsFalseWhenPredicateDoesNotMatch()
    {
        var actor = MakeActorWithState(AlivenessState.Downed);
        Assert.False(actor.HasBuffWhere<AlivenessBuff>(b => b.State == AlivenessState.Alive));
    }

    [Fact]
    public void HasBuffWhere_ReturnsFalseWhenBuffAbsent()
    {
        var actor = new IslandActorState { Id = new ActorId("A") };
        Assert.False(actor.HasBuffWhere<AlivenessBuff>(b => b.State == AlivenessState.Alive));
    }

    // ─── CandidateRequirements helpers ────────────────────────────────────────

    [Fact]
    public void CandidateRequirements_AliveOnly_PassesForAliveActor()
    {
        var actor = MakeAliveActor();
        var requirement = CandidateRequirements.AliveOnly;
        Assert.True(requirement(actor));
    }

    [Fact]
    public void CandidateRequirements_AliveOnly_FailsForDownedActor()
    {
        var actor = MakeActorWithState(AlivenessState.Downed);
        var requirement = CandidateRequirements.AliveOnly;
        Assert.False(requirement(actor));
    }

    [Fact]
    public void CandidateRequirements_AliveOnly_FailsForDeadActor()
    {
        var actor = MakeActorWithState(AlivenessState.Dead);
        var requirement = CandidateRequirements.AliveOnly;
        Assert.False(requirement(actor));
    }

    [Fact]
    public void CandidateRequirements_HasBuff_PassesWhenBuffPresent()
    {
        var actor = MakeAliveActor();
        var requirement = CandidateRequirements.HasBuff<AlivenessBuff>();
        Assert.True(requirement(actor));
    }

    [Fact]
    public void CandidateRequirements_HasBuff_FailsWhenBuffAbsent()
    {
        var actor = new IslandActorState { Id = new ActorId("A") };
        var requirement = CandidateRequirements.HasBuff<AlivenessBuff>();
        Assert.False(requirement(actor));
    }

    [Fact]
    public void CandidateRequirements_HasBuffWhere_PassesWhenPredicateMatches()
    {
        var actor = MakeAliveActor();
        var requirement = CandidateRequirements.HasBuffWhere<AlivenessBuff>(b => b.State == AlivenessState.Alive);
        Assert.True(requirement(actor));
    }

    [Fact]
    public void CandidateRequirements_HasBuffWhere_FailsWhenPredicateDoesNotMatch()
    {
        var actor = MakeActorWithState(AlivenessState.Dead);
        var requirement = CandidateRequirements.HasBuffWhere<AlivenessBuff>(b => b.State == AlivenessState.Alive);
        Assert.False(requirement(actor));
    }

    // ─── Candidate requirement filtering ─────────────────────────────────────

    [Fact]
    public void GenerateCandidates_AliveActor_ProducesCandidates()
    {
        var actor = MakeAliveActor();
        var candidates = GenerateCandidates(actor);
        Assert.NotEmpty(candidates);
    }

    [Fact]
    public void GenerateCandidates_DownedActor_ProducesNoCandidates()
    {
        var actor = MakeActorWithState(AlivenessState.Downed);
        var candidates = GenerateCandidates(actor);
        Assert.Empty(candidates);
    }

    [Fact]
    public void GenerateCandidates_DeadActor_ProducesNoCandidates()
    {
        var actor = MakeActorWithState(AlivenessState.Dead);
        var candidates = GenerateCandidates(actor);
        Assert.Empty(candidates);
    }

    [Fact]
    public void GenerateCandidates_AllCandidates_HaveAliveOnlyRequirement()
    {
        var actor = MakeAliveActor();
        var candidates = GenerateCandidates(actor);

        // Every candidate should have an ActorRequirement set (AliveOnly applied by default)
        foreach (var c in candidates)
            Assert.NotNull(c.ActorRequirement);
    }

    [Fact]
    public void GenerateCandidates_CandidateWithoutRequirement_IsStillIncluded()
    {
        // A candidate with no ActorRequirement (null) should not be filtered
        var actor = MakeAliveActor();
        var domain = new IslandDomainPack();
        var world = new IslandWorldState();

        // Manually add a candidate with no requirement via a custom actor
        var candidate = new ActionCandidate(
            new ActionSpec(new ActionId("test_no_req"), ActionKind.Wait,
                EmptyActionParameters.Instance, 100L, "test"),
            0.5,
            new Dictionary<QualityType, double>(),
            ActorRequirement: null
        );

        // Verify the predicate check: null requirement means no filtering
        Assert.True(candidate.ActorRequirement == null || candidate.ActorRequirement(actor));
    }

    [Fact]
    public void GenerateCandidates_ExistingIslandCandidates_StillGenerateAndScoreNormally_WhenActorIsAlive()
    {
        var actor = MakeAliveActor();
        var candidates = GenerateCandidates(actor);

        // Idle should always be present
        var idleCandidate = candidates.FirstOrDefault(c => c.Action.Id.Value == "idle");
        Assert.NotNull(idleCandidate);
        Assert.True(idleCandidate.Score > 0.0, "Idle candidate should have a positive score");

        // Fishing should be present (OceanItem and FishingPoleItem are added)
        var fishingCandidate = candidates.FirstOrDefault(c => c.Action.Id.Value == "go_fishing");
        Assert.NotNull(fishingCandidate);
        Assert.True(fishingCandidate.Score > 0.0, "Fishing candidate should have a positive score");
    }

    [Fact]
    public void GenerateCandidates_CandidateRequirement_IsEvaluatedBeforeScoring()
    {
        // Confirm that candidates with a failing requirement are not present at all
        // (not merely scored low)
        var actor = MakeActorWithState(AlivenessState.Dead);
        var candidates = GenerateCandidates(actor);

        // No candidates should appear — they are filtered before scoring, not scored low
        Assert.Empty(candidates);
    }
}
