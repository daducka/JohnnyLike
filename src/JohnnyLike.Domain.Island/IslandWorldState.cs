using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Island.Supply;
using System.Text.Json;

namespace JohnnyLike.Domain.Island;

public class IslandWorldState : WorldState
{
    public double CurrentTime { get; set; } = 0.0;

    public List<WorldItem> WorldItems { get; set; } = new();

    public CampfireItem? MainCampfire => WorldItems.OfType<CampfireItem>().FirstOrDefault();
    public ShelterItem? MainShelter => WorldItems.OfType<ShelterItem>().FirstOrDefault();
    public TreasureChestItem? TreasureChest => WorldItems.OfType<TreasureChestItem>().FirstOrDefault();
    public SharkItem? Shark => WorldItems.OfType<SharkItem>().FirstOrDefault();
    public SupplyPile? SharedSupplyPile => WorldItems.OfType<SupplyPile>()
        .FirstOrDefault(p => p.AccessControl == "shared");

    /// <summary>
    /// Get a WorldItem by its ID and type.
    /// </summary>
    public T? GetItem<T>(string id) where T : WorldItem
    {
        return WorldItems.OfType<T>().FirstOrDefault(x => x.Id == id);
    }

    /// <summary>
    /// Get all supply piles that the given actor can access
    /// </summary>
    public List<SupplyPile> GetAccessiblePiles(ActorId actorId)
    {
        return WorldItems.OfType<SupplyPile>()
            .Where(p => p.CanAccess(actorId))
            .ToList();
    }

    /// <summary>
    /// Topologically sort ITickableWorldItems based on their dependencies.
    /// </summary>
    private List<ITickableWorldItem> TopologicalSortTickables()
    {
        var tickables = WorldItems.OfType<ITickableWorldItem>().ToList();
        var sorted = new List<ITickableWorldItem>();
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>();
        var itemById = tickables
            .Select(t => (item: t, id: ((WorldItem)t).Id))
            .ToDictionary(x => x.id, x => x.item);
        var path = new List<string>();

        void Visit(ITickableWorldItem tickable)
        {
            var id = ((WorldItem)tickable).Id;
            if (visited.Contains(id)) return;

            if (visiting.Contains(id))
            {
                var cycleStart = path.IndexOf(id);
                var cycle = string.Join(" -> ", path.Skip(cycleStart).Append(id));
                throw new InvalidOperationException(
                    $"Circular dependency detected in WorldItems: {cycle}");
            }

            visiting.Add(id);
            path.Add(id);

            foreach (var depId in tickable.GetDependencies())
            {
                if (itemById.TryGetValue(depId, out var dep))
                    Visit(dep);
            }

            path.RemoveAt(path.Count - 1);
            visiting.Remove(id);
            visited.Add(id);
            sorted.Add(tickable);
        }

        foreach (var tickable in tickables)
            Visit(tickable);

        return sorted;
    }

    public List<TraceEvent> OnTimeAdvanced(double currentTime, double dt, IResourceAvailability? resourceAvailability = null)
    {
        CurrentTime = currentTime;
        var traceEvents = new List<TraceEvent>();

        // Tick all ITickableWorldItems in dependency order
        var sortedTickables = TopologicalSortTickables();
        foreach (var tickable in sortedTickables)
        {
            var events = tickable.Tick(dt, this, currentTime);
            traceEvents.AddRange(events);
        }

        // Track campfire state before ticking items
        var campfireLitBeforeTick = MainCampfire?.IsLit ?? false;

        // Tick maintainable items
        foreach (var item in WorldItems.OfType<MaintainableWorldItem>())
        {
            item.Tick(dt, this);
        }

        // Campfire extinguished trace event
        var campfire = MainCampfire;
        if (campfireLitBeforeTick && campfire != null && !campfire.IsLit)
        {
            traceEvents.Add(new TraceEvent(
                currentTime,
                null,
                "CampfireExtinguished",
                new Dictionary<string, object>
                {
                    ["itemId"] = campfire.Id,
                    ["quality"] = Math.Round(campfire.Quality, 2)
                }
            ));
        }

        // Remove expired maintainable items after tick cycle
        var expiredItems = WorldItems.OfType<MaintainableWorldItem>().Where(item => item.IsExpired).ToList();
        foreach (var item in expiredItems)
        {
            traceEvents.Add(new TraceEvent(
                currentTime,
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

    public override string Serialize()
    {
        var serializedItems = WorldItems.Select(item => item.SerializeToDict()).ToList();

        var options = new JsonSerializerOptions
        {
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        return JsonSerializer.Serialize(new
        {
            WorldItems = serializedItems
        }, options);
    }

    public override void Deserialize(string json)
    {
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        if (data == null) return;

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

                    WorldItem? item = type switch
                    {
                        "campfire"       => new CampfireItem(id),
                        "shelter"        => new ShelterItem(id),
                        "fishing_pole"   => new FishingPoleItem(id),
                        "treasure_chest" => new TreasureChestItem(id),
                        "shark"          => new SharkItem(id),
                        "supply_pile"    => new SupplyPile(id),
                        "umbrella_tool"  => new UmbrellaItem(id),
                        "calendar"       => new CalendarItem(id),
                        "weather"        => new WeatherItem(id),
                        "beach"          => new BeachItem(id),
                        "palm_tree"      => new CoconutTreeItem(id),
                        _                => null
                    };

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
