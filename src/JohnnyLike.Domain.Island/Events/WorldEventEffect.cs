using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Island.Loot;
using JohnnyLike.Domain.Island.Telemetry;

namespace JohnnyLike.Domain.Island.Events;

/// <summary>A single effect applied when a world event chapter triggers.</summary>
public abstract class WorldEventEffect
{
    public abstract void Apply(IslandWorldState world, long currentTick);
}

/// <summary>
/// Spawns a <see cref="LootItem"/> in a given room and emits a world narration beat.
/// </summary>
public sealed class SpawnLootEffect : WorldEventEffect
{
    public required string ItemId { get; init; }
    public required string RoomId { get; init; }
    public required LootKind LootKind { get; init; }
    public required string WorldNarration { get; init; }

    public override void Apply(IslandWorldState world, long currentTick)
    {
        if (world.WorldItems.Any(i => i.Id == ItemId))
            return;

        var lootItem = new LootItem(ItemId, LootKind);
        world.AddWorldItem(lootItem, RoomId);

        using (world.Tracer.PushPhase(TracePhase.WorldTick))
            world.Tracer.BeatWorld(WorldNarration, subjectId: $"world_event:{ItemId}", priority: 35);
    }
}
