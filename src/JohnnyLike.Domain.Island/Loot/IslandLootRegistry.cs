using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Items;

namespace JohnnyLike.Domain.Island.Loot;

/// <summary>Maps <see cref="LootKind"/> values to their <see cref="LootDefinition"/>s.</summary>
public static class IslandLootRegistry
{
    public static LootDefinition Get(LootKind kind) => kind switch
    {
        LootKind.WreckageMetalScrap => new LootDefinition
        {
            Kind = LootKind.WreckageMetalScrap,
            DisplayName = "Mangled wreckage",
            ActionId = "investigate_wreckage",
            ActionNarrationDescription = "investigate the wreckage",
            Duration = Duration.Minutes(20),
            IntrinsicScore = 0.22,
            Qualities = new Dictionary<QualityType, double>
            {
                [QualityType.Preparation] = 0.6,
                [QualityType.Mastery]     = 0.2,
                [QualityType.Efficiency]  = 0.2
            },
            Drops =
            [
                new SupplyLootDrop
                {
                    SupplyKind = SupplyKind.MetalScrap,
                    Quantity   = 12
                }
            ],
            SuccessNarration =
                "{actor} digs through the wreckage and organizes the metal scraps into a useful pile."
        },

        LootKind.WreckageRadioCache => new LootDefinition
        {
            Kind = LootKind.WreckageRadioCache,
            DisplayName = "Waterlogged wreckage",
            ActionId = "investigate_wreckage",
            ActionNarrationDescription = "investigate the wreckage",
            Duration = Duration.Minutes(20),
            IntrinsicScore = 0.22,
            Qualities = new Dictionary<QualityType, double>
            {
                [QualityType.Preparation] = 0.6,
                [QualityType.Mastery]     = 0.2,
                [QualityType.Efficiency]  = 0.2
            },
            Drops =
            [
                new SupplyLootDrop
                {
                    SupplyKind = SupplyKind.MetalScrap,
                    Quantity   = 8
                },
                new WorldItemLootDrop
                {
                    ItemId  = "broken_radio",
                    Factory = id => new BrokenRadioItem(id)
                }
            ],
            SuccessNarration =
                "{actor} digs through the wreckage and finds some metal scraps and a broken radio."
        },

        LootKind.CorpseWashup => new LootDefinition
        {
            Kind = LootKind.CorpseWashup,
            DisplayName = "A body washed ashore",
            ActionId = "investigate_body",
            ActionNarrationDescription = "investigate the body",
            Duration = Duration.Minutes(15),
            IntrinsicScore = 0.18,
            Qualities = new Dictionary<QualityType, double>
            {
                [QualityType.Preparation] = 0.3,
                [QualityType.Safety]      = 0.4
            },
            Drops =
            [
                new WorldItemLootDrop
                {
                    ItemId  = "corpse_washup",
                    Factory = id => new CorpseItem(id) { ActorName = "Unknown" }
                }
            ],
            SuccessNarration =
                "{actor} cautiously investigates the remains washed ashore."
        },

        LootKind.StormDebris => new LootDefinition
        {
            Kind = LootKind.StormDebris,
            DisplayName = "Storm debris",
            ActionId = "salvage_debris",
            ActionNarrationDescription = "salvage the storm debris",
            Duration = Duration.Minutes(15),
            IntrinsicScore = 0.18,
            Qualities = new Dictionary<QualityType, double>
            {
                [QualityType.Preparation] = 0.5,
                [QualityType.Efficiency]  = 0.3
            },
            Drops =
            [
                new SupplyLootDrop { SupplyKind = SupplyKind.Wood, Quantity = 10 }
            ],
            SuccessNarration =
                "{actor} gathers usable wood from the storm debris."
        },

        LootKind.SupplyCrate => new LootDefinition
        {
            Kind = LootKind.SupplyCrate,
            DisplayName = "Supply crate",
            ActionId = "open_supply_crate",
            ActionNarrationDescription = "open the supply crate",
            Duration = Duration.Minutes(10),
            IntrinsicScore = 0.28,
            Qualities = new Dictionary<QualityType, double>
            {
                [QualityType.Preparation] = 0.8,
                [QualityType.Efficiency]  = 0.4
            },
            Drops =
            [
                new SupplyLootDrop { SupplyKind = SupplyKind.Wood, Quantity = 15 }
            ],
            SuccessNarration =
                "{actor} pries open the crate and takes stock of its contents."
        },

        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}
