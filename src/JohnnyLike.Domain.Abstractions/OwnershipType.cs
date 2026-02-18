namespace JohnnyLike.Domain.Abstractions;

public enum OwnershipType
{
    Shared,     // Multiple actors can use/maintain (e.g., campfire, shelter)
    Exclusive   // Single actor ownership (e.g., fishing pole)
}
