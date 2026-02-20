using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Supply;

namespace JohnnyLike.Domain.Island.Items;

public enum TideLevel { Low, High }

public class BeachItem : WorldItem, ITickableWorldItem, IIslandActionCandidate
{
    public Dictionary<string, double> Bounty { get; set; } = new()
    {
        ["sticks"] = 10,
        ["driftwood"] = 10,
        ["rocks"] = 5
    };

    public TideLevel Tide { get; private set; }

    public BeachItem(string id = "beach") : base(id, "beach") { }

    public IEnumerable<string> GetDependencies() => new[] { "calendar" };

    public List<TraceEvent> Tick(double dtSeconds, IslandWorldState world, double currentTime)
    {
        var calendar = world.GetItem<CalendarItem>("calendar");
        var weather = world.GetItem<WeatherItem>("weather");

        if (calendar == null)
            return new List<TraceEvent>();

        var tidePhase = calendar.HourOfDay % 12;
        Tide = tidePhase >= 6 ? TideLevel.High : TideLevel.Low;

        double regenRate = 0.1;

        if (Tide == TideLevel.High)
            regenRate *= 2;

        if (weather?.Temperature == TemperatureBand.Cold)
            regenRate *= 1.2;

        Bounty["sticks"] += regenRate * dtSeconds;
        Bounty["driftwood"] += regenRate * dtSeconds;
        Bounty["rocks"] += regenRate * dtSeconds * 0.5;

        return new List<TraceEvent>();
    }

    public void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        if (Bounty.Values.Sum() < 1.0)
            return;

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("explore_beach"),
                ActionKind.Interact,
                new LocationActionParameters("beach"),
                20.0 + ctx.Random.NextDouble() * 10.0
            ),
            0.5,
            $"Explore beach (bounty: {Bounty.Values.Sum():F1})",
            EffectHandler: new Action<EffectContext>(effectCtx =>
            {
                var pile = effectCtx.World.SharedSupplyPile;
                if (pile == null) return;

                var sticksGathered = effectCtx.Rng.Next(1, 5);
                var woodGathered = effectCtx.Rng.Next(1, 5);
                var rocksGathered = effectCtx.Rng.Next(0, 3);

                pile.AddSupply("sticks", sticksGathered, id => new StickSupply(id));
                pile.AddSupply("wood", woodGathered, id => new WoodSupply(id));
                pile.AddSupply("rocks", rocksGathered, id => new RocksSupply(id));

                Bounty["sticks"] = Math.Max(0, Bounty["sticks"] - sticksGathered);
                Bounty["driftwood"] = Math.Max(0, Bounty["driftwood"] - woodGathered);
                Bounty["rocks"] = Math.Max(0, Bounty["rocks"] - rocksGathered);

                effectCtx.Actor.Morale += 5.0;
                effectCtx.Actor.Energy -= 10.0;
            }),
            Qualities: new Dictionary<QualityType, double>
            {
                [QualityType.Preparation] = 0.6,
                [QualityType.ResourcePreservation] = 0.4
            }
        ));
    }

    public override Dictionary<string, object> SerializeToDict()
    {
        var dict = base.SerializeToDict();
        dict["Bounty"] = new Dictionary<string, object>(Bounty.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value));
        dict["Tide"] = Tide.ToString();
        return dict;
    }

    public override void DeserializeFromDict(Dictionary<string, System.Text.Json.JsonElement> data)
    {
        base.DeserializeFromDict(data);
        if (data.TryGetValue("Tide", out var tideEl))
            Tide = Enum.Parse<TideLevel>(tideEl.GetString()!);
        if (data.TryGetValue("Bounty", out var bountyEl))
        {
            var bountyDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, double>>(bountyEl.GetRawText());
            if (bountyDict != null) Bounty = bountyDict;
        }
    }
}
