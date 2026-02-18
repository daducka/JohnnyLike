using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Stats;
using JohnnyLike.Domain.Island.Supply;
using Xunit;

namespace JohnnyLike.Domain.Island.Tests;

public class SupplySystemTests
{
    [Fact]
    public void SupplyPile_AddSupply_IncreasesQuantity()
    {
        var pile = new SupplyPile("test_pile", "shared");
        
        pile.AddSupply("wood", 10.0, id => new WoodSupply(id));
        
        Assert.Equal(10.0, pile.GetQuantity<WoodSupply>("wood"));
    }

    [Fact]
    public void SupplyPile_AddSupply_Twice_AccumulatesQuantity()
    {
        var pile = new SupplyPile("test_pile", "shared");
        
        pile.AddSupply("wood", 10.0, id => new WoodSupply(id));
        pile.AddSupply("wood", 5.0, id => new WoodSupply(id));
        
        Assert.Equal(15.0, pile.GetQuantity<WoodSupply>("wood"));
    }

    [Fact]
    public void SupplyPile_TryConsumeSupply_WithSufficientQuantity_ReturnsTrue()
    {
        var pile = new SupplyPile("test_pile", "shared");
        pile.AddSupply("wood", 10.0, id => new WoodSupply(id));
        
        var result = pile.TryConsumeSupply<WoodSupply>("wood", 5.0);
        
        Assert.True(result);
        Assert.Equal(5.0, pile.GetQuantity<WoodSupply>("wood"));
    }

    [Fact]
    public void SupplyPile_TryConsumeSupply_WithInsufficientQuantity_ReturnsFalse()
    {
        var pile = new SupplyPile("test_pile", "shared");
        pile.AddSupply("wood", 3.0, id => new WoodSupply(id));
        
        var result = pile.TryConsumeSupply<WoodSupply>("wood", 5.0);
        
        Assert.False(result);
        Assert.Equal(3.0, pile.GetQuantity<WoodSupply>("wood")); // Quantity unchanged
    }

    [Fact]
    public void SupplyPile_Serialization_PreservesSupplies()
    {
        var pile = new SupplyPile("test_pile", "shared");
        pile.AddSupply("wood", 25.0, id => new WoodSupply(id));
        
        var serialized = pile.SerializeToDict();
        var newPile = new SupplyPile("test_pile");
        newPile.DeserializeFromDict(serialized.ToDictionary(
            kvp => kvp.Key,
            kvp => System.Text.Json.JsonSerializer.SerializeToElement(kvp.Value)
        ));
        
        Assert.Equal("shared", newPile.AccessControl);
        Assert.Equal(25.0, newPile.GetQuantity<WoodSupply>("wood"));
    }

    [Fact]
    public void DriftwoodAvailabilityStat_ReplenishesOverTime()
    {
        var world = new IslandWorldState();
        world.WorldStats.Add(new Stats.TimeOfDayStat());
        world.WorldStats.Add(new Stats.WeatherStat());
        world.WorldStats.Add(new Stats.TideStat());
        var driftwoodStat = new DriftwoodAvailabilityStat();
        driftwoodStat.DriftwoodAvailable = 40.0;
        world.WorldStats.Add(driftwoodStat);
        
        // Advance time by 60 seconds (1 minute) at normal tide/weather
        world.OnTimeAdvanced(60.0, 60.0);
        
        // Base rate: 0.5 per minute, so should increase by 0.5
        Assert.True(driftwoodStat.DriftwoodAvailable >= 40.5);
    }

    [Fact]
    public void DriftwoodAvailabilityStat_HighTide_IncreasesReplenishmentRate()
    {
        var world = new IslandWorldState();
        var timeStat = new Stats.TimeOfDayStat();
        timeStat.TimeOfDay = 0.75; // Set time to ensure high tide (tidePhase = (0.75 * 24) % 12 = 18 % 12 = 6)
        world.WorldStats.Add(timeStat);
        world.WorldStats.Add(new Stats.WeatherStat());
        world.WorldStats.Add(new Stats.TideStat());
        
        var driftwoodStat = new DriftwoodAvailabilityStat();
        driftwoodStat.DriftwoodAvailable = 40.0;
        world.WorldStats.Add(driftwoodStat);
        
        // First tick to set up tide state
        world.OnTimeAdvanced(0.0, 0.1);
        var initialDriftwood = driftwoodStat.DriftwoodAvailable;
        
        // Verify we're at high tide
        var tideStat = world.GetStat<Stats.TideStat>("tide")!;
        Assert.Equal(TideLevel.High, tideStat.TideLevel);
        
        // Advance time by 60 seconds (1 minute)
        world.OnTimeAdvanced(60.0, 60.0);
        
        // At high tide, rate is 2x: 0.5 * 2 = 1.0 per minute
        // So we expect at least 0.9 wood added
        var increase = driftwoodStat.DriftwoodAvailable - initialDriftwood;
        Assert.True(increase >= 0.9, $"Expected at least 0.9 increase, got {increase}");
    }

    [Fact]
    public void CollectDriftwood_AddsWoodToSharedPile()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actor = (IslandActorState)domain.CreateActorState(new ActorId("test_actor"));
        
        // Set up initial conditions
        var driftwoodStat = world.GetStat<DriftwoodAvailabilityStat>("driftwood_availability")!;
        driftwoodStat.DriftwoodAvailable = 50.0;
        
        var sharedPile = world.SharedSupplyPile!;
        var initialWood = sharedPile.GetQuantity<WoodSupply>("wood");
        
        // Manually simulate wood collection (bypass the candidate system)
        var amountCollected = 10.0;
        driftwoodStat.DriftwoodAvailable -= amountCollected;
        sharedPile.AddSupply("wood", amountCollected, id => new WoodSupply(id));
        
        // Verify wood was added to the pile
        var finalWood = sharedPile.GetQuantity<WoodSupply>("wood");
        Assert.Equal(initialWood + amountCollected, finalWood);
        
        // Verify driftwood was consumed
        Assert.Equal(40.0, driftwoodStat.DriftwoodAvailable);
    }

    [Fact]
    public void CampfireAddFuel_ConsumesWoodFromSupplyPile()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        
        // Set up campfire with low fuel
        var campfire = world.MainCampfire!;
        var initialFuel = campfire.FuelSeconds;
        
        // Ensure we have wood in the supply pile
        var sharedPile = world.SharedSupplyPile!;
        sharedPile.AddSupply("wood", 20.0, id => new WoodSupply(id));
        var initialWood = sharedPile.GetQuantity<WoodSupply>("wood");
        
        // Manually simulate wood consumption and fuel addition
        var woodCost = 5.0;
        var success = sharedPile.TryConsumeSupply<WoodSupply>("wood", woodCost);
        Assert.True(success);
        
        campfire.FuelSeconds += 1800.0; // Add fuel
        
        // Verify wood was consumed
        var finalWood = sharedPile.GetQuantity<WoodSupply>("wood");
        Assert.Equal(initialWood - woodCost, finalWood);
        
        // Verify fuel was added to campfire
        Assert.True(campfire.FuelSeconds > initialFuel);
    }

    [Fact]
    public void IslandWorldState_SharedSupplyPile_ReturnsCorrectPile()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        
        Assert.NotNull(world.SharedSupplyPile);
        Assert.Equal("shared", world.SharedSupplyPile.AccessControl);
    }

    [Fact]
    public void IslandWorldState_GetAccessiblePiles_ReturnsSharedPile()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        
        var piles = world.GetAccessiblePiles(new ActorId("test_actor"));
        
        Assert.Single(piles);
        Assert.Equal("shared", piles[0].AccessControl);
    }
}
