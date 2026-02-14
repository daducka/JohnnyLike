using JohnnyLike.Domain.Abstractions;
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

    private double _lastTimeAdvanced = 0.0;

    public void OnTimeAdvanced(double currentTime, double dt)
    {
        _lastTimeAdvanced = currentTime;

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
    }

    public override string Serialize()
    {
        return JsonSerializer.Serialize(new
        {
            TimeOfDay,
            DayCount,
            Weather,
            FishAvailable,
            FishRegenRatePerMinute,
            CoconutsAvailable,
            TideLevel
        });
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
    }
}
