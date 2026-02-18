using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Island.Supply;

namespace JohnnyLike.Domain.Island.Tests;

public class EffectHandlerRefactoringTests
{
    [Fact]
    public void CampfireCandidate_ContainsEffectHandler()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        var campfire = world.MainCampfire!;
        // Set fuel to 500s which is below the 1800s threshold that triggers add fuel candidate
        campfire.FuelSeconds = 500.0;
        
        // Add wood to the shared pile (minimum 3.0 required for add fuel action)
        world.SharedSupplyPile!.AddSupply("wood", 20.0, id => new WoodSupply(id));
        
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0.0, new Random(42), new EmptyResourceAvailability());
        
        var addFuelCandidate = candidates.FirstOrDefault(c => c.Action.Id.Value == "add_fuel_campfire");
        
        Assert.NotNull(addFuelCandidate);
        
        // Verify the effect handler is in ActionCandidate.EffectHandler property
        Assert.NotNull(addFuelCandidate.EffectHandler);
        Assert.IsType<Action<EffectContext>>(addFuelCandidate.EffectHandler);
    }
    
    [Fact]
    public void EffectHandler_ExecutesCorrectly()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        var campfire = world.MainCampfire!;
        var initialQuality = campfire.Quality;
        campfire.Quality = 50.0;
        
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0.0, new Random(42), new EmptyResourceAvailability());
        
        var repairCandidate = candidates.FirstOrDefault(c => c.Action.Id.Value == "repair_campfire");
        
        Assert.NotNull(repairCandidate);
        
        // Simulate successful action completion, passing the effect handler
        var outcome = new ActionOutcome(
            repairCandidate.Action.Id,
            ActionOutcomeType.Success,
            25.0,
            new Dictionary<string, object>(repairCandidate.Action.ResultData!)
        );
        
        var rng = new RandomRngStream(new Random(42));
        domain.ApplyActionEffects(actorId, outcome, actor, world, rng, new EmptyResourceAvailability(), repairCandidate.EffectHandler);
        
        // Verify effect was applied
        Assert.True(campfire.Quality > 50.0, "Quality should increase after repair");
    }
}
