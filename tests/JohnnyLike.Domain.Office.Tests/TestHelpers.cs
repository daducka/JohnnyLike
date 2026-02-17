using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Domain.Office.Tests;

public class EmptyResourceAvailability : IResourceAvailability
{
    public bool IsReserved(ResourceId resourceId) => false;
}
