namespace JohnnyLike.Domain.Island;

/// <summary>
/// Capability interface for world items that can yield food to actors soon
/// (e.g., coconut trees, fishing poles, future traps or berry bushes).
///
/// Food need scoring queries this interface to compute <c>AcquirableFoodAvailability</c>
/// without hardcoding specific item types or IDs.  To add a new food source world item,
/// implement this interface — no changes to <c>BuildQualityModel</c> are required.
///
/// Note: implement this on the <em>action-providing</em> item (e.g., the fishing pole that
/// checks ownership and pole condition before offering <c>go_fishing</c>), not on the
/// underlying resource reservoir (e.g., the ocean bounty which only stores fish quantities).
/// </summary>
public interface IFoodSource
{
    /// <summary>
    /// Returns the estimated number of food units likely acquirable soon from this source,
    /// given the current actor and world state.
    /// Each unit represents one nominal serving (e.g., 1 fish, 1 coconut).
    /// </summary>
    double GetAcquirableFoodUnits(IslandActorState actor, IslandWorldState world);
}
