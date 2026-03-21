namespace JohnnyLike.SimRunner.PressureFuzzer;

/// <summary>
/// A single hand-authored state that is always included in every fuzzer run,
/// regardless of the stat grid being used.
/// </summary>
public sealed record GoldenStateSpec(
    FuzzerScenarioKind Scenario,
    double Satiety,
    double Health,
    double Energy,
    double Morale,
    /// <summary>
    /// Short human-readable label describing the intended failure mode or
    /// balancing concern. Used as <c>goldenStateLabel</c> in the output.
    /// </summary>
    string Label);

/// <summary>
/// Curated set of important (actor, world) states that act as seed points for
/// balancing analysis and as the natural starting set for reverse-engineering
/// desired actor behaviour.
///
/// <para>These states are de-duplicated against the grid: if the coarse/fine
/// grid already covers the exact point, the existing row is patched with the
/// label rather than duplicated.</para>
///
/// <para>To add a new golden state: append an entry to <see cref="All"/>.
/// Keep labels short and focus on the failure mode, not the expected outcome.</para>
/// </summary>
public static class GoldenStates
{
    public static IReadOnlyList<GoldenStateSpec> All { get; } = new List<GoldenStateSpec>
    {
        // ── Starvation + immediate food available ─────────────────────────────
        // The classic "eat when hungry" case — should always choose food.
        new(FuzzerScenarioKind.FoodAvailableNow,
            Satiety: 10, Health: 70, Energy: 50, Morale: 50,
            Label: "starving+food_available"),

        // ── Starvation + only acquirable food (no pile) ───────────────────────
        // Actor must choose to go and get food rather than doing something else.
        new(FuzzerScenarioKind.NoFood_SourceAvailable,
            Satiety: 10, Health: 70, Energy: 50, Morale: 50,
            Label: "starving+source_only"),

        // ── Injured + hungry + bed available ─────────────────────────────────
        // Tension between Rest/Safety (injury pressure) and FoodConsumption.
        new(FuzzerScenarioKind.FoodAvailable_WithComfort,
            Satiety: 15, Health: 15, Energy: 50, Morale: 30,
            Label: "injured+hungry+bed"),

        // ── Low morale but otherwise stable ───────────────────────────────────
        // Mood suppression should not override survival needs.
        new(FuzzerScenarioKind.FoodAvailableNow,
            Satiety: 15, Health: 70, Energy: 70, Morale: 10,
            Label: "low_morale+hungry"),

        // ── Recipe temptation with high opportunity ────────────────────────────
        // Preparation/Efficiency pressure should be suppressed when hungry.
        new(FuzzerScenarioKind.HighRecipeOpportunity,
            Satiety: 10, Health: 70, Energy: 70, Morale: 50,
            Label: "hungry+high_recipe_opportunity"),

        // ── Late-collapse: food + broken campfire, low energy ─────────────────
        // Endgame distress: tests whether Rest wins over food when exhausted.
        new(FuzzerScenarioKind.LateCollapse,
            Satiety: 15, Health: 50, Energy: 10, Morale: 30,
            Label: "late_collapse+exhausted+hungry"),

        // ── Fully satisfied, comfortable world — baseline reference ───────────
        // Expect Comfort/Fun/Mastery to surface; food should score low.
        new(FuzzerScenarioKind.FoodAvailable_WithComfort,
            Satiety: 100, Health: 100, Energy: 100, Morale: 100,
            Label: "fully_satisfied_baseline"),

        // ── Bed-loop risk: adequate satiety but borderline, rest wins ─────────
        // satiety is below FoodPressureThreshold; bed is available.
        // Tests whether the rest+comfort pressure from the bed beats food pressure.
        new(FuzzerScenarioKind.FoodAvailable_WithComfort,
            Satiety: 25, Health: 70, Energy: 10, Morale: 50,
            Label: "borderline_hunger+exhausted+bed"),
    };
}
