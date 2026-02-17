using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Engine;

/// <summary>
/// Implementation of IResourceReservationService that wraps ReservationTable
/// for use by domain code (e.g., world items reserving resources).
/// </summary>
internal class ResourceReservationService : IResourceReservationService
{
    private readonly ReservationTable _reservationTable;
    private readonly Func<double> _getCurrentTime;

    public ResourceReservationService(ReservationTable reservationTable, Func<double> getCurrentTime)
    {
        _reservationTable = reservationTable;
        _getCurrentTime = getCurrentTime;
    }

    public bool TryReserve(ResourceId resourceId, ReservationOwner owner, double until)
    {
        // Use a synthetic SceneId for world item reservations
        // We use the owner ID to ensure consistent scene IDs for the same world item
        var sceneId = new SceneId($"world_item:{owner.Id}");
        return _reservationTable.TryReserve(resourceId, sceneId, owner, until);
    }

    public void Release(ResourceId resourceId)
    {
        _reservationTable.Release(resourceId);
    }

    public bool IsReserved(ResourceId resourceId)
    {
        return _reservationTable.IsReserved(resourceId);
    }
}
