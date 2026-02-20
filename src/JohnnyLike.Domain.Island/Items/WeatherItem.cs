using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Domain.Island.Items;

public enum TemperatureBand
{
    Cold,
    Hot
}

public class WeatherItem : WorldItem, ITickableWorldItem
{
    public TemperatureBand Temperature { get; set; } = TemperatureBand.Hot;

    public WeatherItem(string id = "weather") : base(id, "weather") { }

    public IEnumerable<string> GetDependencies() => new[] { "calendar" };

    public List<TraceEvent> Tick(double dtSeconds, IslandWorldState world, double currentTime)
    {
        var calendar = world.GetItem<CalendarItem>("calendar");

        if (calendar == null)
            return new List<TraceEvent>();

        var hour = calendar.HourOfDay;

        Temperature = (hour < 8.0 || hour >= 19.0)
            ? TemperatureBand.Cold
            : TemperatureBand.Hot;

        return new List<TraceEvent>();
    }

    public override Dictionary<string, object> SerializeToDict()
    {
        var dict = base.SerializeToDict();
        dict["Temperature"] = Temperature.ToString();
        return dict;
    }

    public override void DeserializeFromDict(Dictionary<string, System.Text.Json.JsonElement> data)
    {
        base.DeserializeFromDict(data);
        if (data.TryGetValue("Temperature", out var temp))
            Temperature = Enum.Parse<TemperatureBand>(temp.GetString()!);
    }
}
