using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Recipes;
using System.Text.Json;

namespace JohnnyLike.Domain.Island.Supply;

/// <summary>
/// Represents a pile of supplies with generic methods to manage different supply types
/// </summary>
public class SupplyPile : WorldItem, IIslandActionCandidate, ISupplyBounty, IIslandWorldEventProvider
{
    public List<SupplyItem> Supplies { get; set; } = new();
    public string AccessControl { get; set; }

    // ISupplyBounty: route through the Supplies list so default interface methods work too
    List<SupplyItem> ISupplyBounty.BountySupplies => Supplies;
    Dictionary<string, Dictionary<string, double>> ISupplyBounty.ActiveReservations { get; } = new();

    public SupplyPile(string id, string accessControl = "shared") 
        : base(id, "supply_pile")
    {
        AccessControl = accessControl;
    }

    /// <summary>
    /// Gets a specific supply by ID, or null if not found
    /// </summary>
    public T? GetSupply<T>(string supplyId) where T : SupplyItem
    {
        return (Supplies.FirstOrDefault(s => s.Id == supplyId && s is T) as T)
            ?? Supplies.OfType<T>().FirstOrDefault();
    }

    public T? GetSupply<T>() where T : SupplyItem
    {
        return Supplies.OfType<T>().FirstOrDefault();
    }

    /// <summary>
    /// Gets a supply by ID, or creates it using the factory if it doesn't exist
    /// </summary>
    public T GetOrCreateSupply<T>(string supplyId, Func<string, T> factory) where T : SupplyItem
    {
        var existing = GetSupply<T>(supplyId);
        if (existing != null)
            return existing;

        var newSupply = factory(supplyId);
        Supplies.Add(newSupply);
        return newSupply;
    }

    public T GetOrCreateSupply<T>(Func<T> factory) where T : SupplyItem
    {
        var existing = GetSupply<T>();
        if (existing != null)
            return existing;

        var newSupply = factory();
        Supplies.Add(newSupply);
        return newSupply;
    }

    /// <summary>
    /// Adds quantity to a supply, creating it if it doesn't exist
    /// </summary>
    public void AddSupply<T>(string supplyId, double quantity, Func<string, T> factory) where T : SupplyItem
    {
        var supply = GetOrCreateSupply(supplyId, factory);
        supply.Quantity += quantity;
    }

    public void AddSupply<T>(double quantity, Func<T> factory) where T : SupplyItem
    {
        var supply = GetOrCreateSupply(factory);
        supply.Quantity += quantity;
    }

    /// <summary>
    /// Attempts to consume a specific quantity of a supply
    /// Returns true if successful, false if insufficient quantity
    /// </summary>
    public bool TryConsumeSupply<T>(string supplyId, double quantity) where T : SupplyItem
    {
        var supply = GetSupply<T>(supplyId);
        if (supply == null || supply.Quantity < quantity)
            return false;

        supply.Quantity -= quantity;
        return true;
    }

    public bool TryConsumeSupply<T>(double quantity) where T : SupplyItem
    {
        var supply = GetSupply<T>();
        if (supply == null || supply.Quantity < quantity)
            return false;

        supply.Quantity -= quantity;
        return true;
    }

    /// <summary>
    /// Gets the quantity of a specific supply (returns 0 if not found)
    /// </summary>
    public double GetQuantity<T>(string supplyId) where T : SupplyItem
    {
        var supply = GetSupply<T>(supplyId);
        return supply?.Quantity ?? 0.0;
    }

    public double GetQuantity<T>() where T : SupplyItem
    {
        var supply = GetSupply<T>();
        return supply?.Quantity ?? 0.0;
    }

    /// <summary>
    /// Checks if an actor can access this supply pile
    /// </summary>
    public bool CanAccess(ActorId actorId)
    {
        // For now, just return true for shared piles
        return AccessControl == "shared";
    }

    /// <summary>
    /// Provides action candidates from all supply items that implement ISupplyActionCandidate,
    /// and the <c>think_about_supplies</c> environment affordance.
    /// </summary>
    public void AddCandidates(IslandContext ctx, List<ActionCandidate> output)
    {
        if (!CanAccess(ctx.ActorId))
            return;

        foreach (var supply in Supplies)
        {
            if (supply is ISupplyActionCandidate candidate)
                candidate.AddCandidates(ctx, this, output);
        }

        AddThinkAboutSuppliesCandidate(ctx, output);
    }

    /// <summary>
    /// Delegates to each supply item that implements <see cref="ISupplyWorldEventProvider"/>,
    /// allowing those supplies to trigger autonomous world events (such as animal spawning)
    /// that do not require an actor decision context.
    /// Called once per tick by <see cref="IslandDomainPack.TickWorldState"/>.
    /// </summary>
    public void ExecuteWorldEvents(
        IslandWorldState world,
        long currentTick,
        Dictionary<ActorId, ActorState>? mutableActors)
    {
        foreach (var supply in Supplies)
        {
            if (supply is ISupplyWorldEventProvider provider)
                provider.ExecuteWorldEvents(this, world, currentTick, mutableActors);
        }
    }

