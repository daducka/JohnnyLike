namespace JohnnyLike.Domain.Island;

/// <summary>
/// Central registry of all known island recipes.
/// </summary>
public static class RecipeRegistry
{
    public static readonly Dictionary<string, IIslandRecipe> Recipes =
        new(StringComparer.Ordinal)
        {
            ["cook_fish"] = new Recipes.CookFishRecipe()
        };
}
