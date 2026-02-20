using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Tests;

public class WorldItemTests
{
    [Fact]
    public void WorldItem_Construction_SetsIdAndType()
    {
        var campfire = new CampfireItem("test_campfire");
        
        Assert.Equal("test_campfire", campfire.Id);
        Assert.Equal("campfire", campfire.Type);
    }

    [Fact]
    public void MaintainableWorldItem_DecaysQualityOverTime()
    {
        var shelter = new ShelterItem("test_shelter");
        var world = new IslandWorldState();
        
        shelter.Quality = 100.0;
        shelter.Tick(100.0, world);
        
        Assert.True(shelter.Quality < 100.0);
        Assert.True(shelter.Quality >= 0.0);
    }

    [Fact]
    public void MaintainableWorldItem_QualityNeverGoesBelowZero()
    {
        var shelter = new ShelterItem("test_shelter");
        var world = new IslandWorldState();
        
        shelter.Quality = 5.0;
        shelter.Tick(10000.0, world);
        
        Assert.Equal(0.0, shelter.Quality);
    }
}

public class CampfireItemTests
{
    [Fact]
    public void CampfireItem_StartsLitWithFuel()
    {
        var campfire = new CampfireItem();
        
        Assert.True(campfire.IsLit);
        Assert.True(campfire.FuelSeconds > 0.0);
    }

    [Fact]
    public void CampfireItem_FuelDecaysWhenLit()
    {
        var campfire = new CampfireItem();
        var world = new IslandWorldState();
        
        var initialFuel = campfire.FuelSeconds;
        campfire.Tick(60.0, world);
        
        Assert.True(campfire.FuelSeconds < initialFuel);
    }

    [Fact]
    public void CampfireItem_GoesOutWhenFuelDepleted()
    {
        var campfire = new CampfireItem();
        var world = new IslandWorldState();
        
        campfire.FuelSeconds = 10.0;
        campfire.Tick(20.0, world);
        
        Assert.False(campfire.IsLit);
        Assert.Equal(0.0, campfire.FuelSeconds);
    }

    [Fact]
    public void CampfireItem_DecaysFasterWhenUnlit()
    {
        var campfireLit = new CampfireItem();
        var campfireUnlit = new CampfireItem();
        var world = new IslandWorldState();
        
        campfireLit.IsLit = true;
        campfireLit.FuelSeconds = 1000.0;
        campfireUnlit.IsLit = false;
        
        campfireLit.Tick(100.0, world);
        campfireUnlit.Tick(100.0, world);
        
        Assert.True(campfireUnlit.Quality < campfireLit.Quality);
    }

    [Fact]
    public void CampfireItem_BoundaryCondition_ZeroFuel()
    {
        var campfire = new CampfireItem();
        var world = new IslandWorldState();
        
        campfire.FuelSeconds = 0.0;
        campfire.IsLit = true;
        campfire.Tick(1.0, world);
        
        Assert.False(campfire.IsLit);
        Assert.Equal(0.0, campfire.FuelSeconds);
    }
}

public class ShelterItemTests
{
    [Fact]
    public void ShelterItem_StartsWithFullQuality()
    {
        var shelter = new ShelterItem();
        
        Assert.Equal(100.0, shelter.Quality);
    }

    [Fact]
    public void ShelterItem_DecaysInNormalWeather()
    {
        var shelter = new ShelterItem();
        var world = new IslandWorldState();

        var initialQuality = shelter.Quality;
        shelter.Tick(100.0, world);

        Assert.True(shelter.Quality < initialQuality);
    }

    [Fact]
    public void ShelterItem_DecaysFasterInColdWeather()
    {
        var shelterWarm = new ShelterItem();
        var shelterCold = new ShelterItem();

        var worldWarm = new IslandWorldState();
        worldWarm.WorldItems.Add(new WeatherItem("weather") { Temperature = TemperatureBand.Hot });

        var worldCold = new IslandWorldState();
        worldCold.WorldItems.Add(new WeatherItem("weather") { Temperature = TemperatureBand.Cold });

        shelterWarm.Tick(100.0, worldWarm);
        shelterCold.Tick(100.0, worldCold);

        Assert.True(shelterCold.Quality < shelterWarm.Quality);
    }

