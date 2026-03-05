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
///     At basal rate, the actor burns through a full Satiety bar in ~1440 s = 1 game-day.
///     Starvation is therefore a day-scale problem, not a minutes-scale one.
///
///   Energy 100 = 500 kcal of short-term ATP / glycogen reserves.
///     Light activity (walking, crafting) drains ~0.17 Energy/s.
///     Heavy activity (vigorous swimming) drains ~1.33 Energy/s.
///     Sleep restores Energy at 0.67 points/s; a full recovery from 0 takes ~150 s ≈ 2.5 story-hours.
///
/// BASAL RATE:
///   BasalKcalPerDay  = 2400 kcal/day.
///   BasalKcalPerSecond = 2400 ÷ 1440 ≈ 1.667 kcal/sim-s.
///   Satiety drain at rest ≈ 0.083 points/sim-s → 100 points in 1200 sim-s ≈ 20 story-hours.
///
/// FOOD CALORIES:
///   Coconut (critical success) : 400 kcal → +20 Satiety
///   Coconut (success)          : 300 kcal → +15 Satiety
///   Coconut (partial success)  : 160 kcal → +8  Satiety
///   Coconut (failure)          : 100 kcal → +5  Satiety
///   Coconut (critical failure) :  60 kcal → +3  Satiety
///   Cooked fish                : 400 kcal → +20 Satiety
///   Raw fish                   : 200 kcal → +10 Satiety
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

    /// <summary>Heavy activity (vigorous swimming). ~4× basal extra energy drain.</summary>
    public const double HeavyActivityKcalPerSecond = BasalKcalPerSecond * 4.0;

    // ─── Sleep recovery ───────────────────────────────────────────────────────

    /// <summary>
    /// Energy restored per sim-second while sleeping, expressed in kcal.
    /// At 2× basal the actor fully recovers from Energy = 0 in ~150 sim-s (≈ 2.5 story-hours).
    /// </summary>
    public const double SleepEnergyRecoveryKcalPerSecond = BasalKcalPerSecond * 2.0;

    // ─── Food calorie values ──────────────────────────────────────────────────

    /// <summary>Calories from a coconut: cracked cleanly (critical success) — ~400 kcal → +20 Satiety.</summary>
    public const double CoconutKcalCriticalSuccess = 400.0;

    /// <summary>Calories from a coconut: normal success — 300 kcal → +15 Satiety.</summary>
    public const double CoconutKcalSuccess = 300.0;

    /// <summary>Calories from a coconut: partial success — 160 kcal → +8 Satiety.</summary>
    public const double CoconutKcalPartialSuccess = 160.0;

    /// <summary>Calories from a coconut: failure (only a few bites) — 100 kcal → +5 Satiety.</summary>
    public const double CoconutKcalFailure = 100.0;

    /// <summary>Calories from a coconut: critical failure (most spilled) — 60 kcal → +3 Satiety.</summary>
    public const double CoconutKcalCriticalFailure = 60.0;

    /// <summary>Calories from a cooked fish — 400 kcal → +20 Satiety.</summary>
    public const double CookedFishKcal = 400.0;

    /// <summary>Calories from a raw fish (less bioavailable) — 200 kcal → +10 Satiety.</summary>
    public const double RawFishKcal = 200.0;

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
