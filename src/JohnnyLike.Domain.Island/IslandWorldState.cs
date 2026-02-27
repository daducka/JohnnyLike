using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Island.Supply;
using System.Text.Json;

namespace JohnnyLike.Domain.Island;

public class IslandWorldState : WorldState
{
    /// <summary>Current tick (set by engine via OnTickAdvanced).</summary>
    public long CurrentTick { get; set; } = 0L;
    private long _prevTick = 0L;

    public List<WorldItem> WorldItems { get; set; } = new();

    public CampfireItem? MainCampfire => WorldItems.OfType<CampfireItem>().FirstOrDefault();
    public ShelterItem? MainShelter => WorldItems.OfType<ShelterItem>().FirstOrDefault();
    public TreasureChestItem? TreasureChest => WorldItems.OfType<TreasureChestItem>().FirstOrDefault();
    public SharkItem? Shark => WorldItems.OfType<SharkItem>().FirstOrDefault();
    public SupplyPile? SharedSupplyPile => WorldItems.OfType<SupplyPile>()
        .FirstOrDefault(p => p.AccessControl == "shared");

    public T? GetItem<T>(string id) where T : WorldItem
    {
        return WorldItems.OfType<T>().FirstOrDefault(x => x.Id == id);
    }

    public List<SupplyPile> GetAccessiblePiles(ActorId actorId)
    {
        return WorldItems.OfType<SupplyPile>()
            .Where(p => p.CanAccess(actorId))
            .ToList();
    }

    public List<TraceEvent> OnTickAdvanced(long currentTick, IResourceAvailability? resourceAvailability = null)
    {
        CurrentTick = currentTick;
        var dtTicks = currentTick - _prevTick;
        _prevTick = currentTick;
        var traceEvents = new List<TraceEvent>();

        // Note: ITickableWorldItem ticking is handled by the engine via WorldItemTickOrchestrator
        // before this method is called.

        var campfireLitBeforeTick = MainCampfire?.IsLit ?? false;

        // Tick maintainable items in stable order
        foreach (var item in WorldItems.OfType<MaintainableWorldItem>().OrderBy(i => i.Id))
        {
            item.Tick(dtTicks, this);
        }

        var campfire = MainCampfire;
        if (campfireLitBeforeTick && campfire != null && !campfire.IsLit)
        {
            traceEvents.Add(new TraceEvent(
                currentTick,
                null,
                "CampfireExtinguished",
                new Dictionary<string, object>
                {
                    ["itemId"] = campfire.Id,
                    ["quality"] = Math.Round(campfire.Quality, 2)
                }
            ));
        }

        var expiredItems = WorldItems.OfType<MaintainableWorldItem>().Where(item => item.IsExpired).ToList();
        foreach (var item in expiredItems)
        {
            traceEvents.Add(new TraceEvent(
                currentTick,
                null,
                "WorldItemExpired",
                new Dictionary<string, object>
                {
                    ["itemId"] = item.Id,
                    ["itemType"] = item.Type,
                    ["quality"] = Math.Round(item.Quality, 2)
                }
            ));

            item.PerformExpiration(this, resourceAvailability);
            WorldItems.Remove(item);
        }

        return traceEvents;
    }

    public override IReadOnlyList<WorldItem> GetAllItems() => WorldItems;

    public override string Serialize()
    {
        var serializedItems = WorldItems.Select(item => item.SerializeToDict()).ToList();

        var options = new JsonSerializerOptions
        {
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        return JsonSerializer.Serialize(new
        {
            CurrentTick,
            WorldItems = serializedItems
        }, options);
    }

    public override void Deserialize(string json)
    {
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        if (data == null) return;

        if (data.TryGetValue("CurrentTick", out var tickEl))
        {
            CurrentTick = tickEl.GetInt64();
            _prevTick = CurrentTick;
        }

        WorldItems.Clear();
        if (data.TryGetValue("WorldItems", out var itemsElement))
        {
            var itemsList = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(itemsElement.GetRawText());
            if (itemsList != null)
            {
                foreach (var itemData in itemsList)
                {
                    var type = itemData["Type"].GetString()!;
                    var id = itemData["Id"].GetString()!;

                    var item = WorldItemTypeRegistry.Create(type, id);
                    if (item != null)
                    {
                        item.DeserializeFromDict(itemData);
                        WorldItems.Add(item);
                    }
                }
            }
        }
    }
}
