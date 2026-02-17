namespace JohnnyLike.Domain.Abstractions;

/// <summary>
/// Provides access to resource reservations.
/// Used by domains to check availability and manage reservations.
/// </summary>
public interface IResourceAvailability
{
    /// <summary>
    /// Checks if a resource is currently reserved.
    /// </summary>
    /// <param name="resourceId">The resource to check.</param>
    /// <returns>True if the resource is reserved, false otherwise.</returns>
    bool IsReserved(ResourceId resourceId);

    /// <summary>
    /// Attempts to reserve a resource until a specific time.
    /// </summary>
    /// <param name="resourceId">The resource to reserve.</param>
    /// <param name="until">Time until which the resource should be reserved.</param>
    /// <returns>True if reservation succeeded, false if resource is already reserved.</returns>
    bool TryReserve(ResourceId resourceId, double until);

    /// <summary>
    /// Releases a resource reservation.
    /// </summary>
    /// <param name="resourceId">The resource to release.</param>
    void Release(ResourceId resourceId);
}
