namespace JohnnyLike.Domain.Abstractions;

/// <summary>
/// Represents an entity that can own a resource reservation.
/// Can be either an actor or a world item.
/// </summary>
public readonly record struct ReservationOwner
{
    public enum OwnerType
    {
        Actor,
        WorldItem
    }

    public OwnerType Type { get; init; }
    public string Id { get; init; }

    private ReservationOwner(OwnerType type, string id)
    {
        Type = type;
        Id = id;
    }

    public static ReservationOwner FromActor(ActorId actorId)
    {
        return new ReservationOwner(OwnerType.Actor, actorId.Value);
    }

    public static ReservationOwner FromWorldItem(string worldItemId)
    {
        return new ReservationOwner(OwnerType.WorldItem, worldItemId);
    }

    public ActorId? AsActorId()
    {
        return Type == OwnerType.Actor ? new ActorId(Id) : null;
    }

    public override string ToString()
    {
        return Type == OwnerType.Actor ? $"actor:{Id}" : $"world_item:{Id}";
    }
}
