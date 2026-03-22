using System.Text.Json.Serialization;

namespace JohnnyLike.SimRunner.PressureFuzzer;

/// <summary>
/// The four numeric stats captured for a golden-state sample point.
/// Values are on a 0–100 scale matching <see cref="ActorStatSnapshot"/>.
/// </summary>
public sealed record GoldenStateValues(
    double Satiety,
    double Health,
    double Energy,
    double Morale);

/// <summary>
/// Defines the expected decision outcome for a golden state.
/// Category fields reference <c>QualityType</c> names (e.g. "FoodConsumption", "Rest").
/// At least one of <see cref="DesiredTopCategory"/> or <see cref="AcceptableTopCategories"/>
/// must be provided.
/// </summary>
public sealed record GoldenStateDesiredOutcome(
    /// <summary>
    /// The single most important category that should win for this state.
    /// Null when only a set of acceptable categories is specified.
    /// </summary>
    string? DesiredTopCategory,
    /// <summary>
    /// Additional categories that are also considered acceptable outcomes.
    /// When <see cref="DesiredTopCategory"/> is set, these are alternatives that are
    /// not preferred but still acceptable (no regression).
    /// </summary>
    IReadOnlyList<string>? AcceptableTopCategories,
    /// <summary>
    /// Categories that must never win in this state, regardless of scoring.
    /// Used for hard "must-not-do" constraints.
    /// </summary>
    IReadOnlyList<string>? ForbiddenTopCategories,
    /// <summary>
    /// Optional free-text rationale explaining why this outcome is expected.
    /// Not used by tooling; purely for human readers.
    /// </summary>
    string? Notes = null);

/// <summary>
/// A single hand-authored golden state: an (actor, scenario, stats) triple together with
/// the desired decision outcome and its relative importance.
///
/// <para>
/// <see cref="SampleKey"/> follows the same deterministic format used by
/// <see cref="PressureSample.SampleKey"/>:
/// <c>{actor}|{scenario}|s{satiety}|h{health}|e{energy}|m{morale}</c>.
/// This allows golden-state entries to be directly joined with fuzzer output rows.
/// </para>
/// </summary>
public sealed record GoldenStateEntry(
    /// <summary>
    /// Stable identifier: <c>{actor}|{scenario}|s{satiety}|h{health}|e{energy}|m{morale}</c>.
    /// Must match the <see cref="PressureSample.SampleKey"/> format exactly so the two
    /// datasets can be joined on this field.
    /// </summary>
    string SampleKey,
    /// <summary>Actor archetype name (e.g. "Johnny", "Sawyer"). Case-sensitive.</summary>
    string Actor,
    /// <summary>Scenario kind name. Must be a valid <see cref="FuzzerScenarioKind"/> member.</summary>
    string Scenario,
    /// <summary>The stat values that define this sample point.</summary>
    GoldenStateValues State,
    /// <summary>What the actor should decide in this state.</summary>
    GoldenStateDesiredOutcome DesiredOutcome,
    /// <summary>
    /// Relative importance of this golden state. Higher values indicate
    /// states that are more critical to get right (e.g. survival-critical states
    /// should have higher priority than flavour/archetype states).
    /// </summary>
    double Priority,
    /// <summary>
    /// Optional short human-readable label, matching the style of
    /// <see cref="GoldenStateSpec.Label"/> in the hard-coded set.
    /// Used as a display name in reports and debug output.
    /// </summary>
    string? Label = null);
