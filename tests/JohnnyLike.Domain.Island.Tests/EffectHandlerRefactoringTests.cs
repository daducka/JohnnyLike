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
        campfire.FuelSeconds = 500.0; // Low fuel to trigger add fuel candidate
        
        // Add wood to the shared pile
        world.SharedSupplyPile!.AddSupply("wood", 20.0, id => new WoodSupply(id));
        
        var candidates = domain.GenerateCandidates(actorId, actor, world, 0.0, new Random(42), new EmptyResourceAvailability());
        
        var addFuelCandidate = candidates.FirstOrDefault(c => c.Action.Id.Value == "add_fuel_campfire");
        
        Assert.NotNull(addFuelCandidate);
        
        // Verify the effect handler is in ResultData
        Assert.True(addFuelCandidate.Action.ResultData?.ContainsKey("__effect_handler__") ?? false,
            "Candidate should contain __effect_handler__ in ResultData");
        
        var effectHandler = addFuelCandidate.Action.ResultData!["__effect_handler__"];
        Assert.IsType<Action<EffectContext>>(effectHandler);
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
        
        // Simulate successful action completion
        var outcome = new ActionOutcome(
            repairCandidate.Action.Id,
            ActionOutcomeType.Success,
            25.0,
            new Dictionary<string, object>(repairCandidate.Action.ResultData!)
        );
        
        var rng = new RandomRngStream(new Random(42));
        domain.ApplyActionEffects(actorId, outcome, actor, world, rng, new EmptyResourceAvailability());
        
        // Verify effect was applied
        Assert.True(campfire.Quality > 50.0, "Quality should increase after repair");
    }
}
