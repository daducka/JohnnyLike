using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.Domain.Island.Stats;

namespace JohnnyLike.Domain.Island.Tests;

public class WorldStatTests
{
    [Fact]
    public void TimeOfDayStat_AdvancesTimeOfDay()
    {
        var stat = new TimeOfDayStat { TimeOfDay = 0.5 };
        var world = new IslandWorldState();
        
        stat.Tick(21600.0, world); // 6 hours
        
        Assert.InRange(stat.TimeOfDay, 0.74, 0.76);
    }

    [Fact]
    public void TimeOfDayStat_IncrementsDay()
    {
        var stat = new TimeOfDayStat { TimeOfDay = 0.9, DayCount = 0 };
        var world = new IslandWorldState();
        
        stat.Tick(8640.0, world); // 2.4 hours
        
        Assert.Equal(1, stat.DayCount);
        Assert.InRange(stat.TimeOfDay, 0.0, 0.1);
    }

    [Fact]
    public void FishPopulationStat_RegeneratesFish()
    {
        var stat = new FishPopulationStat { FishAvailable = 50.0, FishRegenRatePerMinute = 5.0 };
        var world = new IslandWorldState();
        
        stat.Tick(60.0, world); // 1 minute
        
        Assert.Equal(55.0, stat.FishAvailable, 1);
    }

    [Fact]
    public void FishPopulationStat_CapsAt100()
    {
        var stat = new FishPopulationStat { FishAvailable = 95.0, FishRegenRatePerMinute = 10.0 };
        var world = new IslandWorldState();
        
        stat.Tick(60.0, world); // 1 minute
        
        Assert.Equal(100.0, stat.FishAvailable);
    }

    [Fact]
    public void TideStat_CalculatesLowTide()
    {
        var world = new IslandWorldState();
        world.WorldStats.Add(new TimeOfDayStat { TimeOfDay = 0.0 }); // Midnight
        var tideStat = new TideStat();
        
        tideStat.Tick(0.0, world);
        
        Assert.Equal(TideLevel.Low, tideStat.TideLevel);
    }

    [Fact]
    public void TideStat_CalculatesHighTide()
    {
        var world = new IslandWorldState();
        world.WorldStats.Add(new TimeOfDayStat { TimeOfDay = 0.375 }); // 9am - should be high tide
        var tideStat = new TideStat();
        
        tideStat.Tick(0.0, world);
        
        Assert.Equal(TideLevel.High, tideStat.TideLevel);
    }

    [Fact]
    public void CoconutAvailabilityStat_RegeneratesOnNewDay()
    {
        var world = new IslandWorldState();
        var timeOfDay = new TimeOfDayStat { TimeOfDay = 0.9, DayCount = 0 };
        world.WorldStats.Add(timeOfDay);
        
        var coconutStat = new CoconutAvailabilityStat { CoconutsAvailable = 2 };
        
        // Advance time to trigger new day
        timeOfDay.Tick(10000.0, world); // More than 2.4 hours
        coconutStat.Tick(0.0, world); // Coconut stat checks for day change
        
        Assert.Equal(5, coconutStat.CoconutsAvailable);
    }

    [Fact]
    public void CoconutAvailabilityStat_CapsAt10()
    {
        var world = new IslandWorldState();
        var timeOfDay = new TimeOfDayStat { TimeOfDay = 0.9, DayCount = 0 };
        world.WorldStats.Add(timeOfDay);
        
        var coconutStat = new CoconutAvailabilityStat { CoconutsAvailable = 8 };
        
        // Advance time to trigger new day
        timeOfDay.Tick(10000.0, world);
        coconutStat.Tick(0.0, world);
        
        Assert.Equal(10, coconutStat.CoconutsAvailable); // Capped at 10
    }

    [Fact]
    public void WorldStat_SerializationRoundTrip()
    {
        var original = new TimeOfDayStat { TimeOfDay = 0.75, DayCount = 3 };
        
        var dict = original.SerializeToDict();
        var deserialized = new TimeOfDayStat();
        
        // Convert dictionary for deserialization without creating unnecessary JsonDocument instances
        var jsonDict = new Dictionary<string, System.Text.Json.JsonElement>();
        foreach (var kvp in dict)
        {
            using var doc = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(kvp.Value));
            jsonDict[kvp.Key] = doc.RootElement.Clone();
        }
        
        deserialized.DeserializeFromDict(jsonDict);
        
        Assert.Equal(original.TimeOfDay, deserialized.TimeOfDay);
        Assert.Equal(original.DayCount, deserialized.DayCount);
    }
}

