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
        var blanket = new PalmFrondBlanketItem("test_blanket");
        var world = new IslandWorldState();
        
        blanket.Quality = 100.0;
        blanket.Tick(2000L, world);
        
        Assert.True(blanket.Quality < 100.0);
        Assert.True(blanket.Quality >= 0.0);
    }

    [Fact]
    public void MaintainableWorldItem_QualityNeverGoesBelowZero()
    {
        var blanket = new PalmFrondBlanketItem("test_blanket");
        var world = new IslandWorldState();
        
        blanket.Quality = 5.0;
        blanket.Tick(200000L, world);
        
        Assert.Equal(0.0, blanket.Quality);
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
        campfire.Tick(1200L, world);
        
        Assert.True(campfire.FuelSeconds < initialFuel);
    }

    [Fact]
    public void CampfireItem_GoesOutWhenFuelDepleted()
    {
        var campfire = new CampfireItem();
        var world = new IslandWorldState();
        
        campfire.FuelSeconds = 10.0;
        campfire.Tick(400L, world);
        
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
        
        campfireLit.Tick(2000L, world);
        campfireUnlit.Tick(2000L, world);
        
        Assert.True(campfireUnlit.Quality < campfireLit.Quality);
    }

    [Fact]
    public void CampfireItem_BoundaryCondition_ZeroFuel()
    {
        var campfire = new CampfireItem();
        var world = new IslandWorldState();
        
        campfire.FuelSeconds = 0.0;
        campfire.IsLit = true;
        campfire.Tick(20L, world);
        
        Assert.False(campfire.IsLit);
        Assert.Equal(0.0, campfire.FuelSeconds);
    }
}

public class PalmFrondBlanketItemTests
{
    [Fact]
    public void PalmFrondBlanketItem_StartsWithFullQuality()
    {
        var blanket = new PalmFrondBlanketItem();

        Assert.Equal(100.0, blanket.Quality);
    }

    [Fact]
    public void PalmFrondBlanketItem_DecaysInNormalWeather()
    {
        var blanket = new PalmFrondBlanketItem();
        var world = new IslandWorldState();

        var initialQuality = blanket.Quality;
        blanket.Tick(2000L, world);

        Assert.True(blanket.Quality < initialQuality);
    }

    [Fact]
    public void PalmFrondBlanketItem_QualityNeverGoesBelowZero_UnderHeavyDecay()
    {
        var blanket = new PalmFrondBlanketItem();
        var world = new IslandWorldState();

        blanket.Quality = 1.0;
        blanket.Tick(200000L, world);

        Assert.Equal(0.0, blanket.Quality);
    }
}

public class IslandWorldStateItemIntegrationTests
{
    [Fact]
    public void IslandWorldState_InitializesWithBaseItems()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();

        // Initial state: CalendarItem, WeatherItem, BeachItem, CoconutTreeItem, SupplyPile, StalactiteItem
        Assert.NotNull(world.SharedSupplyPile);
        Assert.NotNull(world.GetItem<CalendarItem>("calendar"));
        Assert.NotNull(world.GetItem<WeatherItem>("weather"));
        Assert.NotNull(world.GetItem<BeachItem>("beach"));
        Assert.Null(world.WorldItems.OfType<PalmFrondBlanketItem>().FirstOrDefault());
    }

    [Fact]
    public void IslandWorldState_TickAdvancesAllItems()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        world.WorldItems.Add(new CampfireItem("main_campfire"));

        var campfire = world.MainCampfire!;
        var initialCampfireFuel = campfire.FuelSeconds;

        world.OnTickAdvanced((long)(100.0 * 20));
    }

    [Fact]
    public void IslandWorldState_BlanketAccessor_ReturnsFirstBlanket()
    {
        var world = new IslandWorldState();
        world.WorldItems.Add(new PalmFrondBlanketItem("blanket1"));
        world.WorldItems.Add(new PalmFrondBlanketItem("blanket2"));

        var first = world.WorldItems.OfType<PalmFrondBlanketItem>().FirstOrDefault();
        Assert.Equal("blanket1", first?.Id);
    }

    [Fact]
    public void IslandWorldState_SerializationIncludesWorldItems()
    {
        var world = new IslandWorldState();
        world.WorldItems.Add(new CampfireItem("test_campfire"));
        world.WorldItems.Add(new PalmFrondBlanketItem("test_blanket"));

        var json = world.Serialize();

        Assert.Contains("WorldItems", json);
        Assert.Contains("test_campfire", json);
        Assert.Contains("test_blanket", json);
    }

    [Fact]
    public void IslandWorldState_DeserializationRestoresWorldItems()
    {
        var world1 = new IslandWorldState();
        world1.WorldItems.Add(new CampfireItem("test_campfire") { FuelSeconds = 500.0, Quality = 75.0 });
        world1.WorldItems.Add(new PalmFrondBlanketItem("test_blanket") { Quality = 60.0 });

        var json = world1.Serialize();

        var world2 = new IslandWorldState();
        world2.Deserialize(json);

        Assert.Equal(2, world2.WorldItems.Count);
        Assert.NotNull(world2.MainCampfire);
        var blanket = world2.WorldItems.OfType<PalmFrondBlanketItem>().FirstOrDefault();
        Assert.NotNull(blanket);
        Assert.Equal(500.0, world2.MainCampfire!.FuelSeconds);
        Assert.Equal(75.0, world2.MainCampfire.Quality);
        Assert.Equal(60.0, blanket!.Quality);
    }
}
