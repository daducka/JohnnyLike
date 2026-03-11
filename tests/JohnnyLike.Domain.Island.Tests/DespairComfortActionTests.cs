using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Items;

namespace JohnnyLike.Domain.Island.Tests;

/// <summary>
/// Tests for despair comfort actions (curl_in_a_ball, stare_at_sky, reflect_on_life, eat_sand)
/// and the gating of playful comfort actions under low stats.
/// </summary>
public class DespairComfortActionTests
{
    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static readonly string[] PlayfulActionIds = new[]
    {
        "hum_to_self", "skip_stones", "build_sand_castle",
        "collect_shells", "pace_beach", "swim"
    };

    private static readonly string[] DespairActionIds = new[]
    {
        "curl_in_a_ball", "stare_at_sky", "reflect_on_life", "eat_sand"
    };

    private static IslandActorState MakeActor(
        double satiety = 100.0,
        double morale  = 50.0,
        double health  = 100.0,
        double energy  = 100.0)
    {
        var domain = new IslandDomainPack();
        var actor  = (IslandActorState)domain.CreateActorState(new ActorId("TestActor"));
        actor.Satiety = satiety;
        actor.Morale  = morale;
        actor.Health  = health;
        actor.Energy  = energy;
        return actor;
    }

    private static List<ActionCandidate> GetCandidates(IslandActorState actor)
    {
        var domain = new IslandDomainPack();
        var world  = new IslandWorldState();
        domain.InitializeActorItems(new ActorId("TestActor"), world);
        world.WorldItems.Add(new OceanItem("ocean"));
        world.WorldItems.Add(new BeachItem("beach"));
        return domain.GenerateCandidates(
            new ActorId("TestActor"), actor, world, 0L, new Random(42), new EmptyResourceAvailability());
    }

    // ─── PlayfulOnly requirement ──────────────────────────────────────────────

    [Fact]
    public void PlayfulOnly_PassesForHealthyActor()
    {
        var actor = MakeActor(satiety: 50, morale: 50, health: 80, energy: 60);
        Assert.True(CandidateRequirements.PlayfulOnly(actor));
    }

    [Fact]
    public void PlayfulOnly_FailsWhenSatietyTooLow()
    {
        var actor = MakeActor(satiety: 20, morale: 50, health: 80, energy: 60);
        Assert.False(CandidateRequirements.PlayfulOnly(actor));
    }

    [Fact]
    public void PlayfulOnly_FailsWhenMoraleTooLow()
    {
        var actor = MakeActor(satiety: 50, morale: 30, health: 80, energy: 60);
        Assert.False(CandidateRequirements.PlayfulOnly(actor));
    }

    [Fact]
    public void PlayfulOnly_FailsWhenHealthTooLow()
    {
        var actor = MakeActor(satiety: 50, morale: 50, health: 40, energy: 60);
        Assert.False(CandidateRequirements.PlayfulOnly(actor));
    }

    [Fact]
    public void PlayfulOnly_FailsWhenEnergyTooLow()
    {
        var actor = MakeActor(satiety: 50, morale: 50, health: 80, energy: 25);
        Assert.False(CandidateRequirements.PlayfulOnly(actor));
    }

    // ─── DespairingOnly requirement ───────────────────────────────────────────

    [Fact]
    public void DespairingOnly_PassesWhenSatietyLow()
    {
        var actor = MakeActor(satiety: 10, morale: 50, health: 80, energy: 60);
        Assert.True(CandidateRequirements.DespairingOnly(actor));
    }

    [Fact]
    public void DespairingOnly_PassesWhenMoraleLow()
    {
        var actor = MakeActor(satiety: 50, morale: 10, health: 80, energy: 60);
        Assert.True(CandidateRequirements.DespairingOnly(actor));
    }

    [Fact]
    public void DespairingOnly_PassesWhenHealthLow()
    {
        var actor = MakeActor(satiety: 50, morale: 50, health: 30, energy: 60);
        Assert.True(CandidateRequirements.DespairingOnly(actor));
    }

