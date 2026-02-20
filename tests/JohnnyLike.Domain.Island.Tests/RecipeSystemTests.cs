using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Island.Recipes;
using JohnnyLike.Domain.Island.Stats;
using JohnnyLike.Domain.Island.Supply;

namespace JohnnyLike.Domain.Island.Tests;

public class RecipeSystemTests
{
    // ── Helpers ────────────────────────────────────────────────────────────────

    private static IslandContext MakeContext(
        IslandActorState actor,
        IslandWorldState world,
        Random? rng = null)
    {
        rng ??= new Random(42);
        return new IslandContext(
            new ActorId("test_actor"),
            actor,
            world,
            0.0,
            new RandomRngStream(rng),
            rng,
            new EmptyResourceAvailability()
        );
    }

    private static (IslandActorState actor, IslandWorldState world) MakeBase()
    {
        var world = new IslandWorldState();
        world.WorldItems.Add(new CampfireItem("main_campfire"));

        var pile = new SupplyPile("shared_supplies", "shared");
        world.WorldItems.Add(pile);

        world.WorldStats.Add(new TimeOfDayStat());
        world.WorldStats.Add(new WeatherStat());

        var actor = new IslandActorState { Id = new ActorId("test_actor") };
        return (actor, world);
    }

    // ── Registry ───────────────────────────────────────────────────────────────

    [Fact]
    public void Registry_ContainsCookFishAndUmbrella()
    {
        Assert.True(IslandRecipeRegistry.All.ContainsKey("cook_fish"));
        Assert.True(IslandRecipeRegistry.All.ContainsKey("umbrella"));
    }

    // ── cook_fish candidate ────────────────────────────────────────────────────

    [Fact]
    public void CookFish_CandidateExists_WhenKnownAndPrereqsMet()
    {
        var (actor, world) = MakeBase();

        // Light the campfire
        var campfire = world.MainCampfire!;
        campfire.FuelSeconds = 600;

        // Add fish to supply pile
        world.SharedSupplyPile!.AddSupply("fish", 2, id => new FishSupply(id));

        actor.KnownRecipeIds.Add("cook_fish");

        var ctx = MakeContext(actor, world);
        var candidates = new List<ActionCandidate>();
        actor.AddCandidates(ctx, candidates);

        Assert.Contains(candidates, c => c.Action.Id.Value == "cook_fish");
    }

    [Fact]
    public void CookFish_CandidateAbsent_WhenNotKnown()
    {
        var (actor, world) = MakeBase();
        var campfire = world.MainCampfire!;
        campfire.FuelSeconds = 600;
        world.SharedSupplyPile!.AddSupply("fish", 2, id => new FishSupply(id));

        // KnownRecipeIds is empty

        var ctx = MakeContext(actor, world);
        var candidates = new List<ActionCandidate>();
        actor.AddCandidates(ctx, candidates);

        Assert.DoesNotContain(candidates, c => c.Action.Id.Value == "cook_fish");
    }

    [Fact]
    public void CookFish_CandidateAbsent_WhenCampfireUnlit()
    {
        var (actor, world) = MakeBase();
        // Set campfire to unlit
        var campfire = world.MainCampfire!;
        campfire.IsLit = false;
        campfire.FuelSeconds = 0;
        world.SharedSupplyPile!.AddSupply("fish", 2, id => new FishSupply(id));
        actor.KnownRecipeIds.Add("cook_fish");

        var ctx = MakeContext(actor, world);
        var candidates = new List<ActionCandidate>();
        actor.AddCandidates(ctx, candidates);

        Assert.DoesNotContain(candidates, c => c.Action.Id.Value == "cook_fish");
    }

    // ── umbrella candidate ─────────────────────────────────────────────────────

    [Fact]
    public void Umbrella_CandidateAbsent_BeforeDiscovery()
    {
        var (actor, world) = MakeBase();
        var pile = world.SharedSupplyPile!;
        pile.AddSupply("stick", 5, id => new StickSupply(id));
        pile.AddSupply("palm_frond", 5, id => new PalmFrondSupply(id));

        // KnownRecipeIds does NOT contain "umbrella"

        var ctx = MakeContext(actor, world);
        var candidates = new List<ActionCandidate>();
        actor.AddCandidates(ctx, candidates);

        Assert.DoesNotContain(candidates, c => c.Action.Id.Value == "craft_umbrella");
    }

    [Fact]
    public void Umbrella_CandidateExists_AfterDiscovery()
    {
        var (actor, world) = MakeBase();
        var pile = world.SharedSupplyPile!;
        pile.AddSupply("stick", 5, id => new StickSupply(id));
        pile.AddSupply("palm_frond", 5, id => new PalmFrondSupply(id));

        actor.KnownRecipeIds.Add("umbrella");

        var ctx = MakeContext(actor, world);
        var candidates = new List<ActionCandidate>();
        actor.AddCandidates(ctx, candidates);

        Assert.Contains(candidates, c => c.Action.Id.Value == "craft_umbrella");
    }