    private void AddThinkAboutSuppliesCandidate(IslandContext ctx, List<ActionCandidate> output)
    {
        // think_about_supplies is human-only. Skip for non-human actors.
        if (ctx.Actor is not HumanActorState humanActor)
            return;

        var tuning = ctx.TuningProfile.Categories.ThinkAboutSupplies;
        var qualities = ComputeThinkAboutSuppliesQualities(humanActor, ctx.World, tuning, ctx.QualityEffectiveWeight);

        output.Add(new ActionCandidate(
            new ActionSpec(
                new ActionId("think_about_supplies"),
                ActionKind.Wait,
                EmptyActionParameters.Instance,
                Duration.Minutes(10.0, 15.0, ctx.Random),
                NarrationDescription: "think about available supplies"
            ),
            0.08,
            Reason: "Think about supplies",
            EffectHandler: new Action<EffectContext>(effectCtx =>
            {
                if (effectCtx.Actor is HumanActorState humanEffectActor)
                    RecipeDiscoverySystem.TryDiscover(
                        humanEffectActor, effectCtx.World, effectCtx.Rng,
                        DiscoveryTrigger.ThinkAboutSupplies,
                        actorId: effectCtx.ActorId.Value,
                        sourceActionId: "think_about_supplies");
            }),
            Qualities: qualities,
            ActorRequirement: actor => CandidateRequirements.IsHuman(actor) && CandidateRequirements.AliveOnly(actor)
        ));
    }

    /// <summary>
    /// Computes dynamic action qualities for <c>think_about_supplies</c> based on which
    /// recipes the actor can currently discover.  Uses a weighted top-N blend so that a
    /// single highly relevant survival recipe is not drowned out by many mediocre ones.
    /// Falls back to a small default when no meaningful discovery opportunity exists.
    /// When the actor is starving and discoverable recipes would not materially help with
    /// food or safety, qualities are further suppressed so the action loses priority.
    /// </summary>
    private static Dictionary<QualityType, double> ComputeThinkAboutSuppliesQualities(
        HumanActorState actor,
        IslandWorldState world,
        ThinkAboutSuppliesTuning tuning,
        Func<QualityType, double>? effectiveWeight = null)
    {
        var discoverable = new List<(double weight, IReadOnlyDictionary<QualityType, double> qualities)>();

        foreach (var (id, recipe) in IslandRecipeRegistry.All)
        {
            if (recipe.Discovery == null || recipe.Discovery.Trigger != DiscoveryTrigger.ThinkAboutSupplies)
                continue;

            if (actor.KnownRecipeIds.Contains(id))
                continue;

            if (!recipe.Discovery.CanDiscover(actor, world))
                continue;

            discoverable.Add((recipe.Discovery.BaseChance, recipe.Qualities));
        }

        if (discoverable.Count == 0)
        {
            return new Dictionary<QualityType, double>
            {
                [QualityType.Preparation] = tuning.FallbackPreparation,
                [QualityType.Efficiency]  = tuning.FallbackEfficiency
            };
        }

        double RecipeScore((double weight, IReadOnlyDictionary<QualityType, double> qualities) r)
            => effectiveWeight != null
                ? r.weight * r.qualities.Sum(kvp => kvp.Value * effectiveWeight(kvp.Key))
                : r.weight * r.qualities.Values.Sum();

        var topRecipes = discoverable
            .OrderByDescending(RecipeScore)
            .Take(tuning.TopN)
            .ToList();

        var result = new Dictionary<QualityType, double>(Enum.GetValues<QualityType>().Length);
        var totalWeight = topRecipes.Sum(r => r.weight);

        foreach (var (weight, qualities) in topRecipes)
        {
            foreach (var (q, v) in qualities)
            {
                result.TryGetValue(q, out var existing);
                result[q] = existing + v * weight / totalWeight;
            }
        }

        if (actor.Satiety < tuning.StarvationThreshold)
        {
            bool hasSurvivalRelevantRecipe = topRecipes.Any(r =>
                r.qualities.ContainsKey(QualityType.FoodConsumption) ||
                r.qualities.ContainsKey(QualityType.FoodAcquisition) ||
                r.qualities.ContainsKey(QualityType.Safety));

            if (!hasSurvivalRelevantRecipe)
            {
                foreach (var key in result.Keys.ToList())
                    result[key] *= tuning.StarvationSuppression;
            }
        }

        return result;
    }

    public override Dictionary<string, object> SerializeToDict()
    {
        var dict = base.SerializeToDict();
        dict["AccessControl"] = AccessControl;
        dict["Supplies"] = Supplies.Select(s => s.SerializeToDict()).ToList();
        return dict;
    }

    public override void DeserializeFromDict(Dictionary<string, JsonElement> data)
    {
        base.DeserializeFromDict(data);
        AccessControl = data["AccessControl"].GetString()!;

        Supplies.Clear();
        if (data.TryGetValue("Supplies", out var suppliesElement))
        {
            var suppliesList = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(suppliesElement.GetRawText());
            if (suppliesList != null)
            {
                foreach (var supplyData in suppliesList)
                {
                    var type = supplyData["Type"].GetString()!;
                    var id = supplyData["Id"].GetString()!;

                    SupplyItem? supply = type switch
                    {
                        "supply_wood"            => new WoodSupply(id),
                        "supply_fish"            => new FishSupply(id),
                        "supply_cooked_fish"     => new CookedFishSupply(id),
                        "supply_coconut"         => new CoconutSupply(id),
                        "supply_stick"           => new StickSupply(id),
                        "supply_palm_frond"      => new PalmFrondSupply(id),
                        "supply_rocks"           => new RocksSupply(id),
                        "supply_rope"            => new RopeSupply(id),
                        "supply_carcass_scraps"  => new CarcassScrapsSupply(id),
                        "supply_bait"            => new BaitSupply(id),
                        "supply_shells"          => new ShellSupply(id),
                        _ => null
                    };

                    if (supply != null)
                    {
                        supply.DeserializeFromDict(supplyData);
                        Supplies.Add(supply);
                    }
                }
            }
        }
    }
}