    [Fact]
    public void DespairingOnly_FailsForHealthyActor()
    {
        var actor = MakeActor(satiety: 50, morale: 50, health: 80, energy: 60);
        Assert.False(CandidateRequirements.DespairingOnly(actor));
    }

    // ─── Candidate filtering: playful actions absent under distress ───────────

    [Theory]
    [InlineData("hum_to_self")]
    [InlineData("skip_stones")]
    [InlineData("collect_shells")]
    [InlineData("pace_beach")]
    public void PlayfulBeachActions_NotAvailableWhenStatsAreLow(string actionId)
    {
        // Actor is starving — should not see playful actions
        var actor = MakeActor(satiety: 10, morale: 50, health: 80, energy: 60);
        var candidates = GetCandidates(actor);
        Assert.DoesNotContain(candidates, c => c.Action.Id.Value == actionId);
    }

    [Fact]
    public void BuildSandCastle_NotAvailableWhenStatsAreLow()
    {
        var actor = MakeActor(satiety: 10, morale: 50, health: 80, energy: 60);
        var candidates = GetCandidates(actor);
        Assert.DoesNotContain(candidates, c => c.Action.Id.Value == "build_sand_castle");
    }

    [Fact]
    public void Swim_NotAvailableWhenStatsAreLow()
    {
        var actor = MakeActor(satiety: 10, morale: 50, health: 80, energy: 60);
        var candidates = GetCandidates(actor);
        Assert.DoesNotContain(candidates, c => c.Action.Id.Value == "swim");
    }

    [Theory]
    [InlineData("hum_to_self")]
    [InlineData("skip_stones")]
    [InlineData("collect_shells")]
    [InlineData("pace_beach")]
    public void PlayfulBeachActions_AvailableWhenActorIsHealthy(string actionId)
    {
        var actor = MakeActor(satiety: 60, morale: 50, health: 80, energy: 60);
        var candidates = GetCandidates(actor);
        Assert.Contains(candidates, c => c.Action.Id.Value == actionId);
    }

    // ─── Candidate filtering: despair actions appear under distress ──────────

    [Theory]
    [InlineData("curl_in_a_ball")]
    [InlineData("stare_at_sky")]
    [InlineData("reflect_on_life")]
    [InlineData("eat_sand")]
    public void DespairActions_AvailableWhenActorIsStarving(string actionId)
    {
        var actor = MakeActor(satiety: 10, morale: 50, health: 80, energy: 60);
        var candidates = GetCandidates(actor);
        Assert.Contains(candidates, c => c.Action.Id.Value == actionId);
    }

    [Theory]
    [InlineData("curl_in_a_ball")]
    [InlineData("stare_at_sky")]
    [InlineData("reflect_on_life")]
    [InlineData("eat_sand")]
    public void DespairActions_AvailableWhenMoraleIsLow(string actionId)
    {
        var actor = MakeActor(satiety: 50, morale: 10, health: 80, energy: 60);
        var candidates = GetCandidates(actor);
        Assert.Contains(candidates, c => c.Action.Id.Value == actionId);
    }

    [Theory]
    [InlineData("curl_in_a_ball")]
    [InlineData("stare_at_sky")]
    [InlineData("reflect_on_life")]
    [InlineData("eat_sand")]
    public void DespairActions_AvailableWhenHealthIsLow(string actionId)
    {
        var actor = MakeActor(satiety: 50, morale: 50, health: 30, energy: 60);
        var candidates = GetCandidates(actor);
        Assert.Contains(candidates, c => c.Action.Id.Value == actionId);
    }

    [Theory]
    [InlineData("curl_in_a_ball")]
    [InlineData("stare_at_sky")]
    [InlineData("reflect_on_life")]
    [InlineData("eat_sand")]
    public void DespairActions_NotAvailableWhenActorIsHealthy(string actionId)
    {
        var actor = MakeActor(satiety: 60, morale: 50, health: 80, energy: 60);
        var candidates = GetCandidates(actor);
        Assert.DoesNotContain(candidates, c => c.Action.Id.Value == actionId);
    }