    // ── supplies consumed and produced ────────────────────────────────────────

    [Fact]
    public void CookFish_Effect_ProducesCookedFish_AndPreAction_ConsumesRawFish()
    {
        var (actor, world) = MakeBase();
        var campfire = world.MainCampfire!;
        campfire.FuelSeconds = 600;
        var pile = world.SharedSupplyPile!;
        pile.AddSupply("fish", 3, id => new FishSupply(id));

        var recipe = IslandRecipeRegistry.Get("cook_fish");
        var ctx = MakeContext(actor, world);

        // PreAction consumes fish
        var effectCtx = MakeEffectContext(actor, world);
        var preOk = recipe.PreAction(effectCtx);
        Assert.True(preOk);
        Assert.Equal(2.0, pile.GetQuantity<FishSupply>("fish"));

        // Effect produces cooked fish
        recipe.Effect(effectCtx);
        Assert.Equal(1.0, pile.GetQuantity<CookedFishSupply>("cooked_fish"));
    }

    [Fact]
    public void Umbrella_Effect_ProducesUmbrellaTool_AndPreAction_ConsumesIngredients()
    {
        var (actor, world) = MakeBase();
        var pile = world.SharedSupplyPile!;
        pile.AddSupply("stick", 5, id => new StickSupply(id));
        pile.AddSupply("palm_frond", 6, id => new PalmFrondSupply(id));

        var recipe = IslandRecipeRegistry.Get("umbrella");
        var effectCtx = MakeEffectContext(actor, world);

        var preOk = recipe.PreAction(effectCtx);
        Assert.True(preOk);
        Assert.Equal(3.0, pile.GetQuantity<StickSupply>("stick"));
        Assert.Equal(3.0, pile.GetQuantity<PalmFrondSupply>("palm_frond"));

        recipe.Effect(effectCtx);
        var umbrellaItem = world.WorldItems.OfType<UmbrellaItem>().FirstOrDefault();
        Assert.NotNull(umbrellaItem);
        Assert.Equal(actor.Id, umbrellaItem.OwnerActorId);
    }

    [Fact]
    public void Umbrella_PreAction_Fails_WhenInsufficientIngredients()
    {
        var (actor, world) = MakeBase();
        var pile = world.SharedSupplyPile!;
        pile.AddSupply("stick", 1, id => new StickSupply(id));   // needs 2
        pile.AddSupply("palm_frond", 3, id => new PalmFrondSupply(id));

        var recipe = IslandRecipeRegistry.Get("umbrella");
        var effectCtx = MakeEffectContext(actor, world);

        var preOk = recipe.PreAction(effectCtx);
        Assert.False(preOk);
    }

    // ── discovery ─────────────────────────────────────────────────────────────

    [Fact]
    public void Discovery_Umbrella_UnlockedWhenRainyAndMaterialsPresent_WithLowSeed()
    {
        // Find a seed where NextDouble() < 0.25 for the umbrella roll
        int seed = FindSeedBelow(0.25);

        var (actor, world) = MakeBase();
        var weather = world.GetStat<WeatherStat>("weather")!;
        weather.Weather = Weather.Rainy;

        var pile = world.SharedSupplyPile!;
        pile.AddSupply("stick", 1, id => new StickSupply(id));
        pile.AddSupply("palm_frond", 1, id => new PalmFrondSupply(id));

        var ctx = MakeContext(actor, world, new Random(seed));
        RecipeDiscoverySystem.TryDiscover(ctx, DiscoveryTrigger.ThinkAboutSupplies);

        Assert.Contains("umbrella", actor.KnownRecipeIds);
    }

    [Fact]
    public void Discovery_Umbrella_NotUnlocked_WhenRollTooHigh()
    {
        // Find a seed where NextDouble() >= 0.25
        int seed = FindSeedAboveOrEqual(0.25);

        var (actor, world) = MakeBase();
        var weather = world.GetStat<WeatherStat>("weather")!;
        weather.Weather = Weather.Rainy;

        var pile = world.SharedSupplyPile!;
        pile.AddSupply("stick", 1, id => new StickSupply(id));
        pile.AddSupply("palm_frond", 1, id => new PalmFrondSupply(id));

        var ctx = MakeContext(actor, world, new Random(seed));
        RecipeDiscoverySystem.TryDiscover(ctx, DiscoveryTrigger.ThinkAboutSupplies);

        Assert.DoesNotContain("umbrella", actor.KnownRecipeIds);
    }

