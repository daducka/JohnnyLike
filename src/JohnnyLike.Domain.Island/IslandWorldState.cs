using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Island.Stats;
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

    // Helper properties for backward-compatible access to stat values
    public double TimeOfDay
    {
        get => GetStat<TimeOfDayStat>("time_of_day")?.TimeOfDay ?? 0.5;
        set
        {
            var stat = GetStat<TimeOfDayStat>("time_of_day");
            if (stat != null) stat.TimeOfDay = value;
        }
    }

    public int DayCount
    {
        get => GetStat<TimeOfDayStat>("time_of_day")?.DayCount ?? 0;
        set
        {
            var stat = GetStat<TimeOfDayStat>("time_of_day");
            if (stat != null) stat.DayCount = value;
        }
    }

    public Weather Weather
    {
        get => GetStat<WeatherStat>("weather")?.Weather ?? Weather.Clear;
        set
        {
            var stat = GetStat<WeatherStat>("weather");
            if (stat != null) stat.Weather = value;
        }
    }

    public double FishAvailable
    {
        get => GetStat<FishPopulationStat>("fish_population")?.FishAvailable ?? 100.0;
        set
        {
            var stat = GetStat<FishPopulationStat>("fish_population");
            if (stat != null) stat.FishAvailable = value;
        }
    }

    public double FishRegenRatePerMinute
    {
        get => GetStat<FishPopulationStat>("fish_population")?.FishRegenRatePerMinute ?? 5.0;
        set
        {
            var stat = GetStat<FishPopulationStat>("fish_population");
            if (stat != null) stat.FishRegenRatePerMinute = value;
        }
    }

    public int CoconutsAvailable
    {
        get => GetStat<CoconutAvailabilityStat>("coconut_availability")?.CoconutsAvailable ?? 5;
        set
        {
            var stat = GetStat<CoconutAvailabilityStat>("coconut_availability");
            if (stat != null) stat.CoconutsAvailable = value;
        }
    }

    public TideLevel TideLevel
    {
        get => GetStat<TideStat>("tide")?.TideLevel ?? TideLevel.Low;
        set
        {
            var stat = GetStat<TideStat>("tide");
            if (stat != null) stat.TideLevel = value;
        }
    }

    public CampfireItem? MainCampfire => WorldItems.OfType<CampfireItem>().FirstOrDefault();
    public ShelterItem? MainShelter => WorldItems.OfType<ShelterItem>().FirstOrDefault();
    public TreasureChestItem? TreasureChest => WorldItems.OfType<TreasureChestItem>().FirstOrDefault();
    public SharkItem? Shark => WorldItems.OfType<SharkItem>().FirstOrDefault();

    /// <summary>
    /// Get a stat by its ID and optionally cast to a specific type
    /// </summary>
    public T? GetStat<T>(string id) where T : WorldStat
    {
        return WorldStats.FirstOrDefault(s => s.Id == id) as T;
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

        void Visit(WorldStat stat)
        {
            if (visited.Contains(stat.Id))
                return;

            if (visiting.Contains(stat.Id))
                throw new InvalidOperationException($"Circular dependency detected in WorldStats involving {stat.Id}");

            visiting.Add(stat.Id);

            foreach (var depId in stat.GetDependencies())
            {
                if (statById.TryGetValue(depId, out var depStat))
                {
                    Visit(depStat);
                }
            }

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

    public void OnTimeAdvanced(double currentTime, double dt, IResourceAvailability? resourceAvailability = null)
    {
        CurrentTime = currentTime;

        // Tick all stats in dependency order
        var sortedStats = TopologicalSortStats();
        foreach (var stat in sortedStats)
        {
            stat.Tick(dt, this);
        }

        // Tick maintainable items
        foreach (var item in WorldItems.OfType<MaintainableWorldItem>())
        {
            item.Tick(dt, this);
        }

        // Remove expired maintainable items after tick cycle to avoid collection modification during iteration
        var expiredItems = WorldItems.OfType<MaintainableWorldItem>().Where(item => item.IsExpired).ToList();
        foreach (var item in expiredItems)
        {
            item.PerformExpiration(this, resourceAvailability);
            WorldItems.Remove(item);
        }
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
