using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.Domain.Island.Candidates;
using SimEngine = JohnnyLike.Engine.Engine;

namespace JohnnyLike.Domain.Island.Tests;

/// <summary>
/// Tests for the refactored living actor base and humanoid extension:
/// <see cref="LivingActorState"/> base, <see cref="PresenceState"/> enum,
/// stash tick hook, and stashed actor exclusion from the decision loop.
/// </summary>
public class LivingActorBaseTests
{
    // ─── helpers ──────────────────────────────────────────────────────────────

    private static IslandActorState MakeActor(
        double satiety = 80.0,
        double energy  = 80.0,
        double morale  = 60.0,
        double health  = 100.0)
    {
        var domain  = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actor   = (IslandActorState)domain.CreateActorState(actorId, new Dictionary<string, object>
        {
            ["satiety"] = satiety,
            ["energy"]  = energy,
            ["morale"]  = morale
        });
        actor.Health = health;
        return actor;
    }

    // ─── Hierarchy ────────────────────────────────────────────────────────────

    [Fact]
    public void IslandActorState_InheritsFrom_LivingActorState()
    {
        var actor = MakeActor();
        Assert.IsAssignableFrom<LivingActorState>(actor);
    }

    [Fact]
    public void LivingActorState_InheritsFrom_ActorState()
    {
        var actor = MakeActor();
        Assert.IsAssignableFrom<ActorState>(actor);
    }

    // ─── Stats on base class ─────────────────────────────────────────────────

    [Fact]
    public void LivingActorState_HasDndStats()
    {
        var domain  = new IslandDomainPack();
        var actorId = new ActorId("StatActor");
        var actor   = (LivingActorState)domain.CreateActorState(actorId, new Dictionary<string, object>
        {
            ["STR"] = 14,
            ["DEX"] = 12,
            ["CON"] = 16,
            ["INT"] = 10,
            ["WIS"] = 13,
            ["CHA"] = 8
        });

        Assert.Equal(14, actor.STR);
        Assert.Equal(12, actor.DEX);
        Assert.Equal(16, actor.CON);
        Assert.Equal(10, actor.INT);
        Assert.Equal(13, actor.WIS);
        Assert.Equal(8,  actor.CHA);
    }

    [Fact]
    public void LivingActorState_HasPhysiologicalVitals()
    {
        var actor = MakeActor(satiety: 70.0, energy: 65.0, morale: 55.0, health: 90.0);

        Assert.Equal(70.0, actor.Satiety);
        Assert.Equal(65.0, actor.Energy);
        Assert.Equal(55.0, actor.Morale);
        Assert.Equal(90.0, actor.Health);
    }

    [Fact]
    public void LivingActorState_VitalsClampedToValidRange()
    {
        var actor = MakeActor();
        actor.Satiety = 200.0;
        actor.Energy  = -50.0;
        actor.Morale  = 150.0;
        actor.Health  = -10.0;

        Assert.Equal(100.0, actor.Satiety);
        Assert.Equal(0.0,   actor.Energy);
        Assert.Equal(100.0, actor.Morale);
        Assert.Equal(0.0,   actor.Health);
    }

    // ─── Skill checks on base class ──────────────────────────────────────────

    [Fact]
    public void LivingActorState_GetSkillModifier_UsesAbilityScores()
    {
        var domain  = new IslandDomainPack();
        var actorId = new ActorId("SkillActor");
        var actor   = (LivingActorState)domain.CreateActorState(actorId, new Dictionary<string, object>
        {
            ["STR"] = 16, // +3 modifier
            ["DEX"] = 14, // +2 modifier
            ["WIS"] = 12  // +1 modifier
        });

        // FishingSkill = DEX modifier + WIS modifier = +2 + +1 = +3
        Assert.Equal(3, actor.GetSkillModifier(SkillType.Fishing));
        // AthleticsSkill = STR modifier = +3
        Assert.Equal(3, actor.GetSkillModifier(SkillType.Athletics));
    }

    [Fact]
    public void LivingActorState_HasBuff_WorksOnBaseClass()
    {
        var actor = MakeActor();
        // AlivenessBuff is added during CreateActorState
        Assert.True(actor.HasBuff<AlivenessBuff>());
    }

    [Fact]
    public void LivingActorState_TryGetBuff_ReturnsBuffOrNull()
    {
        var actor = MakeActor();
        var aliveness = actor.TryGetBuff<AlivenessBuff>();
        Assert.NotNull(aliveness);
        Assert.Equal(AlivenessState.Alive, aliveness!.State);
    }

    // ─── PresenceState ────────────────────────────────────────────────────────

    [Fact]
    public void ActorState_DefaultPresenceState_IsActive()
    {
        var actor = MakeActor();
        Assert.Equal(PresenceState.Active, actor.PresenceState);
    }

