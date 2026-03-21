namespace JohnnyLike.Domain.Island.Supply;

/// <summary>
/// Capability interface for supply items that provide immediate caloric relief when consumed.
/// Implemented by supplies already in an actor's accessible pile (e.g., CoconutSupply, FishSupply).
///
/// Food need scoring queries this interface to compute <c>ImmediateFoodAvailability</c> without
/// hardcoding specific supply types.  To add a new edible supply, implement this interface —
/// no changes to <c>BuildQualityModel</c> are required.
/// </summary>
public interface IEdibleSupply
{
    /// <summary>
    /// Returns the number of food units available for immediate consumption,
    /// given the current actor and world state.
    /// Each unit represents one nominal serving (e.g., 1 fish, 1 coconut).
    /// </summary>
    double GetImmediateFoodUnits(IslandActorState actor, IslandWorldState world);
}
