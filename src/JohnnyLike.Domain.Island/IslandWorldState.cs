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

public class TreasureChestState
{
    public bool IsPresent { get; set; } = false;
    public bool IsOpened { get; set; } = false;
    public double Health { get; set; } = 0.0;
    public string? Position { get; set; } = null;
}

public class SharkState
{
    public bool IsPresent { get; set; } = false;
    public double ExpiresAt { get; set; } = 0.0;
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

    public TreasureChestState TreasureChest { get; set; } = new();
    public SharkState Shark { get; set; } = new();

    public CampfireItem? MainCampfire => WorldItems.OfType<CampfireItem>().FirstOrDefault();
    public ShelterItem? MainShelter => WorldItems.OfType<ShelterItem>().FirstOrDefault();

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

        // Auto-despawn shark when expired
        if (Shark.IsPresent && CurrentTime >= Shark.ExpiresAt)
        {
            Shark.IsPresent = false;
        }

        foreach (var item in WorldItems.OfType<MaintainableWorldItem>())
        {
            item.Tick(dt, this);
        }
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
            WorldItems = serializedItems,
            TreasureChest = new
            {
                TreasureChest.IsPresent,
                TreasureChest.IsOpened,
                TreasureChest.Health,
                TreasureChest.Position
            },
            Shark = new
            {
                Shark.IsPresent,
                Shark.ExpiresAt
            }
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

        if (data.TryGetValue("TreasureChest", out var treasureElement))
        {
            var treasureData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(treasureElement.GetRawText());
            if (treasureData != null)
            {
                TreasureChest.IsPresent = treasureData["IsPresent"].GetBoolean();
                TreasureChest.IsOpened = treasureData["IsOpened"].GetBoolean();
                TreasureChest.Health = treasureData["Health"].GetDouble();
                TreasureChest.Position = treasureData.TryGetValue("Position", out var posEl) && posEl.ValueKind != JsonValueKind.Null 
                    ? posEl.GetString() 
                    : null;
            }
        }

        if (data.TryGetValue("Shark", out var sharkElement))
        {
            var sharkData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(sharkElement.GetRawText());
            if (sharkData != null)
            {
                Shark.IsPresent = sharkData["IsPresent"].GetBoolean();
                Shark.ExpiresAt = sharkData["ExpiresAt"].GetDouble();
            }
        }
    }
}
