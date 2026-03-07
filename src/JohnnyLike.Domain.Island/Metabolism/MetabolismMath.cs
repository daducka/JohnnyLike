namespace JohnnyLike.Domain.Island.Metabolism;

/// <summary>
/// Shared metabolism math for the Island domain.
///
/// TIME SCALE:
///   1 simulation-second ≈ 1 story-time minute.
///   Actions that take 8–20 sim-seconds therefore represent 8–20 story-minutes of activity.
///   A "game day" is 1440 sim-seconds (24 × 60 story-minutes).
///
/// STAT SCALE:
///   Satiety 100 = 2000 kcal of stored food energy.
///     BasalKcalPerDay = 2400 kcal, so a full Satiety bar is exhausted after ~1200 sim-s ≈ 20 story-hours
///     of pure rest (not a full 24-hour day).  This is intentional: 2400 kcal is the physiologically
///     realistic BMR for an active adult, while 2000 kcal for "full" gives the actor a meaningful but
///     slightly sub-day food reserve — they must eat at least once per in-game day to stay nourished.
///     Starvation is therefore a day-scale problem, not a minutes-scale one.
///
///   Energy 100 = 500 kcal of short-term ATP / glycogen reserves.
///     Light activity net drain ≈ 0 Energy/s (Satiety→Energy conversion fully offsets drain).
///     Heavy activity net drain ≈ 0.5 Energy/s (conversion offsets 25% of drain).
///     Sleep: Energy recovers at ~1.17 points/s (recovery + conversion combined); full recovery
///     from 0 takes ~86 sim-s ≈ 1.4 story-hours.
///
/// SATIETY → ENERGY CONVERSION:
///   When Energy is below 100 and Satiety > 0, the body automatically converts stored food
///   reserves into short-term ATP.  This means Energy rarely hits 0 while Satiety is available,
///   and light activity barely depletes Energy at all.  Constants:
///     SatietyToEnergyKcalPerSecondAwake  = 0.5× basal (≈ 0.833 kcal/s)
///     SatietyToEnergyKcalPerSecondAsleep = 1.5× basal (≈ 2.500 kcal/s)
///
/// BASAL RATE:
///   BasalKcalPerDay    = 2400 kcal/day.
///   BasalKcalPerSecond = 2400 ÷ 1440 ≈ 1.667 kcal/sim-s.
///
/// FOOD CALORIES (defined on each supply class, not here):
///   CoconutSupply  : 60–400 kcal per tier → +3 to +20 Satiety.
///   CookedFishSupply: 400 kcal → +20 Satiety, small Energy boost.
///   FishSupply     : 200 kcal → +10 Satiety.
///   (Tier differences are preserved by scaling kcal, not Satiety points directly.)
/// </summary>
public static class MetabolismMath
{
    // ─── Scale constants ─────────────────────────────────────────────────────

    /// <summary>Kilocalories that correspond to Satiety = 100 (a full "tank" of food energy).</summary>
    public const double SatietyKcalAt100 = 2000.0;

    /// <summary>Kilocalories that correspond to Energy = 100 (full short-term ATP/glycogen reserves).</summary>
    public const double EnergyKcalAt100 = 500.0;

    /// <summary>Resting metabolic rate in kcal per game-day (2400 kcal ≈ typical adult).</summary>
    public const double BasalKcalPerDay = 2400.0;

    /// <summary>
    /// Basal metabolic rate in kcal per sim-second.
    /// Derived as BasalKcalPerDay ÷ 1440 (minutes per day), because 1 sim-second ≈ 1 story-minute.
    /// ≈ 1.667 kcal/sim-s.
    /// </summary>
    public const double BasalKcalPerSecond = BasalKcalPerDay / 1440.0;

    // ─── Activity-level energy drain (extra Energy burn above basal, per sim-second) ──

    /// <summary>Light activity (crafting, gathering, walking). ~0.5× basal extra energy drain.</summary>
    public const double LightActivityKcalPerSecond = BasalKcalPerSecond * 0.5;

    /// <summary>Moderate activity (hiking, active fishing). ~2× basal extra energy drain.</summary>
    public const double ModerateActivityKcalPerSecond = BasalKcalPerSecond * 2.0;

    /// <summary>Heavy activity (vigorous swimming). ~2× basal total energy drain.
    /// Reduced from 4× to 2× so that a strenuous swim drains ~0.5 Energy/s net (after
    /// Satiety→Energy conversion), giving several minutes of continuous heavy activity
    /// before the Energy bar is exhausted.</summary>
    public const double HeavyActivityKcalPerSecond = BasalKcalPerSecond * 2.0;

    // ─── Satiety → Energy conversion ──────────────────────────────────────────

    /// <summary>
    /// Kilocalories per sim-second converted from Satiety reserves into Energy while awake,
    /// whenever Energy is below 100 and Satiety is available.
    /// Set to 0.5× basal so that light activity barely drains Energy — the conversion rate
    /// matches <see cref="LightActivityKcalPerSecond"/> — while heavy activity still causes
    /// a noticeable net Energy decrease.
    /// </summary>
    public const double SatietyToEnergyKcalPerSecondAwake = BasalKcalPerSecond * 0.5;

