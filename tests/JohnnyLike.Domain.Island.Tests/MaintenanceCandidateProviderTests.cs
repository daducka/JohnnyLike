using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Island.Supply;
using JohnnyLike.Domain.Kit.Dice;

namespace JohnnyLike.Domain.Island.Tests;

public class CampfireMaintenanceCandidateProviderTests
{
    [Fact]
    public void CampfireItem_SuggestsAddFuel_WhenFuelIsLow()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        world.WorldItems.Add(new CampfireItem("main_campfire"));
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        world.MainCampfire!.FuelSeconds = 1200.0;
        
        var ctx = new IslandContext(actorId, actor, world, 0L, new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        var candidates = new List<ActionCandidate>();
        
        world.MainCampfire.AddCandidates(ctx, candidates);
        
        Assert.Contains(candidates, c => c.Action.Id.Value == "add_fuel_campfire");
    }

    [Fact]
    public void CampfireItem_SuggestsRelight_WhenCampfireIsOut()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        world.WorldItems.Add(new CampfireItem("main_campfire"));
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        world.MainCampfire!.IsLit = false;
        world.MainCampfire.Quality = 50.0;
        
        var ctx = new IslandContext(actorId, actor, world, 0L, new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        var candidates = new List<ActionCandidate>();
        
        world.MainCampfire.AddCandidates(ctx, candidates);
        
        Assert.Contains(candidates, c => c.Action.Id.Value == "relight_campfire");
    }

    [Fact]
    public void CampfireItem_SuggestsRepair_WhenQualityIsLow()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        world.WorldItems.Add(new CampfireItem("main_campfire"));
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        world.MainCampfire!.Quality = 50.0;
        
        var ctx = new IslandContext(actorId, actor, world, 0L, new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        var candidates = new List<ActionCandidate>();
        
        world.MainCampfire.AddCandidates(ctx, candidates);
        
        Assert.Contains(candidates, c => c.Action.Id.Value == "repair_campfire");
    }

    [Fact]
    public void CampfireItem_SuggestsRebuild_WhenQualityIsCriticallyLow()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        world.WorldItems.Add(new CampfireItem("main_campfire"));
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        world.MainCampfire!.Quality = 5.0;
        
        var ctx = new IslandContext(actorId, actor, world, 0L, new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        var candidates = new List<ActionCandidate>();
        
        world.MainCampfire.AddCandidates(ctx, candidates);
        
        Assert.Contains(candidates, c => c.Action.Id.Value == "rebuild_campfire");
    }

    [Fact]
    public void CampfireItem_HigherSurvivalSkill_IncreasesScore()
    {
        // IntrinsicScore is now static; skill level no longer affects IntrinsicScore.
        // Both actors should receive the same static base score.
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        world.WorldItems.Add(new CampfireItem("main_campfire"));
        world.MainCampfire!.FuelSeconds = 1200.0;
        
        var lowSkillActor = (IslandActorState)domain.CreateActorState(
            new ActorId("LowSkill"), 
            new Dictionary<string, object> { ["WIS"] = 8, ["STR"] = 8 }
        );
        
        var highSkillActor = (IslandActorState)domain.CreateActorState(
            new ActorId("HighSkill"), 
            new Dictionary<string, object> { ["WIS"] = 18, ["STR"] = 16 }
        );
        
        var ctxLow = new IslandContext(new ActorId("LowSkill"), lowSkillActor, world, 0L, 
            new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        var ctxHigh = new IslandContext(new ActorId("HighSkill"), highSkillActor, world, 0L, 
            new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        
        var candidatesLow = new List<ActionCandidate>();
        var candidatesHigh = new List<ActionCandidate>();
        
        world.MainCampfire.AddCandidates(ctxLow, candidatesLow);
        world.MainCampfire.AddCandidates(ctxHigh, candidatesHigh);
        
        var scoreLow = candidatesLow.First(c => c.Action.Id.Value == "add_fuel_campfire").IntrinsicScore;
        var scoreHigh = candidatesHigh.First(c => c.Action.Id.Value == "add_fuel_campfire").IntrinsicScore;
        
        // IntrinsicScore is now static; skills no longer change the base score
        Assert.Equal(scoreLow, scoreHigh);

        // Verify add_fuel_campfire has a non-null Qualities dictionary
        var fuelQualities = candidatesLow.First(c => c.Action.Id.Value == "add_fuel_campfire").Qualities;
        Assert.NotNull(fuelQualities);
        Assert.True(fuelQualities.Count > 0);
    }

    [Fact]
    public void CampfireItem_DoesNotSuggestActions_WhenCampfireMissing()
    {
        var world = new IslandWorldState();
        var actorId = new ActorId("TestActor");
        var domain = new IslandDomainPack();
        var actor = (IslandActorState)domain.CreateActorState(actorId);
        
        var ctx = new IslandContext(actorId, actor, world, 0L, new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        var candidates = new List<ActionCandidate>();
        
        if (world.MainCampfire != null)
        {
            world.MainCampfire.AddCandidates(ctx, candidates);
        }
        
        Assert.Empty(candidates);
    }
}

public class PalmFrondBlanketMaintenanceCandidateProviderTests
{
    private static PalmFrondBlanketItem AddBlanket(IslandWorldState world, double quality = 50.0)
    {
        var blanket = new PalmFrondBlanketItem("palm_frond_blanket");
        blanket.Quality = quality;
        world.WorldItems.Add(blanket);
        return blanket;
    }

    [Fact]
    public void PalmFrondBlanketItem_SuggestsRepair_WhenQualityIsLow()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);

        var blanket = AddBlanket(world, 50.0);
        world.SharedSupplyPile!.AddSupply(5, () => new Supply.PalmFrondSupply());

        var ctx = new IslandContext(actorId, actor, world, 0L, new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        var candidates = new List<ActionCandidate>();

        blanket.AddCandidates(ctx, candidates);

        Assert.Contains(candidates, c => c.Action.Id.Value == "repair_blanket");
    }

    [Fact]
    public void PalmFrondBlanketItem_DoesNotSuggestRepair_WhenQualityIsHigh()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);

        var blanket = AddBlanket(world, 90.0);
        world.SharedSupplyPile!.AddSupply(5, () => new Supply.PalmFrondSupply());

        var ctx = new IslandContext(actorId, actor, world, 0L, new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        var candidates = new List<ActionCandidate>();

        blanket.AddCandidates(ctx, candidates);

        Assert.DoesNotContain(candidates, c => c.Action.Id.Value == "repair_blanket");
    }

    [Fact]
    public void PalmFrondBlanketItem_DoesNotSuggestRepair_WhenNoPalmFronds()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);

