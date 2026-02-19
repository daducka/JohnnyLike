namespace JohnnyLike.Domain.Abstractions;

/// <summary>
/// Marker interface for objects that can provide action candidates.
/// This abstraction allows items, actors, and other domain objects to contribute
/// actions to the decision-making process.
/// Domain-specific implementations should provide concrete methods with their
/// specific context types.
/// </summary>
public interface IActionCandidate
{
}
