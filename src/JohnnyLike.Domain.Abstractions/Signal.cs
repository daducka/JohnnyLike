namespace JohnnyLike.Domain.Abstractions;

public record Signal(
    string Type,
    long Tick,
    ActorId? TargetActor,
    Dictionary<string, object> Data
);
