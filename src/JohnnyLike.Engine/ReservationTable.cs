using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Engine;

public class ReservationTable : IResourceAvailability
{
    private readonly Dictionary<ResourceId, (SceneId Scene, ReservationOwner? Owner, double Until)> _reservations = new();

    public bool TryReserve(ResourceId resourceId, SceneId sceneId, ReservationOwner? owner, double until)
    {
        if (_reservations.TryGetValue(resourceId, out var existing))
        {
            return false;
        }

        _reservations[resourceId] = (sceneId, owner, until);
        return true;
    }

    public void Release(ResourceId resourceId)
    {
        _reservations.Remove(resourceId);
    }

    public void ReleaseByScene(SceneId sceneId)
    {
        var toRelease = _reservations
            .Where(kvp => kvp.Value.Scene == sceneId)
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
