using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.Domain.Island.Metabolism;

namespace JohnnyLike.Domain.Island.Tests;

/// <summary>
/// Unit tests for <see cref="MetabolicBuff"/> behaviour and the tickable buff lifecycle.
/// </summary>
public class MetabolicBuffTests
{
    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static (IslandActorState actor, IslandWorldState world) MakeActor(
        double satiety = 100.0, double energy = 100.0)
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actorState = (IslandActorState)domain.CreateActorState(actorId,
            new Dictionary<string, object> { ["satiety"] = satiety, ["energy"] = energy });
        var worldState = new IslandWorldState();
        return (actorState, worldState);
    }

    private static void Tick(
        IslandActorState actor,
        IslandWorldState world,
        long toTick)
    {
        var domain = new IslandDomainPack();
        var actors = new Dictionary<ActorId, ActorState> { [actor.Id] = actor };
        domain.TickWorldState(world, actors, toTick, new EmptyResourceAvailability());
    }

    // ─── Presence of MetabolicBuff ────────────────────────────────────────────

    [Fact]
    public void CreateActorState_AlwaysHasMetabolicBuff()
    {
        var domain = new IslandDomainPack();
        var actorState = (IslandActorState)domain.CreateActorState(new ActorId("A"));

        Assert.Contains(actorState.ActiveBuffs, b => b is MetabolicBuff);
    }

    [Fact]
    public void MetabolicBuff_DefaultIntensity_IsLight()
    {
        var domain = new IslandDomainPack();
        var actorState = (IslandActorState)domain.CreateActorState(new ActorId("A"));
        var buff = actorState.ActiveBuffs.OfType<MetabolicBuff>().Single();

        Assert.Equal(MetabolicIntensity.Light, buff.Intensity);
    }

    [Fact]
    public void MetabolicBuff_NeverExpires()
    {
        var domain = new IslandDomainPack();
        var actorState = (IslandActorState)domain.CreateActorState(new ActorId("A"));
        var buff = actorState.ActiveBuffs.OfType<MetabolicBuff>().Single();

        Assert.Equal(long.MaxValue, buff.ExpiresAtTick);
    }

    // ─── OnTick effects ───────────────────────────────────────────────────────

    [Fact]
    public void OnTick_LightIntensity_DrainsSatiety_AtBasalRate()
    {
        var (actor, world) = MakeActor(satiety: 100.0, energy: 100.0);

        // Tick 72 000 engine-ticks = 3600 sim-seconds (1 hour).
        // BasalKcalPerSecond ≈ 0.02778 kcal/s → basal burn = 100 kcal → ~5 Satiety.
        // Plus Satiety→Energy conversion (0.5×basal * 3600 = 50 kcal → ~2.5 pts) if Energy < 100.
        // Since Energy starts at 100, no conversion fires; total drop ≈ 5 pts.
        // Use a generous band to avoid brittleness.
        Tick(actor, world, 72_000L); // 1 hour

        double drop = 100.0 - actor.Satiety;
        Assert.True(actor.Satiety < 100.0,
            $"Satiety should decrease after a tick at rest, got {actor.Satiety:F4}");
        Assert.InRange(drop, 3.0, 10.0);
    }

    [Fact]
    public void OnTick_HeavyIntensity_DrainsEnergyFasterThanLight()
    {
        var (actorHeavy, worldHeavy) = MakeActor(energy: 100.0);
        var (actorLight, worldLight) = MakeActor(energy: 100.0);

        // Set heavy intensity on the first actor.
        actorHeavy.ActiveBuffs.OfType<MetabolicBuff>().Single().Intensity = MetabolicIntensity.Heavy;

        Tick(actorHeavy, worldHeavy, 72_000L); // 1 hour
        Tick(actorLight, worldLight, 72_000L);

        Assert.True(actorHeavy.Energy < actorLight.Energy,
            $"Heavy activity should drain Energy more than Light. Heavy={actorHeavy.Energy:F2}, Light={actorLight.Energy:F2}");
    }

    [Fact]
    public void OnTick_SleepingIntensity_IncreasesEnergy()
    {
        var (actor, world) = MakeActor(energy: 30.0);
        actor.ActiveBuffs.OfType<MetabolicBuff>().Single().Intensity = MetabolicIntensity.Sleeping;

        Tick(actor, world, 72_000L); // 1 hour

        Assert.True(actor.Energy > 30.0,
            $"Sleeping should restore Energy, got {actor.Energy:F2}");
    }

    [Fact]
    public void OnTick_StatsNeverExceedBounds()
    {
        var (actor, world) = MakeActor(satiety: 0.0, energy: 100.0);

        // Very long tick at Heavy intensity — stats should clamp at 0.
        actor.ActiveBuffs.OfType<MetabolicBuff>().Single().Intensity = MetabolicIntensity.Heavy;
        Tick(actor, world, 200_000L); // 10 000 sim-seconds

        Assert.True(actor.Satiety >= 0.0, $"Satiety clamped at 0, got {actor.Satiety}");
        Assert.True(actor.Energy  >= 0.0, $"Energy clamped at 0, got {actor.Energy}");
        Assert.True(actor.Satiety <= 100.0);
        Assert.True(actor.Energy  <= 100.0);
    }

    // ─── Serialization round-trip ─────────────────────────────────────────────

    [Fact]
    public void MetabolicBuff_SerializeDeserialize_PreservesIntensityAndLastTick()
    {
        var domain = new IslandDomainPack();
        var actorId = new ActorId("TestActor");
        var actorState = (IslandActorState)domain.CreateActorState(actorId);
        var buff = actorState.ActiveBuffs.OfType<MetabolicBuff>().Single();
        buff.Intensity = MetabolicIntensity.Heavy;
        buff.LastTick  = 9999L;

        var json = actorState.Serialize();
        var restored = new IslandActorState();
        restored.Deserialize(json);

        var restoredBuff = restored.ActiveBuffs.OfType<MetabolicBuff>().FirstOrDefault();
        Assert.NotNull(restoredBuff);
        Assert.Equal(MetabolicIntensity.Heavy, restoredBuff.Intensity);
        Assert.Equal(9999L, restoredBuff.LastTick);
    }

    // ─── ApplyActionEffects resets intensity ──────────────────────────────────

    [Fact]
    public void ApplyActionEffects_ResetsBuff_ToLightIntensity()
    {
        var domain = new IslandDomainPack();
        var actorId  = new ActorId("TestActor");
        var actorState = (IslandActorState)domain.CreateActorState(actorId);
        var worldState = new IslandWorldState();

        // Manually set Heavy intensity (simulating a PreAction for swim).
        actorState.ActiveBuffs.OfType<MetabolicBuff>().Single().Intensity = MetabolicIntensity.Heavy;

        // Apply any action to trigger the reset.
        var outcome = new ActionOutcome(new ActionId("idle"), ActionOutcomeType.Success, Duration.FromTicks(200L), null);
        domain.ApplyActionEffects(actorId, outcome, actorState, worldState,
            new RandomRngStream(new Random(42)), new EmptyResourceAvailability());

        Assert.Equal(MetabolicIntensity.Light,
            actorState.ActiveBuffs.OfType<MetabolicBuff>().Single().Intensity);
    }
}
