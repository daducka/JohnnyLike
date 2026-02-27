namespace JohnnyLike.Domain.Abstractions;

public readonly record struct ActorId(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct ResourceId(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct ActionId(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct AnchorId(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct RoomId(string Value)
{
    public override string ToString() => Value;
}
