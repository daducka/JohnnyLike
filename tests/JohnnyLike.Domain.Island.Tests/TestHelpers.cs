using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Domain.Island.Tests;

public class EmptyResourceAvailability : IResourceAvailability
{
    public bool IsReserved(ResourceId resourceId) => false;
}
