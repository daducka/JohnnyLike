using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Engine;

public class ReservationTable : IResourceAvailability
{
    // Simple resource -> expiry time mapping for world items and simple reservations
    private readonly Dictionary<ResourceId, double> _simpleReservations = new();
    
    // Scene-grouped reservations for actor actions (allows batch release)
    private readonly Dictionary<ResourceId, (SceneId Scene, ReservationOwner Owner, double Until)> _sceneReservations = new();

    /// <summary>
    /// Simple reserve for world items - no scene grouping needed.
    /// </summary>
    public bool TryReserve(ResourceId resourceId, double until)
    {
        if (_simpleReservations.ContainsKey(resourceId) || _sceneReservations.ContainsKey(resourceId))
        {
            return false;
        }

        _simpleReservations[resourceId] = until;
        return true;
    }

    /// <summary>
    /// Reserve with scene grouping for actor actions.
    /// </summary>
    public bool TryReserveForScene(ResourceId resourceId, SceneId sceneId, ReservationOwner owner, double until)
    {
        if (_simpleReservations.ContainsKey(resourceId) || _sceneReservations.ContainsKey(resourceId))
        {
            return false;
        }

        _sceneReservations[resourceId] = (sceneId, owner, until);
        return true;
    }

    public void Release(ResourceId resourceId)
    {
        _simpleReservations.Remove(resourceId);
        _sceneReservations.Remove(resourceId);
    }

    public void ReleaseByScene(SceneId sceneId)
    {
        var toRelease = _sceneReservations
            .Where(kvp => kvp.Value.Scene == sceneId)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var rid in toRelease)
        {
            _sceneReservations.Remove(rid);
        }
    }

    public bool IsReserved(ResourceId resourceId)
    {
        return _simpleReservations.ContainsKey(resourceId) || _sceneReservations.ContainsKey(resourceId);
    }

    public void CleanupExpired(double currentTime)
    {
        var expiredSimple = _simpleReservations
            .Where(kvp => kvp.Value < currentTime)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var rid in expiredSimple)
        {
            _simpleReservations.Remove(rid);
        }

        var expiredScene = _sceneReservations
            .Where(kvp => kvp.Value.Until < currentTime)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var rid in expiredScene)
        {
            _sceneReservations.Remove(rid);
        }
    }
}
