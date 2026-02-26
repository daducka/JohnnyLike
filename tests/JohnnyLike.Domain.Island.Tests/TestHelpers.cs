using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Domain.Island.Tests;

/// <summary>
/// Test helper that provides an empty resource availability implementation.
/// Returns false for all IsReserved queries (no resources are reserved).
/// Note: This helper is duplicated in Office.Tests to avoid cross-project test dependencies.
/// </summary>
public class EmptyResourceAvailability : IResourceAvailability
{
    public bool IsReserved(ResourceId resourceId) => false;
    public bool TryReserve(ResourceId resourceId, string utilityId, long until) => true;
    public void Release(ResourceId resourceId) { }
}
