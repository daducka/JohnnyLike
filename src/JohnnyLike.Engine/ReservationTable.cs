using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Engine;

public class ReservationTable
{
    private readonly Dictionary<ResourceId, (ActorId Actor, SceneId? Scene, double Until)> _reservations = new();

    public bool TryReserve(ResourceId resourceId, ActorId actorId, SceneId? sceneId, double until)
    {
        if (_reservations.TryGetValue(resourceId, out var existing))
        {
            return false;
        }

        _reservations[resourceId] = (actorId, sceneId, until);
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
