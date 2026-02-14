namespace JohnnyLike.Domain.Abstractions;

public record Signal(
    string Type,
    double Timestamp,
    ActorId? TargetActor,
    Dictionary<string, object> Data
);
