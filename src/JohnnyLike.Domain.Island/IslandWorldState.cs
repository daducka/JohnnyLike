using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Items;
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
    public double TimeOfDay { get; set; } = 0.5;
    public int DayCount { get; set; } = 0;
    public Weather Weather { get; set; } = Weather.Clear;
    public double FishAvailable { get; set; } = 100.0;
    public double FishRegenRatePerMinute { get; set; } = 5.0;
    public int CoconutsAvailable { get; set; } = 5;
    public TideLevel TideLevel { get; set; } = TideLevel.Low;

    public double CurrentTime { get; set; } = 0.0;

    public List<WorldItem> WorldItems { get; set; } = new();

    public CampfireItem? MainCampfire => WorldItems.OfType<CampfireItem>().FirstOrDefault();
    public ShelterItem? MainShelter => WorldItems.OfType<ShelterItem>().FirstOrDefault();
    public TreasureChestItem? TreasureChest => WorldItems.OfType<TreasureChestItem>().FirstOrDefault();
    public SharkItem? Shark => WorldItems.OfType<SharkItem>().FirstOrDefault();

    public void OnTimeAdvanced(double currentTime, double dt)
    {
        CurrentTime = currentTime;

        TimeOfDay += dt / 86400.0;
        if (TimeOfDay >= 1.0)
        {
            TimeOfDay -= 1.0;
            DayCount++;
            CoconutsAvailable = Math.Min(10, CoconutsAvailable + 3);
        }

        FishAvailable = Math.Min(100.0, FishAvailable + FishRegenRatePerMinute * (dt / 60.0));

        var tidePhase = (TimeOfDay * 24.0) % 12.0;
        TideLevel = tidePhase >= 6.0 ? TideLevel.High : TideLevel.Low;

        // Tick maintainable items
        foreach (var item in WorldItems.OfType<MaintainableWorldItem>())
        {
            item.Tick(dt, this);
        }

        // Tick shark for auto-despawn
        Shark?.Tick(currentTime, this);
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
            TimeOfDay,
            DayCount,
            Weather,
            FishAvailable,
            FishRegenRatePerMinute,
            CoconutsAvailable,
            TideLevel,
            WorldItems = serializedItems
        }, options);
    }

    public override void Deserialize(string json)
    {
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        if (data == null) return;

        TimeOfDay = data["TimeOfDay"].GetDouble();
        DayCount = data["DayCount"].GetInt32();
        Weather = Enum.Parse<Weather>(data["Weather"].GetString()!);
        FishAvailable = data["FishAvailable"].GetDouble();
        FishRegenRatePerMinute = data["FishRegenRatePerMinute"].GetDouble();
        CoconutsAvailable = data["CoconutsAvailable"].GetInt32();
        TideLevel = Enum.Parse<TideLevel>(data["TideLevel"].GetString()!);

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
    }
}
