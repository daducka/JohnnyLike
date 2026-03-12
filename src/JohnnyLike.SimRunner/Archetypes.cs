namespace JohnnyLike.SimRunner;

public static class Archetypes
{
    public static readonly IReadOnlyDictionary<string, Dictionary<string, object>> All =
        new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Johnny"] = new()
            {
                ["STR"] = 12,
                ["DEX"] = 14,
                ["CON"] = 13,
                ["INT"] = 10,
                ["WIS"] = 11,
                ["CHA"] = 15,
                ["satiety"] = 70.0,
                ["energy"] = 80.0,
                ["morale"] = 60.0
            },
            ["Frank"] = new()
            {
                ["STR"] = 11,
                ["DEX"] = 13,
                ["CON"] = 14,
                ["INT"] = 14,
                ["WIS"] = 15,
                ["CHA"] = 8,
                ["satiety"] = 70.0,
                ["energy"] = 80.0,
                ["morale"] = 60.0
            },
            ["Sawyer"] = new()
            {
                ["STR"] = 13,
                ["DEX"] = 11,
                ["CON"] = 12,
                ["INT"] = 9,
                ["WIS"] = 10,
                ["CHA"] = 16,
                ["satiety"] = 70.0,
                ["energy"] = 80.0,
                ["morale"] = 60.0
            },
            ["Ashley"] = new()
            {
                ["STR"] = 12,
                ["DEX"] = 15,
                ["CON"] = 13,
                ["INT"] = 13,
                ["WIS"] = 11,
                ["CHA"] = 10,
                ["satiety"] = 70.0,
                ["energy"] = 80.0,
                ["morale"] = 60.0
            },
            ["Oscar"] = new()
            {
                ["STR"] = 8,
                ["DEX"] = 9,
                ["CON"] = 10,
                ["INT"] = 15,
                ["WIS"] = 16,
                ["CHA"] = 11,
                ["satiety"] = 70.0,
                ["energy"] = 80.0,
                ["morale"] = 60.0
            },
            ["Elizabeth"] = new()
            {
                ["STR"] = 11,
                ["DEX"] = 12,
                ["CON"] = 15,
                ["INT"] = 11,
                ["WIS"] = 12,
                ["CHA"] = 14,
                ["satiety"] = 70.0,
                ["energy"] = 80.0,
                ["morale"] = 60.0
            }
        };
}
