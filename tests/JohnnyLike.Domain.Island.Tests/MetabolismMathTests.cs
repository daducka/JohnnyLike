using JohnnyLike.Domain.Island.Metabolism;

namespace JohnnyLike.Domain.Island.Tests;

/// <summary>
/// Unit tests for <see cref="MetabolismMath"/> constants and helpers.
/// </summary>
public class MetabolismMathTests
{
    // ─── Conversion helpers ───────────────────────────────────────────────────

    [Fact]
    public void CaloriesToSatietyDelta_2000kcal_Maps_To_100Satiety()
    {
        // 2000 kcal = SatietyKcalAt100, so it should fill Satiety completely.
        var delta = MetabolismMath.CaloriesToSatietyDelta(MetabolismMath.SatietyKcalAt100);
        Assert.Equal(100.0, delta, precision: 6);
    }

    [Fact]
    public void CaloriesToEnergyDelta_500kcal_Maps_To_100Energy()
    {
        var delta = MetabolismMath.CaloriesToEnergyDelta(MetabolismMath.EnergyKcalAt100);
        Assert.Equal(100.0, delta, precision: 6);
    }

    [Fact]
    public void SatietyToCalories_100_Returns_SatietyKcalAt100()
    {
        Assert.Equal(MetabolismMath.SatietyKcalAt100, MetabolismMath.SatietyToCalories(100.0), precision: 6);
    }

    [Fact]
    public void EnergyToCalories_100_Returns_EnergyKcalAt100()
    {
        Assert.Equal(MetabolismMath.EnergyKcalAt100, MetabolismMath.EnergyToCalories(100.0), precision: 6);
    }

    // ─── Food calorie → Satiety mappings ─────────────────────────────────────

    [Fact]
    public void CoconutSuccess_kcal_Maps_To_15_Satiety()
    {
        // 300 kcal → +15 Satiety (the normal success coconut case)
        var delta = MetabolismMath.CaloriesToSatietyDelta(MetabolismMath.CoconutKcalSuccess);
        Assert.Equal(15.0, delta, precision: 6);
    }

    [Fact]
    public void CookedFishKcal_Maps_To_20_Satiety()
    {
        // 400 kcal → +20 Satiety
        var delta = MetabolismMath.CaloriesToSatietyDelta(MetabolismMath.CookedFishKcal);
        Assert.Equal(20.0, delta, precision: 6);
    }

    [Fact]
    public void RawFishKcal_Maps_To_10_Satiety()
    {
        // 200 kcal → +10 Satiety
        var delta = MetabolismMath.CaloriesToSatietyDelta(MetabolismMath.RawFishKcal);
        Assert.Equal(10.0, delta, precision: 6);
    }

    // ─── Basal burn over a game-day ───────────────────────────────────────────

    [Fact]
    public void FullGameDay_BasalBurn_ReducesSatiety_By_Roughly_120()
    {
        // 1 game-day = 1440 sim-seconds (1 sim-s ≈ 1 story-minute).
        // BasalKcalPerDay = 2400 kcal → satiety drop = 2400/2000*100 = 120 points.
        // Order-of-magnitude check: drop should be in [50, 150].
        const double gameDaySeconds = 1440.0;
        double satiety = 100.0;
        double energy = 100.0;

        MetabolismMath.ApplyTimeStep(
            ref satiety, ref energy,
            gameDaySeconds,
            activityKcalPerSecond: 0.0,   // rest only
            isSleeping: false);

        double drop = 100.0 - satiety;
        Assert.InRange(drop, 50.0, 150.0); // expected ~120 points
    }

    [Fact]
    public void FullSatiety_AtBasal_SurvivesMany_SimulationSeconds()
    {
        // Starting at full Satiety, the actor should not reach "starving" (<20)
        // within a single 300-second simulation run (representing a 5-hour story-session).
        double satiety = 100.0;
        double energy = 100.0;

        MetabolismMath.ApplyTimeStep(
            ref satiety, ref energy,
            300.0,
            MetabolismMath.LightActivityKcalPerSecond,
            isSleeping: false);

        Assert.True(satiety > 20.0,
            $"Actor should not be starving after 300 sim-s of light activity, got Satiety={satiety:F2}");
    }

    // ─── Sleep recovery ───────────────────────────────────────────────────────

