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
            ["cook_fish"]          = CookFish.Define(),
            ["cook_crab"]          = CookCrab.Define(),
            ["umbrella"]           = Umbrella.Define(),
            ["campfire"]           = Campfire.Define(),
            ["rope"]               = Rope.Define(),
            ["fishing_pole"]       = FishingPole.Define(),
            ["crab_trap"]          = CrabTrap.Define(),
            ["fishing_net"]        = FishingNetRecipe.Define(),
            ["palm_frond_blanket"] = PalmFrondBlanket.Define(),
            ["palm_frond_bed"]     = PalmFrondBed.Define(),
            ["bait"]               = Bait.Define(),
        };

    public static RecipeDefinition Get(string id)
        => All[id];
}
