namespace JohnnyLike.Domain.Abstractions;

/// <summary>
/// Service for managing resource reservations from domain code.
/// Allows world items and other domain entities to reserve and release resources.
/// </summary>
public interface IResourceReservationService
{
    /// <summary>
    /// Attempts to reserve a resource for a world item or other non-actor entity.
    /// </summary>
    /// <param name="resourceId">The resource to reserve.</param>
    /// <param name="owner">The owner of the reservation (typically a world item).</param>
    /// <param name="until">Time until which the resource should be reserved.</param>
    /// <returns>True if reservation succeeded, false if resource is already reserved.</returns>
    bool TryReserve(ResourceId resourceId, ReservationOwner owner, double until);

    /// <summary>
    /// Releases a resource reservation.
    /// </summary>
    /// <param name="resourceId">The resource to release.</param>
    void Release(ResourceId resourceId);

    /// <summary>
    /// Checks if a resource is currently reserved.
    /// </summary>
    /// <param name="resourceId">The resource to check.</param>
    /// <returns>True if the resource is reserved, false otherwise.</returns>
    bool IsReserved(ResourceId resourceId);
}