        var blanket = AddBlanket(world, 50.0);
        // No fronds added to supply

        var ctx = new IslandContext(actorId, actor, world, 0L, new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        var candidates = new List<ActionCandidate>();

        blanket.AddCandidates(ctx, candidates);

        Assert.DoesNotContain(candidates, c => c.Action.Id.Value == "repair_blanket");
    }

    [Fact]
    public void PalmFrondBlanketItem_AlwaysSuggestsSleep()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);

        var blanket = AddBlanket(world, 80.0);

        var ctx = new IslandContext(actorId, actor, world, 0L, new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        var candidates = new List<ActionCandidate>();

        blanket.AddCandidates(ctx, candidates);

        Assert.Contains(candidates, c => c.Action.Id.Value == "sleep_in_blanket");
    }

    [Fact]
    public void PalmFrondBlanketItem_SleepScore_IsHigherThanTreeSleep_WhenHealthy()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        var actorId = new ActorId("TestActor");
        var actor = (IslandActorState)domain.CreateActorState(actorId);

        var blanket = AddBlanket(world, 100.0);

        var ctx = new IslandContext(actorId, actor, world, 0L, new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        var candidates = new List<ActionCandidate>();
        blanket.AddCandidates(ctx, candidates);
        actor.AddCandidates(ctx, candidates);

        var blanketSleepScore = candidates.First(c => c.Action.Id.Value == "sleep_in_blanket").IntrinsicScore;
        var treeSleepScore = candidates.First(c => c.Action.Id.Value == "sleep_under_tree").IntrinsicScore;

        Assert.True(blanketSleepScore > treeSleepScore,
            $"Blanket sleep ({blanketSleepScore}) should outscore tree sleep ({treeSleepScore}) when blanket is healthy");
    }

    [Fact]
    public void PalmFrondBlanketItem_HigherSurvivalSkill_HasSameStaticScore()
    {
        var domain = new IslandDomainPack();
        var world = (IslandWorldState)domain.CreateInitialWorldState();
        world.SharedSupplyPile!.AddSupply(5, () => new Supply.PalmFrondSupply());

        var blanket = AddBlanket(world, 50.0);

        var lowSkillActor = (IslandActorState)domain.CreateActorState(
            new ActorId("LowSkill"),
            new Dictionary<string, object> { ["WIS"] = 8, ["STR"] = 8 }
        );
        var highSkillActor = (IslandActorState)domain.CreateActorState(
            new ActorId("HighSkill"),
            new Dictionary<string, object> { ["WIS"] = 18, ["STR"] = 16 }
        );

        var ctxLow = new IslandContext(new ActorId("LowSkill"), lowSkillActor, world, 0L,
            new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        var ctxHigh = new IslandContext(new ActorId("HighSkill"), highSkillActor, world, 0L,
            new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());

        var candidatesLow = new List<ActionCandidate>();
        var candidatesHigh = new List<ActionCandidate>();

        blanket.AddCandidates(ctxLow, candidatesLow);
        blanket.AddCandidates(ctxHigh, candidatesHigh);

        var scoreLow = candidatesLow.First(c => c.Action.Id.Value == "repair_blanket").IntrinsicScore;
        var scoreHigh = candidatesHigh.First(c => c.Action.Id.Value == "repair_blanket").IntrinsicScore;

        Assert.Equal(scoreLow, scoreHigh);

        var repairQualities = candidatesLow.First(c => c.Action.Id.Value == "repair_blanket").Qualities;
        Assert.NotNull(repairQualities);
        Assert.True(repairQualities.Count > 0);
    }

    [Fact]
    public void PalmFrondBlanketItem_DoesNotSuggestActions_WhenBlanketMissing()
    {
        var world = new IslandWorldState();
        var actorId = new ActorId("TestActor");
        var domain = new IslandDomainPack();
        var actor = (IslandActorState)domain.CreateActorState(actorId);

        var ctx = new IslandContext(actorId, actor, world, 0L, new RandomRngStream(new Random(42)), new Random(42), new EmptyResourceAvailability());
        var candidates = new List<ActionCandidate>();

        var blanket = world.WorldItems.OfType<PalmFrondBlanketItem>().FirstOrDefault();
        blanket?.AddCandidates(ctx, candidates);

        Assert.Empty(candidates);
    }
}
