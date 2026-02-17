using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Domain.Island;

/// <summary>
/// Null implementation of IResourceReservationService that does nothing.
/// Used as a fallback in tests or when no engine is present.
/// </summary>
internal class NullResourceReservationService : IResourceReservationService
{
    public static readonly NullResourceReservationService Instance = new();

    private NullResourceReservationService() { }

    public bool TryReserve(ResourceId resourceId, ReservationOwner owner, double until)
    {
        // Always succeed but don't actually reserve anything
        return true;
    }

    public void Release(ResourceId resourceId)
    {
        // Do nothing
    }

    public bool IsReserved(ResourceId resourceId)
    {
        // Never reserved
        return false;
    }
}
