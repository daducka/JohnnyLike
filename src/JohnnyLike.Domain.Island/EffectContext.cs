using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island;

/// <summary>
/// Island domain-specific effect context that extends the base EffectContext with roll outcome tier information.
/// </summary>
public class EffectContext : EffectContext<LivingActorState, IslandWorldState>
{
    public RollOutcomeTier? Tier { get; init; }
}
