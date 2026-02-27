using JohnnyLike.Domain.Abstractions;
using JohnnyLike.Domain.Island.Candidates;
using JohnnyLike.Domain.Island.Supply;

namespace JohnnyLike.Domain.Island.Recipes;

/// <summary>
/// A fully data-driven definition of a crafting recipe.
/// Each recipe lives in its own file and returns one of these objects.
/// </summary>
public sealed record RecipeDefinition(
    string Id,
    string DisplayName,

    ActionId CraftActionId,

    string Location,

    long Duration,

    double IntrinsicScore,

    IReadOnlyDictionary<QualityType, double> Qualities,

    Func<IslandContext, bool> CanCraft,

    Func<EffectContext, bool> PreAction,

    Action<EffectContext> Effect,

    RecipeDiscoverySpec? Discovery,

    IReadOnlyList<RecipeSupplyCost>? SupplyCosts = null
)
{
    public bool HasRequiredSupplies(SupplyPile? pile)
        => HasRequiredSupplies(pile, SupplyCosts);

    public bool TryConsumeRequiredSupplies(SupplyPile? pile)
        => TryConsumeRequiredSupplies(pile, SupplyCosts);

    public static bool HasRequiredSupplies(SupplyPile? pile, IReadOnlyList<RecipeSupplyCost>? supplyCosts)
    {
        if (pile == null)
            return false;

        if (supplyCosts == null || supplyCosts.Count == 0)
            return true;

        return supplyCosts.All(cost => cost.AvailableQuantity(pile) >= cost.Quantity);
    }

    public static bool TryConsumeRequiredSupplies(SupplyPile? pile, IReadOnlyList<RecipeSupplyCost>? supplyCosts)
    {
        if (!HasRequiredSupplies(pile, supplyCosts))
            return false;

        if (supplyCosts == null)
            return true;

        foreach (var cost in supplyCosts)
        {
            if (!cost.TryConsume(pile!, cost.Quantity))
                return false;
        }

        return true;
    }
}

public sealed record RecipeSupplyCost(
    string Name,
    double Quantity,
    Func<SupplyPile, double> AvailableQuantity,
    Func<SupplyPile, double, bool> TryConsume)
{
    public static RecipeSupplyCost Of<T>(double quantity, string? name = null) where T : SupplyItem
        => new(
            Name: name ?? typeof(T).Name,
            Quantity: quantity,
            AvailableQuantity: pile => pile.GetQuantity<T>(),
            TryConsume: (pile, amount) => pile.TryConsumeSupply<T>(amount));
}