    [Fact(Skip = "Windy weather no longer exists in the new weather system (Hot/Cold only)")]
    public void ShelterItem_DecaysFasterInWindyWeather()
    {
    }

    [Fact(Skip = "Windy weather no longer exists in the new weather system (Hot/Cold only)")]
    public void ShelterItem_RainyWeatherDecaysFasterThanWindy()
    {
    }
}

public class IslandWorldStateItemIntegrationTests
{
    [Fact]
    public void IslandWorldState_InitializesWithCampfireAndShelter()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();

        // New initial state: CalendarItem, WeatherItem, BeachItem, CoconutTreeItem, ShelterItem, SupplyPile
        Assert.NotNull(world.MainShelter);
        Assert.NotNull(world.SharedSupplyPile);
        Assert.NotNull(world.GetItem<CalendarItem>("calendar"));
        Assert.NotNull(world.GetItem<WeatherItem>("weather"));
        Assert.NotNull(world.GetItem<BeachItem>("beach"));
    }

    [Fact]
    public void IslandWorldState_TickAdvancesAllItems()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        world.WorldItems.Add(new CampfireItem("main_campfire"));

        var campfire = world.MainCampfire!;
        var shelter = world.MainShelter!;
        
        var initialCampfireFuel = campfire.FuelSeconds;
        var initialShelterQuality = shelter.Quality;
        
        world.OnTimeAdvanced(100.0, 100.0);
        
        Assert.True(campfire.FuelSeconds < initialCampfireFuel);
        Assert.True(shelter.Quality < initialShelterQuality);
    }

    [Fact]
    public void IslandWorldState_MainCampfireAccessor_ReturnsFirstCampfire()
    {
        var world = new IslandWorldState();
        world.WorldItems.Add(new CampfireItem("campfire1"));
        world.WorldItems.Add(new CampfireItem("campfire2"));
        
        Assert.Equal("campfire1", world.MainCampfire?.Id);
    }

    [Fact]
    public void IslandWorldState_MainShelterAccessor_ReturnsFirstShelter()
    {
        var world = new IslandWorldState();
        world.WorldItems.Add(new ShelterItem("shelter1"));
        world.WorldItems.Add(new ShelterItem("shelter2"));
        
        Assert.Equal("shelter1", world.MainShelter?.Id);
    }

    [Fact]
    public void IslandWorldState_SerializationIncludesWorldItems()
    {
        var world = new IslandWorldState();
        world.WorldItems.Add(new CampfireItem("test_campfire"));
        world.WorldItems.Add(new ShelterItem("test_shelter"));
        
        var json = world.Serialize();
        
        Assert.Contains("WorldItems", json);
        Assert.Contains("test_campfire", json);
        Assert.Contains("test_shelter", json);
    }

    [Fact]
    public void IslandWorldState_DeserializationRestoresWorldItems()
    {
        var world1 = new IslandWorldState();
        world1.WorldItems.Add(new CampfireItem("test_campfire") { FuelSeconds = 500.0, Quality = 75.0 });
        world1.WorldItems.Add(new ShelterItem("test_shelter") { Quality = 60.0 });
        
        var json = world1.Serialize();
        
        var world2 = new IslandWorldState();
        world2.Deserialize(json);
        
        Assert.Equal(2, world2.WorldItems.Count);
        Assert.NotNull(world2.MainCampfire);
        Assert.NotNull(world2.MainShelter);
        Assert.Equal(500.0, world2.MainCampfire.FuelSeconds);
        Assert.Equal(75.0, world2.MainCampfire.Quality);
        Assert.Equal(60.0, world2.MainShelter.Quality);
    }
}