    /// <summary>
    /// Kilocalories per sim-second converted from Satiety reserves into Energy while sleeping.
    /// Set to 1.5× basal so the body restores short-term ATP from stored food at an
    /// accelerated rate during rest, supplementing <see cref="SleepEnergyRecoveryKcalPerSecond"/>.
    /// </summary>
    public const double SatietyToEnergyKcalPerSecondAsleep = BasalKcalPerSecond * 1.5;

    // ─── Sleep recovery ───────────────────────────────────────────────────────

    /// <summary>
    /// Energy restored per sim-second while sleeping, expressed in kcal.
    /// At 2× basal the actor fully recovers from Energy = 0 in ~150 sim-s (≈ 2.5 story-hours).
    /// </summary>
    public const double SleepEnergyRecoveryKcalPerSecond = BasalKcalPerSecond * 2.0;

    // ─── Conversion helpers ───────────────────────────────────────────────────

    /// <summary>Converts kilocalories to a Satiety point delta (positive = gain).</summary>
    public static double CaloriesToSatietyDelta(double kcal) => kcal / SatietyKcalAt100 * 100.0;

    /// <summary>Converts kilocalories to an Energy point delta (positive = gain).</summary>
    public static double CaloriesToEnergyDelta(double kcal) => kcal / EnergyKcalAt100 * 100.0;

    /// <summary>Converts a Satiety value (0–100) back to kilocalories.</summary>
    public static double SatietyToCalories(double satiety) => satiety / 100.0 * SatietyKcalAt100;

    /// <summary>Converts an Energy value (0–100) back to kilocalories.</summary>
    public static double EnergyToCalories(double energy) => energy / 100.0 * EnergyKcalAt100;

    // ─── Time-step application ────────────────────────────────────────────────

    /// <summary>
    /// Applies metabolism effects for one simulation time-step.
    ///
    /// While awake:
    ///   - Satiety is depleted at basal rate (food constantly metabolised).
    ///   - Energy is depleted by physical activity (<paramref name="activityKcalPerSecond"/>).
    ///     Pass <see cref="LightActivityKcalPerSecond"/>, <see cref="ModerateActivityKcalPerSecond"/>,
    ///     or <see cref="HeavyActivityKcalPerSecond"/> depending on the action.
    ///
    /// While sleeping (<paramref name="isSleeping"/> = true):
    ///   - Satiety still depletes at basal rate (body continues burning calories at rest).
    ///   - Energy recovers at <see cref="SleepEnergyRecoveryKcalPerSecond"/>.
    ///   - <paramref name="activityKcalPerSecond"/> is ignored.
    ///
    /// In both modes, when Energy is below 100 and Satiety > 0, stored food reserves are
    /// automatically converted into short-term Energy at <see cref="SatietyToEnergyKcalPerSecondAwake"/>
    /// (awake) or <see cref="SatietyToEnergyKcalPerSecondAsleep"/> (sleeping).
    ///
    /// Both stats are clamped to [0, 100].
    /// </summary>
    public static void ApplyTimeStep(
        ref double satiety,
        ref double energy,
        double seconds,
        double activityKcalPerSecond,
        bool isSleeping)
    {
        double basalKcal = BasalKcalPerSecond * seconds;

        if (isSleeping)
        {
            satiety = Math.Clamp(satiety - CaloriesToSatietyDelta(basalKcal), 0.0, 100.0);
            double recoveryKcal = SleepEnergyRecoveryKcalPerSecond * seconds;
            energy = Math.Clamp(energy + CaloriesToEnergyDelta(recoveryKcal), 0.0, 100.0);
        }
        else
        {
            satiety = Math.Clamp(satiety - CaloriesToSatietyDelta(basalKcal), 0.0, 100.0);
            double activityKcal = activityKcalPerSecond * seconds;
            energy = Math.Clamp(energy - CaloriesToEnergyDelta(activityKcal), 0.0, 100.0);
        }

        // Satiety → Energy conversion: automatically refill Energy from food reserves when
        // Energy is not full and Satiety is available.
        if (energy < 100.0 && satiety > 0.0)
        {
            double convRate = isSleeping ? SatietyToEnergyKcalPerSecondAsleep : SatietyToEnergyKcalPerSecondAwake;
            double convKcal = convRate * seconds;

            // Cap conversion to what Satiety can provide and what Energy can absorb.
            double satietyKcalAvailable = SatietyToCalories(satiety);
            double energyKcalNeeded = EnergyToCalories(100.0 - energy);
            convKcal = Math.Min(convKcal, Math.Min(satietyKcalAvailable, energyKcalNeeded));

            satiety = Math.Clamp(satiety - CaloriesToSatietyDelta(convKcal), 0.0, 100.0);
            energy  = Math.Clamp(energy  + CaloriesToEnergyDelta(convKcal),  0.0, 100.0);
        }
    }

    /// <summary>
    /// Applies a sudden calorie burn (e.g., traumatic exertion) by draining Energy first,
    /// then overflowing into Satiety when Energy is exhausted.
    /// </summary>
    public static void ApplyCalorieBurn(ref double satiety, ref double energy, double kcalBurned)
    {
        double energyKcal = EnergyToCalories(energy);
        if (kcalBurned <= energyKcal)
        {
            energy = Math.Clamp(energy - CaloriesToEnergyDelta(kcalBurned), 0.0, 100.0);
        }
        else
        {
            energy = 0.0;
            double remaining = kcalBurned - energyKcal;
            satiety = Math.Clamp(satiety - CaloriesToSatietyDelta(remaining), 0.0, 100.0);
        }
    }
}