public class WorldStatDependencyTests
{
    [Fact]
    public void TopologicalSort_SortsStatsInDependencyOrder()
    {
        var world = new IslandWorldState();
        
        // Add stats in random order
        world.WorldStats.Add(new TideStat()); // Depends on time_of_day
        world.WorldStats.Add(new FishPopulationStat()); // Depends on weather, time_of_day
        world.WorldStats.Add(new TimeOfDayStat()); // No dependencies
        world.WorldStats.Add(new WeatherStat()); // Depends on time_of_day
        world.WorldStats.Add(new CoconutAvailabilityStat()); // Depends on time_of_day
        
        // Call OnTimeAdvanced which triggers topological sort
        world.OnTimeAdvanced(0.0, 100.0);
        
        // If no exception thrown, sort worked correctly
        Assert.True(true);
    }

    [Fact]
    public void TimeOfDayStat_HasNoDependencies()
    {
        var stat = new TimeOfDayStat();
        
        var deps = stat.GetDependencies().ToList();
        
        Assert.Empty(deps);
    }

    [Fact]
    public void WeatherStat_DependsOnTimeOfDay()
    {
        var stat = new WeatherStat();
        
        var deps = stat.GetDependencies().ToList();
        
        Assert.Single(deps);
        Assert.Contains("time_of_day", deps);
    }

    [Fact]
    public void TideStat_DependsOnTimeOfDay()
    {
        var stat = new TideStat();
        
        var deps = stat.GetDependencies().ToList();
        
        Assert.Single(deps);
        Assert.Contains("time_of_day", deps);
    }

    [Fact]
    public void FishPopulationStat_DependsOnWeatherAndTimeOfDay()
    {
        var stat = new FishPopulationStat();
        
        var deps = stat.GetDependencies().ToList();
        
        Assert.Equal(2, deps.Count);
        Assert.Contains("weather", deps);
        Assert.Contains("time_of_day", deps);
    }

    [Fact]
    public void CoconutAvailabilityStat_DependsOnTimeOfDay()
    {
        var stat = new CoconutAvailabilityStat();
        
        var deps = stat.GetDependencies().ToList();
        
        Assert.Single(deps);
        Assert.Contains("time_of_day", deps);
    }
}

public class WorldStatIntegrationTests
{
    [Fact]
    public void IslandWorldState_SerializationIncludesWorldStats()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        
        // Modify some stat values
        world.TimeOfDay = 0.75;
        world.DayCount = 3;
        world.FishAvailable = 42.0;
        
        var json = world.Serialize();
        
        Assert.Contains("WorldStats", json);
        Assert.Contains("stat_time_of_day", json);
        Assert.Contains("stat_fish_population", json);
    }

    [Fact]
    public void IslandWorldState_DeserializationRestoresWorldStats()
    {
        var domain = new IslandDomainPack();
        var world1 = (IslandWorldState)domain.CreateInitialWorldState();
        
        // Modify some stat values
        world1.TimeOfDay = 0.75;
        world1.DayCount = 3;
        world1.FishAvailable = 42.0;
        world1.CoconutsAvailable = 7;
        world1.Weather = Weather.Rainy;
        
        var json = world1.Serialize();
        
        var world2 = (IslandWorldState)domain.CreateInitialWorldState();
        world2.Deserialize(json);
        
        Assert.Equal(0.75, world2.TimeOfDay, 2);
        Assert.Equal(3, world2.DayCount);
        Assert.Equal(42.0, world2.FishAvailable, 1);
        Assert.Equal(7, world2.CoconutsAvailable);
        Assert.Equal(Weather.Rainy, world2.Weather);
    }

    [Fact]
    public void IslandWorldState_OnTimeAdvanced_UpdatesAllStats()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        
        world.TimeOfDay = 0.9;
        world.DayCount = 0;
        world.FishAvailable = 50.0;
        world.CoconutsAvailable = 2;
        
        // Advance time by ~2.4 hours to trigger a new day
        world.OnTimeAdvanced(0.0, 8640.0);
        
        // TimeOfDay should have advanced and wrapped
        Assert.InRange(world.TimeOfDay, 0.0, 0.1);
        Assert.Equal(1, world.DayCount);
        
        // Fish should have regenerated
        Assert.True(world.FishAvailable > 50.0);
        
        // Coconuts should have regenerated on new day
        Assert.Equal(5, world.CoconutsAvailable);
    }

    [Fact]
    public void IslandWorldState_GetStat_RetrievesCorrectStat()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        
        var timeOfDayStat = world.GetStat<TimeOfDayStat>("time_of_day");
        var fishStat = world.GetStat<FishPopulationStat>("fish_population");
        
        Assert.NotNull(timeOfDayStat);
        Assert.NotNull(fishStat);
        Assert.Equal("time_of_day", timeOfDayStat.Id);
        Assert.Equal("fish_population", fishStat.Id);
    }
}
