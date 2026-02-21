using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Items;
using JohnnyLike.Domain.Island.Recipes;

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
        world.WorldItems.Add(new CalendarItem("calendar"));
        world.WorldItems.Add(new WeatherItem("weather"));

        var pile = new SupplyPile("shared_supplies", "shared");
        world.WorldItems.Add(pile);

        var actor = new IslandActorState { Id = new ActorId("test_actor") };
        return (actor, world);
    }

    // ── Registry ───────────────────────────────────────────────────────────────

    [Fact]
    public void Registry_ContainsCookFishUmbrellaRopeAndFishingPole()
    {
        Assert.True(IslandRecipeRegistry.All.ContainsKey("cook_fish"));
        Assert.True(IslandRecipeRegistry.All.ContainsKey("umbrella"));
        Assert.True(IslandRecipeRegistry.All.ContainsKey("rope"));
        Assert.True(IslandRecipeRegistry.All.ContainsKey("fishing_pole"));
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

    // ── rope recipe ──────────────────────────────────────────────────────────

    [Fact]
    public void Rope_CandidateExists_WhenKnownAndPalmFrondAvailable()
    {
        var (actor, world) = MakeBase();
        var pile = world.SharedSupplyPile!;
        pile.AddSupply(1, () => new PalmFrondSupply());

        actor.KnownRecipeIds.Add("rope");

        var ctx = MakeContext(actor, world);
        var candidates = new List<ActionCandidate>();
        actor.AddCandidates(ctx, candidates);

        Assert.Contains(candidates, c => c.Action.Id.Value == "craft_rope");
    }

    [Fact]
    public void Rope_CandidateAbsent_WhenInsufficientPalmFrond()
    {
        var (actor, world) = MakeBase();
        actor.KnownRecipeIds.Add("rope");

        var ctx = MakeContext(actor, world);
        var candidates = new List<ActionCandidate>();
        actor.AddCandidates(ctx, candidates);

        Assert.DoesNotContain(candidates, c => c.Action.Id.Value == "craft_rope");
    }

    [Fact]
    public void Rope_Effect_ProducesThreeRope_AndPreAction_ConsumesOnePalmFrond()
    {
        var (actor, world) = MakeBase();
        var pile = world.SharedSupplyPile!;
        pile.AddSupply(2, () => new PalmFrondSupply());

        var recipe = IslandRecipeRegistry.Get("rope");
        var effectCtx = MakeEffectContext(actor, world);

        var preOk = recipe.PreAction(effectCtx);
        Assert.True(preOk);
        Assert.Equal(1.0, pile.GetQuantity<PalmFrondSupply>());

        recipe.Effect(effectCtx);
        Assert.Equal(3.0, pile.GetQuantity<RopeSupply>());
    }

    // ── fishing pole recipe ──────────────────────────────────────────────────

    [Fact]
    public void FishingPole_CandidateExists_WhenKnownAndIngredientsAvailable()
    {
        var (actor, world) = MakeBase();
        var pile = world.SharedSupplyPile!;
        pile.AddSupply(3, () => new StickSupply());
        pile.AddSupply(2, () => new RopeSupply());

        actor.KnownRecipeIds.Add("fishing_pole");

        var ctx = MakeContext(actor, world);
        var candidates = new List<ActionCandidate>();
        actor.AddCandidates(ctx, candidates);

        Assert.Contains(candidates, c => c.Action.Id.Value == "craft_fishing_pole");
    }

    [Fact]
    public void FishingPole_CandidateAbsent_WhenActorAlreadyOwnsPole()
    {
        var (actor, world) = MakeBase();
        var pile = world.SharedSupplyPile!;
        pile.AddSupply(3, () => new StickSupply());
        pile.AddSupply(2, () => new RopeSupply());
        world.WorldItems.Add(new FishingPoleItem($"fishing_pole_{actor.Id.Value}", actor.Id));

        actor.KnownRecipeIds.Add("fishing_pole");

        var ctx = MakeContext(actor, world);
        var candidates = new List<ActionCandidate>();
        actor.AddCandidates(ctx, candidates);

        Assert.DoesNotContain(candidates, c => c.Action.Id.Value == "craft_fishing_pole");
    }

    [Fact]
    public void FishingPole_Effect_CreatesOwnedPole_AndPreAction_ConsumesIngredients()
    {
        var (actor, world) = MakeBase();
        var pile = world.SharedSupplyPile!;
        pile.AddSupply(5, () => new StickSupply());
        pile.AddSupply(4, () => new RopeSupply());

        var recipe = IslandRecipeRegistry.Get("fishing_pole");
        var effectCtx = MakeEffectContext(actor, world);

        var preOk = recipe.PreAction(effectCtx);
        Assert.True(preOk);
        Assert.Equal(2.0, pile.GetQuantity<StickSupply>());
        Assert.Equal(2.0, pile.GetQuantity<RopeSupply>());

        recipe.Effect(effectCtx);
        var pole = world.WorldItems
            .OfType<FishingPoleItem>()
            .SingleOrDefault(p => p.OwnerActorId == actor.Id);

        Assert.NotNull(pole);
    }

    [Fact]
    public void Umbrella_SupplyCosts_AreDefinedInOnePlace()
    {
        var recipe = IslandRecipeRegistry.Get("umbrella");

        Assert.NotNull(recipe.SupplyCosts);
        Assert.Collection(
            recipe.SupplyCosts!,
            c =>
            {
                Assert.Equal("StickSupply", c.Name);
                Assert.Equal(2.0, c.Quantity);
            },
            c =>
            {
                Assert.Equal("PalmFrondSupply", c.Name);
                Assert.Equal(3.0, c.Quantity);
            });
    }

    [Fact]
    public void Umbrella_HasRequiredSupplies_UsesCentralizedSupplyCosts()
    {
        var recipe = IslandRecipeRegistry.Get("umbrella");

        var (_, worldMissing) = MakeBase();
        var missingPile = worldMissing.SharedSupplyPile!;
        missingPile.AddSupply(1, () => new StickSupply());
        missingPile.AddSupply(3, () => new PalmFrondSupply());
        Assert.False(recipe.HasRequiredSupplies(missingPile));

        var (_, worldEnough) = MakeBase();
        var enoughPile = worldEnough.SharedSupplyPile!;
        enoughPile.AddSupply(2, () => new StickSupply());
        enoughPile.AddSupply(3, () => new PalmFrondSupply());
        Assert.True(recipe.HasRequiredSupplies(enoughPile));
    }

    [Fact]
    public void Umbrella_TryConsumeRequiredSupplies_ConsumesExpectedAmounts()
    {
        var recipe = IslandRecipeRegistry.Get("umbrella");

        var (_, world) = MakeBase();
        var pile = world.SharedSupplyPile!;
        pile.AddSupply(5, () => new StickSupply());
        pile.AddSupply(6, () => new PalmFrondSupply());

        var consumed = recipe.TryConsumeRequiredSupplies(pile);

        Assert.True(consumed);
        Assert.Equal(3.0, pile.GetQuantity<StickSupply>());
        Assert.Equal(3.0, pile.GetQuantity<PalmFrondSupply>());
    }

    // ── discovery ─────────────────────────────────────────────────────────────

    [Fact]
    public void Discovery_Umbrella_UnlockedWhenRainyAndMaterialsPresent_WithLowSeed()
    {
        // Find a seed where NextDouble() < 0.25 for the umbrella roll
        int seed = FindSeedBelow(0.25);

        var (actor, world) = MakeBase();
        world.GetItem<WeatherItem>("weather")!.Precipitation = PrecipitationBand.Rainy;

        var pile = world.SharedSupplyPile!;
        pile.AddSupply("stick", 1, id => new StickSupply(id));
        pile.AddSupply("palm_frond", 1, id => new PalmFrondSupply(id));

        RecipeDiscoverySystem.TryDiscover(actor, world, new RandomRngStream(new Random(seed)), DiscoveryTrigger.ThinkAboutSupplies);

        Assert.Contains("umbrella", actor.KnownRecipeIds);
    }

    [Fact]
    public void Discovery_Umbrella_NotUnlocked_WhenRollTooHigh()
    {
        // Find a seed where NextDouble() >= 0.25
        int seed = FindSeedAboveOrEqual(0.25);

        var (actor, world) = MakeBase();
        world.GetItem<WeatherItem>("weather")!.Precipitation = PrecipitationBand.Rainy;

        var pile = world.SharedSupplyPile!;
        pile.AddSupply("stick", 1, id => new StickSupply(id));
        pile.AddSupply("palm_frond", 1, id => new PalmFrondSupply(id));

        RecipeDiscoverySystem.TryDiscover(actor, world, new RandomRngStream(new Random(seed)), DiscoveryTrigger.ThinkAboutSupplies);

        Assert.DoesNotContain("umbrella", actor.KnownRecipeIds);
    }

    [Fact]
    public void Discovery_Umbrella_NotUnlocked_WhenWeatherNotRainy()
    {
        int seed = FindSeedBelow(0.25);

        var (actor, world) = MakeBase();
        world.GetItem<WeatherItem>("weather")!.Precipitation = PrecipitationBand.Clear; // not rainy

        var pile = world.SharedSupplyPile!;
        pile.AddSupply("stick", 1, id => new StickSupply(id));
        pile.AddSupply("palm_frond", 1, id => new PalmFrondSupply(id));

        RecipeDiscoverySystem.TryDiscover(actor, world, new RandomRngStream(new Random(seed)), DiscoveryTrigger.ThinkAboutSupplies);

        Assert.DoesNotContain("umbrella", actor.KnownRecipeIds);
    }

    [Fact]
    public void Discovery_Deterministic_BySeed()
    {
        var (actor1, world1) = MakeBase();
        var (actor2, world2) = MakeBase();

        foreach (var (actor, world) in new[] { (actor1, world1), (actor2, world2) })
        {
            world.GetItem<WeatherItem>("weather")!.Precipitation = PrecipitationBand.Rainy;
            var pile = world.SharedSupplyPile!;
            pile.AddSupply("stick", 1, id => new StickSupply(id));
            pile.AddSupply("palm_frond", 1, id => new PalmFrondSupply(id));
        }

        var seed = 12345;
        RecipeDiscoverySystem.TryDiscover(actor1, world1, new RandomRngStream(new Random(seed)), DiscoveryTrigger.ThinkAboutSupplies);
        RecipeDiscoverySystem.TryDiscover(actor2, world2, new RandomRngStream(new Random(seed)), DiscoveryTrigger.ThinkAboutSupplies);

        Assert.Equal(actor1.KnownRecipeIds.Contains("umbrella"), actor2.KnownRecipeIds.Contains("umbrella"));
    }

    [Fact]
    public void Discovery_AlreadyKnown_NotDiscoveredAgain()
    {
        var (actor, world) = MakeBase();
        world.GetItem<WeatherItem>("weather")!.Precipitation = PrecipitationBand.Rainy;
        var pile = world.SharedSupplyPile!;
        pile.AddSupply("stick", 1, id => new StickSupply(id));
        pile.AddSupply("palm_frond", 1, id => new PalmFrondSupply(id));

        actor.KnownRecipeIds.Add("umbrella"); // already known

        RecipeDiscoverySystem.TryDiscover(actor, world, new RandomRngStream(new Random(1)), DiscoveryTrigger.ThinkAboutSupplies);

        // Still just one entry - not duplicated
        Assert.Contains("umbrella", actor.KnownRecipeIds);
        Assert.Single(actor.KnownRecipeIds, id => id == "umbrella");
    }

    // ── cook_fish discovery ────────────────────────────────────────────────────

    [Fact]
    public void CookFish_Discovered_WhenFishPresentAndCampfireLit()
    {
        int seed = FindSeedBelow(0.3);

        var (actor, world) = MakeBase();
        var campfire = world.MainCampfire!;
        campfire.FuelSeconds = 600;
        world.SharedSupplyPile!.AddSupply("fish", 1, id => new FishSupply(id));

        RecipeDiscoverySystem.TryDiscover(actor, world, new RandomRngStream(new Random(seed)), DiscoveryTrigger.ThinkAboutSupplies);

        Assert.Contains("cook_fish", actor.KnownRecipeIds);
    }

    [Fact]
    public void CookFish_NotDiscovered_WhenNoFish()
    {
        int seed = FindSeedBelow(0.3);

        var (actor, world) = MakeBase();
        var campfire = world.MainCampfire!;
        campfire.FuelSeconds = 600;
        // No fish in pile

        RecipeDiscoverySystem.TryDiscover(actor, world, new RandomRngStream(new Random(seed)), DiscoveryTrigger.ThinkAboutSupplies);

        Assert.DoesNotContain("cook_fish", actor.KnownRecipeIds);
    }

    [Fact]
    public void CookFish_NotDiscovered_WhenCampfireUnlit()
    {
        int seed = FindSeedBelow(0.3);

        var (actor, world) = MakeBase();
        var campfire = world.MainCampfire!;
        campfire.IsLit = false;
        campfire.FuelSeconds = 0;
        world.SharedSupplyPile!.AddSupply("fish", 1, id => new FishSupply(id));

        RecipeDiscoverySystem.TryDiscover(actor, world, new RandomRngStream(new Random(seed)), DiscoveryTrigger.ThinkAboutSupplies);

        Assert.DoesNotContain("cook_fish", actor.KnownRecipeIds);
    }

    [Fact]
    public void FishingPole_Discovered_WhenHungry()
    {
        var (actor, world) = MakeBase();
        actor.Satiety = 49.0;

        // Ensure no competing discoverable recipes in this setup
        world.GetItem<WeatherItem>("weather")!.Precipitation = PrecipitationBand.Clear;

        RecipeDiscoverySystem.TryDiscover(actor, world, new RandomRngStream(new Random(1)), DiscoveryTrigger.ThinkAboutSupplies);

        Assert.Contains("fishing_pole", actor.KnownRecipeIds);
    }

    [Fact]
    public void FishingPole_NotDiscovered_WhenNotHungry()
    {
        var (actor, world) = MakeBase();
        actor.Satiety = 50.0; // must be strictly less than 50

        RecipeDiscoverySystem.TryDiscover(actor, world, new RandomRngStream(new Random(1)), DiscoveryTrigger.ThinkAboutSupplies);

        Assert.DoesNotContain("fishing_pole", actor.KnownRecipeIds);
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
        world.GetItem<WeatherItem>("weather")!.Precipitation = PrecipitationBand.Rainy;

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
        world.GetItem<WeatherItem>("weather")!.Precipitation = PrecipitationBand.Clear;

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
        world.GetItem<WeatherItem>("weather")!.Precipitation = PrecipitationBand.Rainy;

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
        world.GetItem<WeatherItem>("weather")!.Precipitation = PrecipitationBand.Clear;

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
        world.GetItem<WeatherItem>("weather")!.Precipitation = PrecipitationBand.Clear;

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
        world.GetItem<WeatherItem>("weather")!.Precipitation = PrecipitationBand.Rainy;

        var otherActorId = new ActorId("other_actor");
        var umbrella = new UmbrellaItem($"umbrella_{otherActorId.Value}", otherActorId);
        world.WorldItems.Add(umbrella);

        var ctx = MakeContext(actor, world); // actor is not the owner
        var candidates = new List<ActionCandidate>();
        umbrella.AddCandidates(ctx, candidates);

        Assert.Empty(candidates);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static EffectContext MakeEffectContext(IslandActorState actor, IslandWorldState world, Random? rng = null)
    {
        return new EffectContext
        {
            ActorId = actor.Id,
            Outcome = new ActionOutcome(new ActionId("test"), ActionOutcomeType.Success, 0.0),
            Actor = actor,
            World = world,
            Tier = null,
            Rng = new RandomRngStream(rng ?? new Random(42)),
            Reservations = new EmptyResourceAvailability()
        };
    }

    // ── issue: deterministic discovery uses effect-time Rng ───────────────────

    [Fact]
    public void Discovery_SameEffectTimeRng_ProducesSameOutcome_RegardlessOfCandidateRngState()
    {
        // Two calls to TryDiscover with the same effect-time seed must produce the
        // same discovery result even when previous RNG state differs — ensuring
        // reproducibility when replaying from the same game state.

        int lowSeed = FindSeedBelow(0.25);

        var (actor1, world1) = MakeBase();
        var (actor2, world2) = MakeBase();

        foreach (var (actor, world) in new[] { (actor1, world1), (actor2, world2) })
        {
            world.GetItem<WeatherItem>("weather")!.Precipitation = PrecipitationBand.Rainy;
            var pile = world.SharedSupplyPile!;
            pile.AddSupply("stick", 1, id => new StickSupply(id));
            pile.AddSupply("palm_frond", 1, id => new PalmFrondSupply(id));
        }

        // Call 1: fresh rng at seed position 0 — roll is below 0.25 → discovers
        var rng1 = new RandomRngStream(new Random(lowSeed));
        RecipeDiscoverySystem.TryDiscover(actor1, world1, rng1, DiscoveryTrigger.ThinkAboutSupplies);
        Assert.Contains("umbrella", actor1.KnownRecipeIds);

        // Call 2: same seed (simulates a replay from the same game state) → same outcome
        var rng2 = new RandomRngStream(new Random(lowSeed));
        RecipeDiscoverySystem.TryDiscover(actor2, world2, rng2, DiscoveryTrigger.ThinkAboutSupplies);

        Assert.Equal(actor1.KnownRecipeIds.Contains("umbrella"), actor2.KnownRecipeIds.Contains("umbrella"));
    }

    [Fact]
    public void ThinkAboutSupplies_EffectHandler_ProducesSameResultWithSameEffectTimeRng()
    {
        // Verifies that EffectHandlers for think_about_supplies use effectCtx.Rng
        // rather than a closure over the candidate-generation ctx. Two actors with
        // different candidate-generation seeds but the same effect-time seed must
        // produce the same discovery outcome (reproducible from the same game state).

        int lowSeed = FindSeedBelow(0.25);

        var (actor1, world1) = MakeBase();
        var (actor2, world2) = MakeBase();

        foreach (var (actor, world) in new[] { (actor1, world1), (actor2, world2) })
        {
            world.GetItem<WeatherItem>("weather")!.Precipitation = PrecipitationBand.Rainy;
            world.SharedSupplyPile!.AddSupply("stick", 1, id => new StickSupply(id));
            world.SharedSupplyPile!.AddSupply("palm_frond", 1, id => new PalmFrondSupply(id));
        }

        // Generate candidates (advances the candidate-generation random by an
        // arbitrary but different amount for each actor)
        var candidateRng1 = new Random(999);
        var ctx1 = MakeContext(actor1, world1, candidateRng1);
        var candidates1 = new List<ActionCandidate>();
        actor1.AddCandidates(ctx1, candidates1);
        // Advance candidateRng1 further to simulate elapsed time
        for (int i = 0; i < 50; i++) candidateRng1.NextDouble();

        var candidateRng2 = new Random(12345); // different candidate seed
        var ctx2 = MakeContext(actor2, world2, candidateRng2);
        var candidates2 = new List<ActionCandidate>();
        actor2.AddCandidates(ctx2, candidates2);

        // Execute think_about_supplies with the SAME effect-time seed for both actors
        var think1 = candidates1.Single(c => c.Action.Id.Value == "think_about_supplies");
        var think2 = candidates2.Single(c => c.Action.Id.Value == "think_about_supplies");

        var effectHandler1 = (Action<EffectContext>)think1.EffectHandler!;
        var effectHandler2 = (Action<EffectContext>)think2.EffectHandler!;

        effectHandler1(MakeEffectContext(actor1, world1, new Random(lowSeed)));
        effectHandler2(MakeEffectContext(actor2, world2, new Random(lowSeed)));

        // Both ran with the same effect-time seed → same discovery outcome
        Assert.Equal(actor1.KnownRecipeIds.Contains("umbrella"), actor2.KnownRecipeIds.Contains("umbrella"));
    }

    // ── issue: duplicate umbrella prevention ──────────────────────────────────

    [Fact]
    public void Umbrella_CanCraft_ReturnsFalse_WhenActorAlreadyOwnsOne()
    {
        var (actor, world) = MakeBase();
        var pile = world.SharedSupplyPile!;
        pile.AddSupply("stick", 5, id => new StickSupply(id));
        pile.AddSupply("palm_frond", 5, id => new PalmFrondSupply(id));

        // Add an existing umbrella owned by this actor
        world.WorldItems.Add(new UmbrellaItem($"umbrella_{actor.Id.Value}", actor.Id));

        actor.KnownRecipeIds.Add("umbrella");

        var ctx = MakeContext(actor, world);
        var candidates = new List<ActionCandidate>();
        actor.AddCandidates(ctx, candidates);

        Assert.DoesNotContain(candidates, c => c.Action.Id.Value == "craft_umbrella");
    }

    [Fact]
    public void Umbrella_CanCraft_ReturnsTrue_WhenOtherActorOwnsOne()
    {
        var (actor, world) = MakeBase();
        var pile = world.SharedSupplyPile!;
        pile.AddSupply("stick", 5, id => new StickSupply(id));
        pile.AddSupply("palm_frond", 5, id => new PalmFrondSupply(id));

        // Another actor owns an umbrella — should not block this actor
        var otherId = new ActorId("other_actor");
        world.WorldItems.Add(new UmbrellaItem($"umbrella_{otherId.Value}", otherId));

        actor.KnownRecipeIds.Add("umbrella");

        var ctx = MakeContext(actor, world);
        var candidates = new List<ActionCandidate>();
        actor.AddCandidates(ctx, candidates);

        Assert.Contains(candidates, c => c.Action.Id.Value == "craft_umbrella");
    }

    // ── issue: unknown recipe IDs skipped gracefully ──────────────────────────

    [Fact]
    public void AddCandidates_SkipsUnknownRecipeId_DoesNotThrow()
    {
        var (actor, world) = MakeBase();
        actor.KnownRecipeIds.Add("nonexistent_recipe_id");

        var ctx = MakeContext(actor, world);
        var candidates = new List<ActionCandidate>();

        // Must not throw KeyNotFoundException
        var ex = Record.Exception(() => actor.AddCandidates(ctx, candidates));
        Assert.Null(ex);
    }

    [Fact]
    public void AddCandidates_SkipsUnknownRecipeId_OtherRecipesStillWork()
    {
        var (actor, world) = MakeBase();
        var campfire = world.MainCampfire!;
        campfire.FuelSeconds = 600;
        world.SharedSupplyPile!.AddSupply("fish", 2, id => new FishSupply(id));

        actor.KnownRecipeIds.Add("nonexistent_recipe_id");
        actor.KnownRecipeIds.Add("cook_fish");

        var ctx = MakeContext(actor, world);
        var candidates = new List<ActionCandidate>();
        actor.AddCandidates(ctx, candidates);

        // cook_fish candidate should still be present despite the stale ID
        Assert.Contains(candidates, c => c.Action.Id.Value == "cook_fish");
    }

    // ── helpers ───────────────────────────────────────────────────────────────

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
