using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island;

public class EffectContext
{
    public required ActorId ActorId { get; init; }
    public required ActionOutcome Outcome { get; init; }
    public required IslandActorState Actor { get; init; }
    public required IslandWorldState World { get; init; }
    public RollOutcomeTier? Tier { get; init; }
    public required IRngStream Rng { get; init; }
    public required IResourceReservationService ReservationService { get; init; }
}
