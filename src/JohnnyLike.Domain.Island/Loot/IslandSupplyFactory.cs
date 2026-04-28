using JohnnyLike.Domain.Island.Supply;

namespace JohnnyLike.Domain.Island.Loot;

/// <summary>Creates <see cref="SupplyItem"/> instances from a <see cref="SupplyKind"/> enum value.</summary>
public static class IslandSupplyFactory
{
    public static SupplyItem Create(SupplyKind kind) => kind switch
    {
        SupplyKind.MetalScrap    => new MetalScrapSupply(),
        SupplyKind.Rope          => new RopeSupply(),
        SupplyKind.Wood          => new WoodSupply(),
        SupplyKind.Fish          => new FishSupply(),
        SupplyKind.Crab          => new CrabSupply(),
        SupplyKind.Bait          => new BaitSupply(),
        SupplyKind.CarcassScraps => new CarcassScrapsSupply(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}
