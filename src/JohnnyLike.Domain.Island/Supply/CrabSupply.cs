using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Domain.Island.Supply;

/// <summary>
/// A live crab caught by a human actor. Can be used as food or traded.
/// Produced when a human successfully catches a <see cref="CrabActorState"/>.
/// </summary>
public class CrabSupply : SupplyItem
{
    public CrabSupply(double quantity)
        : this("crab", quantity)
    {
    }

    public CrabSupply(string id = "crab", double quantity = 0.0)
        : base(id, "supply_crab", quantity)
    {
    }
}
