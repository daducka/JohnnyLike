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
        // Sleeping burns basal calories from Satiety AND converts additional Satiety to Energy
        // via SatietyToEnergyKcalPerSecondAsleep.  For 30 sim-seconds starting at energy = 30:
        //   Basal drain: ~2.5 Satiety
        //   Sleep recovery adds ~20 Energy (energy → 50), then conversion adds ~15 more (energy → 65).
        //   Conversion cost: ~3.75 Satiety.
        //   Total Satiety drop: ~6.25 points.
        // The threshold is < 10 to confirm Satiety doesn't collapse, while allowing for the
        // conversion-driven extra drain.
        double satiety = 50.0;
        double energy = 30.0;

        MetabolismMath.ApplyTimeStep(
            ref satiety, ref energy,
            30.0,
            activityKcalPerSecond: 0.0,
            isSleeping: true);

        double drop = 50.0 - satiety;
        Assert.True(drop < 10.0,
            $"Satiety should not drop more than 10 points during 30 s of sleep, dropped by {drop:F2}");
        Assert.True(energy > 30.0,
            $"Energy should increase during sleep, got {energy:F2}");
    }

    // ─── Satiety → Energy conversion ─────────────────────────────────────────

    [Fact]
    public void ApplyTimeStep_LightActivity_EnergyStaysStable_SatietyFuelsActivity()
    {
        // Light activity drain rate equals the awake conversion rate, so Energy barely moves
        // while Satiety is available.  The cost of the activity is paid by Satiety instead.
        double satiety = 80.0;
        double energy = 80.0; // below 100 so conversion can fire

        MetabolismMath.ApplyTimeStep(
            ref satiety, ref energy,
            10.0,
            MetabolismMath.LightActivityKcalPerSecond,
            isSleeping: false);

        // Energy should be approximately the same as it started (conversion offsets drain).
        Assert.InRange(energy, 79.0, 81.0);
        // Satiety should have decreased (basal + conversion cost).
        Assert.True(satiety < 80.0,
            $"Satiety should decrease when fueling light activity, got {satiety:F2}");
    }

    [Fact]
    public void ApplyTimeStep_HeavyActivity_EnergyDropsButSlowerThanWithoutConversion()
    {
        // With conversion, net Energy drain during heavy activity = activity drain - conversion rate.
        // Heavy (2× basal) drain - 0.5× basal conversion = 1.5× basal net drain.
        double satiety = 100.0;
        double energy = 100.0;

        MetabolismMath.ApplyTimeStep(
            ref satiety, ref energy,
            20.0,
            MetabolismMath.HeavyActivityKcalPerSecond,
            isSleeping: false);

        // Energy should have decreased (heavy activity is not fully offset).
        Assert.True(energy < 100.0,
            $"Energy should drop during heavy activity, got {energy:F2}");
        // But Energy should not have dropped all the way to zero in 20 sim-seconds.
        Assert.True(energy > 50.0,
            $"Energy should not collapse in 20 sim-s of heavy activity, got {energy:F2}");
    }

    [Fact]
    public void ApplyTimeStep_NoConversionWhenEnergyFull()
    {
        // If Energy starts at 100 and activity = 0, no conversion should fire.
        double satiety = 80.0;
        double energy = 100.0;

        MetabolismMath.ApplyTimeStep(
            ref satiety, ref energy,
            10.0,
            activityKcalPerSecond: 0.0,
            isSleeping: false);

        // Satiety drops only from basal burn (no conversion because energy is full).
        double expectedSatietyDrop = MetabolismMath.CaloriesToSatietyDelta(MetabolismMath.BasalKcalPerSecond * 10.0);
        Assert.Equal(80.0 - expectedSatietyDrop, satiety, precision: 6);
        Assert.Equal(100.0, energy, precision: 6);
    }

    [Fact]
    public void ApplyTimeStep_NoConversionWhenSatietyEmpty()
    {
        // If Satiety is zero, no conversion should fire even when Energy is depleted.
        double satiety = 0.0;
        double energy = 20.0;

        MetabolismMath.ApplyTimeStep(
            ref satiety, ref energy,
            10.0,
            MetabolismMath.HeavyActivityKcalPerSecond,
            isSleeping: false);

        Assert.Equal(0.0, satiety, precision: 6); // Satiety stays at 0
        Assert.True(energy < 20.0,               // Energy continues to drain
            $"Energy should drain when Satiety is empty, got {energy:F2}");
    }

    [Fact]
    public void ApplyTimeStep_Sleep_ConversionAddsEnergyFromSatiety()
    {
        // During sleep the body converts Satiety to Energy (in addition to sleep recovery).
        // Starting at energy = 50 with plenty of satiety, sleep should push energy above
        // what sleep recovery alone would achieve.
        double satietyWith = 80.0;
        double energyWith = 50.0;

        MetabolismMath.ApplyTimeStep(
            ref satietyWith, ref energyWith,
            20.0,
            activityKcalPerSecond: 0.0,
            isSleeping: true);

        // Without conversion, sleep recovery alone gives:
        // CaloriesToEnergyDelta(SleepEnergyRecoveryKcalPerSecond * 20) points of Energy.
        double sleepRecoveryOnly = MetabolismMath.CaloriesToEnergyDelta(
            MetabolismMath.SleepEnergyRecoveryKcalPerSecond * 20.0);
        double expectedMinEnergy = 50.0 + sleepRecoveryOnly;

        Assert.True(energyWith > expectedMinEnergy,
            $"Sleep with conversion should recover more than sleep alone ({expectedMinEnergy:F2}), got {energyWith:F2}");
        Assert.True(satietyWith < 80.0,
            $"Satiety should decrease during sleep to fund conversion, got {satietyWith:F2}");
    }



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
        // Both cases consume the same Satiety: basal burn is identical and the Satiety→Energy
        // conversion amount is equal (both are capped by their respective energyKcalNeeded).
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