    [Fact]
    public void Sleep_RestoresEnergy_MeaningfullyWithinReasonableTime()
    {
        // Starting at Energy=0, sleeping for 151 sim-seconds (> 2.5 story-hours)
        // should fully restore Energy.
        // SleepEnergyRecoveryKcalPerSecond = 2 × BasalKcalPerSecond ≈ 3.333 kcal/s.
        // 151 s × 3.333 kcal/s ≈ 503 kcal > EnergyKcalAt100 (500 kcal), so Energy clamps at 100.
        double satiety = 100.0;
        double energy = 0.0;

        MetabolismMath.ApplyTimeStep(
            ref satiety, ref energy,
            151.0,
            activityKcalPerSecond: 0.0,
            isSleeping: true);

        Assert.True(energy >= 100.0,
            $"Energy should be fully recovered after 151 s of sleep, got {energy:F2}");
    }

    [Fact]
    public void Sleep_DoesNotCollapseSatiety()
    {
        // Sleeping burns only basal calories from Satiety.
        // 30 sim-seconds (≈ 30 story-minutes) of sleep should not drop Satiety more than ~3 points.
        double satiety = 50.0;
        double energy = 30.0;

        MetabolismMath.ApplyTimeStep(
            ref satiety, ref energy,
            30.0,
            activityKcalPerSecond: 0.0,
            isSleeping: true);

        double drop = 50.0 - satiety;
        Assert.True(drop < 5.0,
            $"Satiety should barely change during 30 s of sleep, dropped by {drop:F2}");
        Assert.True(energy > 30.0,
            $"Energy should increase during sleep, got {energy:F2}");
    }

    // ─── ApplyCalorieBurn ─────────────────────────────────────────────────────

    [Fact]
    public void ApplyCalorieBurn_DrainEnergy_BeforeSatiety()
    {
        // A small burn should only take from Energy, not Satiety.
        double satiety = 80.0;
        double energy = 50.0;

        // 50 kcal from Energy: CaloriesToEnergyDelta(50) = 50/500*100 = 10 points
        MetabolismMath.ApplyCalorieBurn(ref satiety, ref energy, 50.0);

        Assert.Equal(80.0, satiety, precision: 6); // Satiety untouched
        Assert.Equal(40.0, energy, precision: 6);  // Energy reduced by 10 points
    }

    [Fact]
    public void ApplyCalorieBurn_OverflowsIntoSatiety_WhenEnergyDepleted()
    {
        // If kcalBurned > available energy kcal, the overflow drains Satiety.
        double satiety = 50.0;
        double energy = 10.0; // = 50 kcal

        // Burn 100 kcal: 50 from Energy (exhausts it), 50 overflow from Satiety
        // CaloriesToSatietyDelta(50) = 50/2000*100 = 2.5 points
        MetabolismMath.ApplyCalorieBurn(ref satiety, ref energy, 100.0);

        Assert.Equal(0.0, energy, precision: 6);
        Assert.Equal(50.0 - 2.5, satiety, precision: 6);
    }

    // ─── ApplyTimeStep – sanity checks ────────────────────────────────────────

    [Fact]
    public void ApplyTimeStep_HeavyActivity_DrainsEnergyFasterThanLight()
    {
        double satietyHeavy = 100.0, energyHeavy = 100.0;
        double satietyLight = 100.0, energyLight = 100.0;

        MetabolismMath.ApplyTimeStep(ref satietyHeavy, ref energyHeavy, 20.0,
            MetabolismMath.HeavyActivityKcalPerSecond, isSleeping: false);
        MetabolismMath.ApplyTimeStep(ref satietyLight, ref energyLight, 20.0,
            MetabolismMath.LightActivityKcalPerSecond, isSleeping: false);

        Assert.True(energyHeavy < energyLight,
            "Heavy activity should drain Energy more than light activity");
        // Satiety depletes at the same rate (basal only) regardless of activity level
        Assert.Equal(satietyHeavy, satietyLight, precision: 6);
    }

    [Fact]
    public void ApplyTimeStep_Stats_NeverExceedBounds()
    {
        double satiety = 0.0;
        double energy = 100.0;

        // Burning more kcal than available should clamp at 0
        MetabolismMath.ApplyTimeStep(ref satiety, ref energy, 10000.0,
            MetabolismMath.HeavyActivityKcalPerSecond, isSleeping: false);

        Assert.True(satiety >= 0.0);
        Assert.True(energy >= 0.0);

        satiety = 100.0;
        energy = 0.0;

        // Sleep on a full Satiety bar should not exceed 100 for either stat
        MetabolismMath.ApplyTimeStep(ref satiety, ref energy, 10000.0,
            activityKcalPerSecond: 0.0, isSleeping: true);

        Assert.True(energy <= 100.0);
        Assert.True(satiety >= 0.0);
    }
}
