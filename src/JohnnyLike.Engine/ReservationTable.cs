using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Engine;

public class ReservationTable : IResourceAvailability
{
    private readonly Dictionary<ResourceId, (string UtilityId, long Until)> _reservations = new();

    public bool TryReserve(ResourceId resourceId, string utilityId, long until)
    {
        if (_reservations.ContainsKey(resourceId))
            return false;
        _reservations[resourceId] = (utilityId, until);
        return true;
    }

    public void Release(ResourceId resourceId)
    {
        _reservations.Remove(resourceId);
    }

    public void ReleaseByPrefix(string utilityIdPrefix)
    {
        var toRelease = _reservations
            .Where(kvp => kvp.Value.UtilityId.StartsWith(utilityIdPrefix))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var rid in toRelease)
            _reservations.Remove(rid);
    }

    public bool IsReserved(ResourceId resourceId)
    {
        return _reservations.ContainsKey(resourceId);
    }

    public void CleanupExpired(long currentTick)
    {
        var expired = _reservations
            .Where(kvp => kvp.Value.Until < currentTick)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var rid in expired)
            _reservations.Remove(rid);
    }
}
