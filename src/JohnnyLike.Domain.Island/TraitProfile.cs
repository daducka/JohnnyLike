namespace JohnnyLike.Domain.Island;

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
///
/// <para>
/// <see cref="TraitProfile"/> is the source of truth for golden-state evaluation.
/// When passed explicitly to the domain scoring path it bypasses stat-derived trait
/// reconstruction, ensuring that evaluation uses the exact authored values.
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
