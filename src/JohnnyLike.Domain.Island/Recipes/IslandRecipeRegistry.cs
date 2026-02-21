using JohnnyLike.Domain.Island.Recipes.Definitions;

namespace JohnnyLike.Domain.Island.Recipes;

/// <summary>
/// Central registry of all known recipes. Single source of truth.
/// </summary>
public static class IslandRecipeRegistry
{
    public static readonly IReadOnlyDictionary<string, RecipeDefinition> All =
        new Dictionary<string, RecipeDefinition>
        {
            ["cook_fish"] = CookFish.Define(),
            ["umbrella"]  = Umbrella.Define(),
            ["campfire"]  = Campfire.Define(),
            ["rope"]      = Rope.Define(),
            ["fishing_pole"] = FishingPole.Define(),
        };

    public static RecipeDefinition Get(string id)
        => All[id];
}
