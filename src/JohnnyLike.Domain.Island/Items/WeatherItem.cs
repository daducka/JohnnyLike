using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Telemetry;

namespace JohnnyLike.Domain.Island.Items;

public enum TemperatureBand { Cold, Hot }
public enum PrecipitationBand { Clear, Rainy }

public class WeatherItem : WorldItem, ITickableWorldItem
{
    public TemperatureBand Temperature { get; set; } = TemperatureBand.Hot;
    public PrecipitationBand Precipitation { get; set; } = PrecipitationBand.Clear;

    public WeatherItem(string id = "weather") : base(id, "weather") { }

    public IEnumerable<string> GetDependencies() => new[] { "calendar" };

    public List<TraceEvent> Tick(long currentTick, WorldState worldState)
    {
        var world = (IslandWorldState)worldState;
        var calendar = world.GetItem<CalendarItem>("calendar");
        if (calendar == null) return new List<TraceEvent>();

        var hour = calendar.HourOfDay;
        var prevTemp = Temperature;
        Temperature = (hour < 8.0 || hour >= 19.0) ? TemperatureBand.Cold : TemperatureBand.Hot;

        if (Temperature != prevTemp)
        {
            var text = Temperature == TemperatureBand.Hot
                ? "The temperature rises; it is getting warm."
                : "The temperature drops; it is getting cold.";
            using (world.Tracer.PushPhase(TracePhase.WorldTick))
                world.Tracer.BeatWorld(text, subjectId: "weather:temperature", priority: 15);
        }

        return new List<TraceEvent>();
    }

    public override Dictionary<string, object> SerializeToDict()
    {
        var dict = base.SerializeToDict();
        dict["Temperature"] = Temperature.ToString();
        dict["Precipitation"] = Precipitation.ToString();
        return dict;
    }

    public override void DeserializeFromDict(Dictionary<string, System.Text.Json.JsonElement> data)
    {
        base.DeserializeFromDict(data);
        if (data.TryGetValue("Temperature", out var temp))
            Temperature = Enum.Parse<TemperatureBand>(temp.GetString()!);
        if (data.TryGetValue("Precipitation", out var precip))
            Precipitation = Enum.Parse<PrecipitationBand>(precip.GetString()!);
    }
}
