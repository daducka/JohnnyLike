using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Island.Stats;
using JohnnyLike.Domain.Island.Supply;
using System.Text.Json;

namespace JohnnyLike.Domain.Island;

public enum Weather
{
    Clear,
    Rainy,
    Windy
}

public enum TideLevel
{
    Low,
    High
}

public class IslandWorldState : WorldState
{
    public double CurrentTime { get; set; } = 0.0;

    public List<WorldItem> WorldItems { get; set; } = new();
    public List<WorldStat> WorldStats { get; set; } = new();

    public CampfireItem? MainCampfire => WorldItems.OfType<CampfireItem>().FirstOrDefault();
    public ShelterItem? MainShelter => WorldItems.OfType<ShelterItem>().FirstOrDefault();
    public TreasureChestItem? TreasureChest => WorldItems.OfType<TreasureChestItem>().FirstOrDefault();
    public SharkItem? Shark => WorldItems.OfType<SharkItem>().FirstOrDefault();
    public SupplyPile? SharedSupplyPile => WorldItems.OfType<SupplyPile>()
        .FirstOrDefault(p => p.AccessControl == "shared");

    /// <summary>
    /// Get a stat by its ID and optionally cast to a specific type
    /// </summary>
    public T? GetStat<T>(string id) where T : WorldStat
    {
        return WorldStats.FirstOrDefault(s => s.Id == id) as T;
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
    /// Topologically sort stats based on their dependencies
    /// </summary>
    private List<WorldStat> TopologicalSortStats()
    {
        var sorted = new List<WorldStat>();
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>();
        var statById = WorldStats.ToDictionary(s => s.Id);
        var path = new List<string>(); // Track current dependency path for better error messages

        void Visit(WorldStat stat)
        {
            if (visited.Contains(stat.Id))
                return;

            if (visiting.Contains(stat.Id))
            {
                // Build a clear error message showing the cycle
                var cycleStart = path.IndexOf(stat.Id);
                var cycle = string.Join(" -> ", path.Skip(cycleStart).Append(stat.Id));
                throw new InvalidOperationException(
                    $"Circular dependency detected in WorldStats: {cycle}");
            }

            visiting.Add(stat.Id);
            path.Add(stat.Id);

            foreach (var depId in stat.GetDependencies())
            {
                if (statById.TryGetValue(depId, out var depStat))
                {
                    Visit(depStat);
                }
            }

            path.RemoveAt(path.Count - 1);
            visiting.Remove(stat.Id);
            visited.Add(stat.Id);
            sorted.Add(stat);
        }

        foreach (var stat in WorldStats)
        {
            Visit(stat);
        }

        return sorted;
    }

    public List<TraceEvent> OnTimeAdvanced(double currentTime, double dt, IResourceAvailability? resourceAvailability = null)
    {
        CurrentTime = currentTime;
        var traceEvents = new List<TraceEvent>();

        // Tick all stats in dependency order and collect trace events
        var sortedStats = TopologicalSortStats();
        foreach (var stat in sortedStats)
        {
            var statEvents = stat.Tick(dt, this, currentTime);
            traceEvents.AddRange(statEvents);
        }

        // Track campfire state before ticking items
        var campfireLitBeforeTick = MainCampfire?.IsLit ?? false;
        
        // Track which items exist before ticking (for expiration detection)
        var itemsBeforeTick = WorldItems.OfType<MaintainableWorldItem>().ToList();

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

        // Remove expired maintainable items after tick cycle to avoid collection modification during iteration
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
        var serializedStats = WorldStats.Select(stat => stat.SerializeToDict()).ToList();

        var options = new JsonSerializerOptions
        {
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        return JsonSerializer.Serialize(new
        {
            WorldItems = serializedItems,
            WorldStats = serializedStats
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
                        "campfire" => new CampfireItem(id),
                        "shelter" => new ShelterItem(id),
                        "treasure_chest" => new TreasureChestItem(id),
                        "shark" => new SharkItem(id),
                        "supply_pile" => new SupplyPile(id),
                        _ => null
                    };

                    if (item != null)
                    {
                        item.DeserializeFromDict(itemData);
                        WorldItems.Add(item);
                    }
                }
            }
        }

        WorldStats.Clear();
        if (data.TryGetValue("WorldStats", out var statsElement))
        {
            var statsList = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(statsElement.GetRawText());
            if (statsList != null)
            {
                foreach (var statData in statsList)
                {
                    var type = statData["Type"].GetString()!;
                    var id = statData["Id"].GetString()!;

                    WorldStat? stat = type switch
                    {
                        "stat_time_of_day" => new TimeOfDayStat(),
                        "stat_weather" => new WeatherStat(),
                        "stat_tide" => new TideStat(),
                        "stat_fish_population" => new FishPopulationStat(),
                        "stat_coconut_availability" => new CoconutAvailabilityStat(),
                        "stat_driftwood" => new DriftwoodAvailabilityStat(),
                        _ => null
                    };

                    if (stat != null)
                    {
                        stat.DeserializeFromDict(statData);
                        WorldStats.Add(stat);
                    }
                }
            }
        }
    }
}
