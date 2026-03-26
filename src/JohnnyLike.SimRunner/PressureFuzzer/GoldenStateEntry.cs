using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;

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
/// Category fields use <see cref="QualityType"/> so downstream tooling receives
/// parsed, type-safe values rather than raw strings.
/// At least one of <see cref="DesiredTopCategory"/> or <see cref="AcceptableTopCategories"/>
/// must be provided.
/// </summary>
public sealed record GoldenStateDesiredOutcome(
    /// <summary>
    /// The single most important category that should win for this state.
    /// Null when only a set of acceptable categories is specified.
    /// </summary>
    QualityType? DesiredTopCategory,
    /// <summary>
    /// Additional categories that are also considered acceptable outcomes.
    /// When <see cref="DesiredTopCategory"/> is set, these are alternatives that are
    /// not preferred but still acceptable (no regression).
    /// </summary>
    IReadOnlyList<QualityType>? AcceptableTopCategories,
    /// <summary>
    /// Categories that must never win in this state, regardless of scoring.
    /// Used for hard "must-not-do" constraints.
    /// </summary>
    IReadOnlyList<QualityType>? ForbiddenTopCategories,
    /// <summary>
    /// Optional free-text rationale explaining why this outcome is expected.
    /// Not used by tooling; purely for human readers.
    /// </summary>
    string? Notes = null);

/// <summary>
/// A single hand-authored golden state: a (trait-profile, scenario, stats) triple together with
/// the desired decision outcome and its relative importance.
///
/// <para>
/// <see cref="SampleKey"/> uses a deterministic trait-hash format:
/// <c>trait:{hash}|{scenario}|s{satiety}|h{health}|e{energy}|m{morale}</c>.
/// The hash is an FNV-1a hash of the canonical trait-profile string in fixed field order.
/// </para>
/// </summary>
public sealed record GoldenStateEntry(
    /// <summary>
    /// Stable identifier: <c>trait:{traitHash}|{scenario}|s{satiety}|h{health}|e{energy}|m{morale}</c>.
    /// Built deterministically from <see cref="TraitProfile"/> and <see cref="State"/> so the
    /// key is stable across renames or cast changes.
    /// </summary>
    string SampleKey,
    /// <summary>
    /// The explicit personality trait profile that defines this golden state's behavioural character.
    /// All six traits must be present and in [0, 1].
    /// </summary>
    TraitProfile TraitProfile,
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
    /// Optional human-readable rationale describing the intended trait character of this golden state
    /// (e.g. "High Hedonist, low Planner — should preserve fun longer before survival pivot").
    /// Not used by tooling; purely for human readers.
    /// </summary>
    string? TraitIntent = null,
    /// <summary>
    /// Optional short human-readable label, matching the style of
    /// <see cref="GoldenStateSpec.Label"/> in the hard-coded set.
    /// Used as a display name in reports and debug output.
    /// </summary>
    string? Label = null);