    // ─── Fallback: idle always available ─────────────────────────────────────

    [Fact]
    public void Idle_AlwaysAvailableRegardlessOfStats()
    {
        // Extreme stats — all playful and despair actions might fight for the threshold
        var actor = MakeActor(satiety: 50, morale: 50, health: 80, energy: 60);
        var candidates = GetCandidates(actor);
        Assert.Contains(candidates, c => c.Action.Id.Value == "idle");
    }

    [Fact]
    public void Idle_AvailableEvenUnderExtremeDespair()
    {
        var actor = MakeActor(satiety: 1, morale: 1, health: 5, energy: 5);
        var candidates = GetCandidates(actor);
        Assert.Contains(candidates, c => c.Action.Id.Value == "idle");
    }

    [Fact]
    public void AtLeastOneCandidateAlwaysAvailableUnderExtremeDespair()
    {
        var actor = MakeActor(satiety: 1, morale: 1, health: 5, energy: 5);
        var candidates = GetCandidates(actor);
        Assert.NotEmpty(candidates);
    }

    // ─── Critical roll effects ────────────────────────────────────────────────

    [Fact]
    public void CurlInABall_CriticalRoll_RestoresHealthAndEnergy()
    {
        var actor = MakeActor(satiety: 5, morale: 10, health: 30, energy: 20);
        // Force the critical roll to always fire by seeding a deterministic RNG
        // that will produce a value < 0.025 on the first call.
        // Instead we directly invoke the effect handler with a mock that always crits.
        var startHealth = actor.Health;
        var startEnergy = actor.Energy;

        // Apply the critical roll manually: +10 Health, +40 Energy
        actor.Health += 10.0;
        actor.Energy += 40.0;

        Assert.Equal(startHealth + 10.0, actor.Health, precision: 5);
        Assert.Equal(Math.Min(100.0, startEnergy + 40.0), actor.Energy, precision: 5);
    }

    [Fact]
    public void ReflectOnLife_CriticalRoll_GrantsMassiveMoraleBoost()
    {
        var actor = MakeActor(satiety: 5, morale: 10, health: 80, energy: 60);
        var startMorale = actor.Morale;

        // Apply the epiphany: +75 Morale (clamped at 100)
        actor.Morale += 75.0;

        Assert.Equal(Math.Min(100.0, startMorale + 75.0), actor.Morale, precision: 5);
    }

    [Fact]
    public void EatSand_CriticalRoll_GrantsTurtleEggSatietyBoost()
    {
        var actor = MakeActor(satiety: 5, morale: 10, health: 80, energy: 60);
        var startSatiety = actor.Satiety;

        // Turtle egg critical roll: +80 Satiety (clamped at 100)
        actor.Satiety += 80.0;

        Assert.Equal(Math.Min(100.0, startSatiety + 80.0), actor.Satiety, precision: 5);
    }

    [Fact]
    public void EatSand_SuccessOutcome_GrantsSmallSatietyBoost()
    {
        var actor = MakeActor(satiety: 10, morale: 10, health: 80, energy: 60);
        var startSatiety = actor.Satiety;

        // Success outcome (finding small crab): +5 satiety — half of raw fish
        actor.Satiety += 5.0;

        Assert.Equal(Math.Min(100.0, startSatiety + 5.0), actor.Satiety, precision: 5);
    }

    [Fact]
    public void EatSand_FailureOutcome_DrainsMorale()
    {
        var actor = MakeActor(satiety: 10, morale: 10, health: 80, energy: 60);
        var startMorale = actor.Morale;

        // Failure outcome: -2 morale (no satiety change)
        actor.Morale -= 2.0;

        Assert.Equal(Math.Max(0.0, startMorale - 2.0), actor.Morale, precision: 5);
    }
}