    [Fact]
    public void Discovery_Umbrella_NotUnlocked_WhenWeatherNotRainy()
    {
        int seed = FindSeedBelow(0.25);

        var (actor, world) = MakeBase();
        var weather = world.GetStat<WeatherStat>("weather")!;
        weather.Weather = Weather.Clear; // not rainy

        var pile = world.SharedSupplyPile!;
        pile.AddSupply("stick", 1, id => new StickSupply(id));
        pile.AddSupply("palm_frond", 1, id => new PalmFrondSupply(id));

        var ctx = MakeContext(actor, world, new Random(seed));
        RecipeDiscoverySystem.TryDiscover(ctx, DiscoveryTrigger.ThinkAboutSupplies);

        Assert.DoesNotContain("umbrella", actor.KnownRecipeIds);
    }

    [Fact]
    public void Discovery_Deterministic_BySeed()
    {
        var (actor1, world1) = MakeBase();
        var (actor2, world2) = MakeBase();

        foreach (var (actor, world) in new[] { (actor1, world1), (actor2, world2) })
        {
            world.GetStat<WeatherStat>("weather")!.Weather = Weather.Rainy;
            var pile = world.SharedSupplyPile!;
            pile.AddSupply("stick", 1, id => new StickSupply(id));
            pile.AddSupply("palm_frond", 1, id => new PalmFrondSupply(id));
        }

        var seed = 12345;
        RecipeDiscoverySystem.TryDiscover(MakeContext(actor1, world1, new Random(seed)), DiscoveryTrigger.ThinkAboutSupplies);
        RecipeDiscoverySystem.TryDiscover(MakeContext(actor2, world2, new Random(seed)), DiscoveryTrigger.ThinkAboutSupplies);

        Assert.Equal(actor1.KnownRecipeIds.Contains("umbrella"), actor2.KnownRecipeIds.Contains("umbrella"));
    }

    [Fact]
    public void Discovery_AlreadyKnown_NotDiscoveredAgain()
    {
        var (actor, world) = MakeBase();
        world.GetStat<WeatherStat>("weather")!.Weather = Weather.Rainy;
        var pile = world.SharedSupplyPile!;
        pile.AddSupply("stick", 1, id => new StickSupply(id));
        pile.AddSupply("palm_frond", 1, id => new PalmFrondSupply(id));

        actor.KnownRecipeIds.Add("umbrella"); // already known

        var ctx = MakeContext(actor, world, new Random(1));
        RecipeDiscoverySystem.TryDiscover(ctx, DiscoveryTrigger.ThinkAboutSupplies);

        // Still just one entry - not duplicated
        Assert.Contains("umbrella", actor.KnownRecipeIds);
        Assert.Single(actor.KnownRecipeIds, id => id == "umbrella");
    }

    // ── serialization ─────────────────────────────────────────────────────────

    [Fact]
    public void ActorState_Serialization_PreservesKnownRecipeIds()
    {
        var actor = new IslandActorState { Id = new ActorId("test_actor") };
        actor.KnownRecipeIds.Add("cook_fish");
        actor.KnownRecipeIds.Add("umbrella");

        var json = actor.Serialize();

        var actor2 = new IslandActorState();
        actor2.Deserialize(json);

        Assert.Contains("cook_fish", actor2.KnownRecipeIds);
        Assert.Contains("umbrella", actor2.KnownRecipeIds);
    }

    // ── UmbrellaItem tool ─────────────────────────────────────────────────────

    [Fact]
    public void UmbrellaItem_DeployCandidate_OfferedDuringRain_WhenBuffAbsent()
    {
        var (actor, world) = MakeBase();
        world.GetStat<WeatherStat>("weather")!.Weather = Weather.Rainy;

        var umbrella = new UmbrellaItem($"umbrella_{actor.Id.Value}", actor.Id);
        world.WorldItems.Add(umbrella);

        var ctx = MakeContext(actor, world);
        var candidates = new List<ActionCandidate>();
        umbrella.AddCandidates(ctx, candidates);

        Assert.Contains(candidates, c => c.Action.Id.Value == "deploy_umbrella");
        Assert.DoesNotContain(candidates, c => c.Action.Id.Value == "holster_umbrella");
    }

    [Fact]
    public void UmbrellaItem_DeployCandidate_NotOffered_WhenNotRaining()
    {
        var (actor, world) = MakeBase();
        world.GetStat<WeatherStat>("weather")!.Weather = Weather.Clear;

        var umbrella = new UmbrellaItem($"umbrella_{actor.Id.Value}", actor.Id);
        world.WorldItems.Add(umbrella);

        var ctx = MakeContext(actor, world);
        var candidates = new List<ActionCandidate>();
        umbrella.AddCandidates(ctx, candidates);

        Assert.DoesNotContain(candidates, c => c.Action.Id.Value == "deploy_umbrella");
    }

