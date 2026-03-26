using JohnnyLike.Domain.Abstractions;

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
/// The six derived personality traits that describe an actor's behavioural tendencies.
/// Each trait is normalised to [0, 1] using Norm(a, b) = Clamp((a + b - 20) / 20, 0, 1)
/// where <c>a</c> and <c>b</c> are the two contributing D&amp;D ability scores.
///
/// <para>
/// Trait derivations:
/// <list type="bullet">
///   <item><see cref="Planner"/>     = Norm(INT, WIS) — prefers preparation and efficiency</item>
///   <item><see cref="Craftsman"/>   = Norm(DEX, INT) — prefers crafting and mastery</item>
///   <item><see cref="Survivor"/>    = Norm(CON, WIS) — prefers safety and sustainability</item>
///   <item><see cref="Hedonist"/>    = Norm(CHA, CON) — prefers comfort and morale</item>
///   <item><see cref="Instinctive"/> = Norm(STR, CHA) — prefers immediate reward</item>
///   <item><see cref="Industrious"/> = Norm(STR, DEX) — prefers building and working</item>
/// </list>
/// </para>
/// </summary>
public sealed record TraitProfile(
    /// <summary>Norm(INT, WIS) ∈ [0, 1] — prefers preparation and efficiency.</summary>
    double Planner,
    /// <summary>Norm(DEX, INT) ∈ [0, 1] — prefers crafting and mastery.</summary>
    double Craftsman,
    /// <summary>Norm(CON, WIS) ∈ [0, 1] — prefers safety and sustainability.</summary>
    double Survivor,
    /// <summary>Norm(CHA, CON) ∈ [0, 1] — prefers comfort and morale.</summary>
    double Hedonist,
    /// <summary>Norm(STR, CHA) ∈ [0, 1] — prefers immediate reward.</summary>
    double Instinctive,
    /// <summary>Norm(STR, DEX) ∈ [0, 1] — prefers building and working.</summary>
    double Industrious);

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
