namespace JohnnyLike.Domain.Island.Supply;

/// <summary>
/// Carries the reservation state from a bounty-collection PreAction into the EffectHandler.
/// <para>
/// Because the Director releases actor-level action reservations <em>before</em>
/// <c>ApplyActionEffects</c> is called, the ISupplyBounty reservation key cannot be stored in the
/// Director's own reservation table. Instead it is embedded in this context object, which is
/// captured by both the PreAction and EffectHandler lambdas so it survives until Effect time.
/// </para>
/// </summary>
/// <param name="Source">The bounty that holds the reservation.</param>
/// <param name="ReservationKey">
/// Key under which supplies were reserved in <see cref="ISupplyBounty.ActiveReservations"/>.
/// </param>
public sealed record BountyCollectionContext(
    ISupplyBounty Source,
    string ReservationKey);