    [Fact]
    public void UmbrellaItem_DeployEffect_AddsRainProtectionBuff()
    {
        var (actor, world) = MakeBase();
        world.GetStat<WeatherStat>("weather")!.Weather = Weather.Rainy;

        var umbrella = new UmbrellaItem($"umbrella_{actor.Id.Value}", actor.Id);
        world.WorldItems.Add(umbrella);

        var ctx = MakeContext(actor, world);
        var candidates = new List<ActionCandidate>();
        umbrella.AddCandidates(ctx, candidates);

        var deployCandidate = candidates.Single(c => c.Action.Id.Value == "deploy_umbrella");
        var effectHandler = (Action<EffectContext>)deployCandidate.EffectHandler!;

        effectHandler(MakeEffectContext(actor, world));

        Assert.Contains(actor.ActiveBuffs, b => b.Name == UmbrellaItem.RainProtectionBuffName && b.Type == BuffType.RainProtection);
    }

    [Fact]
    public void UmbrellaItem_HolsterCandidate_OfferedWhenBuffActiveAndNotRaining()
    {
        var (actor, world) = MakeBase();
        world.GetStat<WeatherStat>("weather")!.Weather = Weather.Clear;

        actor.ActiveBuffs.Add(new ActiveBuff
        {
            Name = UmbrellaItem.RainProtectionBuffName,
            Type = BuffType.RainProtection,
            ExpiresAt = double.MaxValue
        });

        var umbrella = new UmbrellaItem($"umbrella_{actor.Id.Value}", actor.Id);
        world.WorldItems.Add(umbrella);

        var ctx = MakeContext(actor, world);
        var candidates = new List<ActionCandidate>();
        umbrella.AddCandidates(ctx, candidates);

        Assert.Contains(candidates, c => c.Action.Id.Value == "holster_umbrella");
        Assert.DoesNotContain(candidates, c => c.Action.Id.Value == "deploy_umbrella");
    }

    [Fact]
    public void UmbrellaItem_HolsterEffect_RemovesRainProtectionBuff()
    {
        var (actor, world) = MakeBase();
        world.GetStat<WeatherStat>("weather")!.Weather = Weather.Clear;

        actor.ActiveBuffs.Add(new ActiveBuff
        {
            Name = UmbrellaItem.RainProtectionBuffName,
            Type = BuffType.RainProtection,
            ExpiresAt = double.MaxValue
        });

        var umbrella = new UmbrellaItem($"umbrella_{actor.Id.Value}", actor.Id);
        world.WorldItems.Add(umbrella);

        var ctx = MakeContext(actor, world);
        var candidates = new List<ActionCandidate>();
        umbrella.AddCandidates(ctx, candidates);

        var holsterCandidate = candidates.Single(c => c.Action.Id.Value == "holster_umbrella");
        var effectHandler = (Action<EffectContext>)holsterCandidate.EffectHandler!;

        effectHandler(MakeEffectContext(actor, world));

        Assert.DoesNotContain(actor.ActiveBuffs, b => b.Name == UmbrellaItem.RainProtectionBuffName);
    }

    [Fact]
    public void UmbrellaItem_NoCandidates_ForOtherActor()
    {
        var (actor, world) = MakeBase();
        world.GetStat<WeatherStat>("weather")!.Weather = Weather.Rainy;

        var otherActorId = new ActorId("other_actor");
        var umbrella = new UmbrellaItem($"umbrella_{otherActorId.Value}", otherActorId);
        world.WorldItems.Add(umbrella);

        var ctx = MakeContext(actor, world); // actor is not the owner
        var candidates = new List<ActionCandidate>();
        umbrella.AddCandidates(ctx, candidates);

        Assert.Empty(candidates);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static EffectContext MakeEffectContext(IslandActorState actor, IslandWorldState world)
    {
        return new EffectContext
        {
            ActorId = actor.Id,
            Outcome = new ActionOutcome(new ActionId("test"), ActionOutcomeType.Success, 0.0),
            Actor = actor,
            World = world,
            Tier = null,
            Rng = new RandomRngStream(new Random(42)),
            Reservations = new EmptyResourceAvailability()
        };
    }

    private static int FindSeedBelow(double threshold)
    {
        for (int s = 0; s < 10000; s++)
        {
            if (new Random(s).NextDouble() < threshold)
                return s;
        }
        throw new InvalidOperationException("Could not find suitable seed");
    }

    private static int FindSeedAboveOrEqual(double threshold)
    {
        for (int s = 0; s < 10000; s++)
        {
            if (new Random(s).NextDouble() >= threshold)
                return s;
        }
        throw new InvalidOperationException("Could not find suitable seed");
    }
}
