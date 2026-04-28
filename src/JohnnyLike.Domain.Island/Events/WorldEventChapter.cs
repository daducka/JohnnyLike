namespace JohnnyLike.Domain.Island.Events;

/// <summary>
/// A single chapter of a <see cref="WorldEventScript"/>. A chapter has requirements
/// (all must be satisfied), a per-check random trigger chance, and a minimum interval
/// between checks. When triggered it applies its effects to the world.
/// </summary>
public sealed class WorldEventChapter
{
    public required string Id { get; init; }
    public IReadOnlyList<WorldEventRequirement> Requirements { get; init; } = [];
    public long CheckIntervalTicks { get; init; } = 600L;
    public double TriggerChancePerCheck { get; init; } = 0.01;
    public IReadOnlyList<WorldEventEffect> Effects { get; init; } = [];
}
