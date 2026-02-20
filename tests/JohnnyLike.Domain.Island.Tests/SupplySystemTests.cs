using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.Domain.Island.Items;
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

    [Fact(Skip = "DriftwoodAvailabilityStat has been removed; driftwood is now tracked via BeachItem.Bounty")]
    public void DriftwoodAvailabilityStat_ReplenishesOverTime()
    {
    }

    [Fact(Skip = "DriftwoodAvailabilityStat has been removed; driftwood is now tracked via BeachItem.Bounty")]
    public void DriftwoodAvailabilityStat_HighTide_IncreasesReplenishmentRate()
    {
    }

    [Fact(Skip = "DriftwoodPileItem has been removed; wood collection now happens via BeachItem.AddCandidates")]
    public void CollectDriftwood_AddsWoodToSharedPile()
    {
    }

    [Fact]
    public void CampfireAddFuel_ConsumesWoodFromSupplyPile()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        world.WorldItems.Add(new CampfireItem("main_campfire"));

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
