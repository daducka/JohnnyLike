using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Domain.Island.Loot;

/// <summary>
/// Defines the properties and rewards of a <see cref="LootItem"/> identified by <see cref="LootKind"/>.
/// </summary>
public sealed class LootDefinition
{
    public required LootKind Kind { get; init; }
    public required string DisplayName { get; init; }
    public required string ActionId { get; init; }
    public required string ActionNarrationDescription { get; init; }
    public required Duration Duration { get; init; }
    public double IntrinsicScore { get; init; }
    public IReadOnlyDictionary<QualityType, double> Qualities { get; init; } = new Dictionary<QualityType, double>();
    public IReadOnlyList<LootDropDefinition> Drops { get; init; } = [];
    public required string SuccessNarration { get; init; }
}
