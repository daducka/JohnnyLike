namespace JohnnyLike.Domain.Abstractions;

/// <summary>
/// Provides read-only access to resource reservation status.
/// Used by domain candidate providers to check if resources are available.
/// </summary>
public interface IResourceAvailability
{
    /// <summary>
    /// Checks if a resource is currently reserved.
    /// </summary>
    /// <param name="resourceId">The resource to check.</param>
    /// <returns>True if the resource is reserved, false otherwise.</returns>
    bool IsReserved(ResourceId resourceId);
}