    [Fact]
    public void ActorState_CanBeSetToStashed()
    {
        var actor = MakeActor();
        actor.PresenceState = PresenceState.Stashed;
        Assert.Equal(PresenceState.Stashed, actor.PresenceState);
    }

    // ─── Stash tick hook ─────────────────────────────────────────────────────

    [Fact]
    public void LivingActorState_OnStashTick_IsVirtualNoOp()
    {
        // Default implementation should not throw and returns without side effects.
        var actor = MakeActor();
        actor.OnStashTick(1000L); // should not throw
    }

    [Fact]
    public void IslandDomainPack_TickWorldState_CallsOnStashTick_ForStashedActor()
    {
        var domain  = new IslandDomainPack();
        var actorId = new ActorId("StashActor");
        var actor   = (IslandActorState)domain.CreateActorState(actorId);
        var world   = (IslandWorldState)domain.CreateInitialWorldState();

        actor.PresenceState = PresenceState.Stashed;

        // Record Health before ticking — stashed actors don't run VitalityBuff,
        // so even with critically low vitals there should be no health change.
        actor.Satiety = 0.0;
        actor.Energy  = 0.0;
        actor.Morale  = 0.0;
        var healthBefore = actor.Health;

        var actors = new Dictionary<ActorId, ActorState> { [actorId] = actor };
        long ticks = 10 * EngineConstants.TickHz; // 10 sim-seconds
        domain.TickWorldState(world, actors, ticks, new EmptyResourceAvailability());

        // Health unchanged because VitalityBuff was not ticked for the stashed actor.
        Assert.Equal(healthBefore, actor.Health);
    }

    [Fact]
    public void IslandDomainPack_TickWorldState_RunsNormalTick_ForActiveActor()
    {
        var domain  = new IslandDomainPack();
        var actorId = new ActorId("ActiveActor");
        var actor   = (IslandActorState)domain.CreateActorState(actorId, new Dictionary<string, object>
        {
            ["satiety"] = 0.0,
            ["energy"]  = 0.0,
            ["morale"]  = 0.0
        });
        actor.Health = 100.0;
        var world = (IslandWorldState)domain.CreateInitialWorldState();

        // Active is the default, but make it explicit.
        actor.PresenceState = PresenceState.Active;

        var actors = new Dictionary<ActorId, ActorState> { [actorId] = actor };
        long ticks = 10 * EngineConstants.TickHz; // 10 sim-seconds with critical stats
        domain.TickWorldState(world, actors, ticks, new EmptyResourceAvailability());

        // VitalityBuff should have applied starvation/exhaustion/psyche damage.
        Assert.True(actor.Health < 100.0, "Health should decrease under critical stats for active actor.");
    }

    // ─── Engine integration ───────────────────────────────────────────────────

    [Fact]
    public void Engine_TryGetNextAction_ReturnsFalse_ForStashedActor()
    {
        var domain = new IslandDomainPack();
        var engine = new SimEngine(domain, seed: 42);

        engine.AddActor(new ActorId("StashActor"));
        var actor = (IslandActorState)engine.Actors[new ActorId("StashActor")];
        actor.PresenceState = PresenceState.Stashed;

        var result = engine.TryGetNextAction(new ActorId("StashActor"), out var action);

        Assert.False(result);
        Assert.Null(action);
    }

    [Fact]
    public void Engine_TryGetNextAction_ReturnsTrue_ForActiveActor()
    {
        var domain = new IslandDomainPack();
        var engine = new SimEngine(domain, seed: 42);

        engine.AddActor(new ActorId("ActiveActor"));
        var actorId = new ActorId("ActiveActor");
        domain.InitializeActorItems(actorId, (IslandWorldState)engine.WorldState);
        engine.AdvanceTicks(0); // ensure world is initialized

        var result = engine.TryGetNextAction(actorId, out var action);

        Assert.True(result);
        Assert.NotNull(action);
    }

    // ─── Serialization round-trip ─────────────────────────────────────────────

    [Fact]
    public void IslandActorState_SerializeDeserialize_PreservesPresenceState()
    {
        var domain  = new IslandDomainPack();
        var actorId = new ActorId("SerializeActor");
        var actor   = (IslandActorState)domain.CreateActorState(actorId);
        actor.PresenceState = PresenceState.Stashed;

        var json = actor.Serialize();
        var restored = (IslandActorState)domain.CreateActorState(actorId);
        restored.Deserialize(json);

        Assert.Equal(PresenceState.Stashed, restored.PresenceState);
    }

    [Fact]
    public void IslandActorState_SerializeDeserialize_DefaultPresenceStateIsActive()
    {
        var domain  = new IslandDomainPack();
        var actorId = new ActorId("SerializeActor");
        var actor   = (IslandActorState)domain.CreateActorState(actorId);

        var json = actor.Serialize();
        var restored = (IslandActorState)domain.CreateActorState(actorId);
        restored.Deserialize(json);

        Assert.Equal(PresenceState.Active, restored.PresenceState);
    }
}
