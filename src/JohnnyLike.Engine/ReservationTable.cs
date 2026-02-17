using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Engine;

public class ReservationTable : IResourceAvailability
{
    private readonly Dictionary<ResourceId, (string UtilityId, double Until)> _reservations = new();

    /// <summary>
    /// Reserve a resource with a utility ID for debugging/tracking.
    /// </summary>
    public bool TryReserve(ResourceId resourceId, string utilityId, double until)
    {
        if (_reservations.ContainsKey(resourceId))
        {
            return false;
        }

        _reservations[resourceId] = (utilityId, until);
        return true;
    }

    public void Release(ResourceId resourceId)
    {
        _reservations.Remove(resourceId);
    }

    /// <summary>
    /// Releases all resources that have a utilityId starting with the given prefix.
    /// Used for batch release (e.g., all resources for a scene or actor).
    /// </summary>
    public void ReleaseByPrefix(string utilityIdPrefix)
    {
        var toRelease = _reservations
            .Where(kvp => kvp.Value.UtilityId.StartsWith(utilityIdPrefix))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var rid in toRelease)
        {
            _reservations.Remove(rid);
        }
    }

    public bool IsReserved(ResourceId resourceId)
    {
        return _reservations.ContainsKey(resourceId);
    }

    public void CleanupExpired(double currentTime)
    {
        var expired = _reservations
            .Where(kvp => kvp.Value.Until < currentTime)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var rid in expired)
        {
            _reservations.Remove(rid);
        }
    }
}
