using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Island.Loot;

namespace JohnnyLike.Domain.Island.Events;

/// <summary>
/// Three-chapter world event script for wreckage washing ashore.
/// </summary>
public sealed class WreckageEventScript : WorldEventScript
{
    private static readonly IReadOnlyList<WorldEventChapter> _chapters =
    [
        new WorldEventChapter
        {
            Id = "wreckage_chapter_1",
            Requirements =
            [
                new MinDayRequirement { MinDay = 3 }
            ],
            CheckIntervalTicks = 600L,
            TriggerChancePerCheck = 0.005,
            Effects =
            [
                new SpawnLootEffect
                {
                    ItemId = "loot_wreckage_day3",
                    RoomId = "beach",
                    LootKind = LootKind.WreckageMetalScrap,
                    WorldNarration = "A mangled piece of wreckage washes on shore."
                }
            ]
        },
        new WorldEventChapter
        {
            Id = "wreckage_chapter_2",
            Requirements =
            [
                new MinDayRequirement { MinDay = 5 },
                new ChapterTriggeredRequirement { ChapterId = "wreckage_chapter_1" }
            ],
            CheckIntervalTicks = 600L,
            TriggerChancePerCheck = 0.005,
            Effects =
            [
                new SpawnLootEffect
                {
                    ItemId = "loot_wreckage_day5",
                    RoomId = "beach",
                    LootKind = LootKind.WreckageRadioCache,
                    WorldNarration = "More wreckage drifts onto the beach, this time heavier and waterlogged."
                }
            ]
        },
        new WorldEventChapter
        {
            Id = "wreckage_chapter_3",
            Requirements =
            [
                new ChapterTriggeredRequirement { ChapterId = "wreckage_chapter_2" },
                new WeatherRequirement { Required = PrecipitationBand.Rainy }
            ],
            CheckIntervalTicks = 600L,
            TriggerChancePerCheck = 0.005,
            Effects =
            [
                new SpawnLootEffect
                {
                    ItemId = "loot_corpse_washup",
                    RoomId = "beach",
                    LootKind = LootKind.CorpseWashup,
                    WorldNarration = "Something large and dark washes up on the beach."
                }
            ]
        }
    ];

    public override IReadOnlyList<WorldEventChapter> Chapters => _chapters;
}
