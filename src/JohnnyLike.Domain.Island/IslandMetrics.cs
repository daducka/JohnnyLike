using System.Text.Json;

namespace JohnnyLike.Domain.Island;

/// <summary>
/// Centralized counters for recipe triggers, debugging, survival studies,
/// pressure-fuzzer analysis, and trace/snapshot output.
/// </summary>
public class IslandMetrics
{
    public int FishCaught { get; set; }
    public int CrabsCaught { get; set; }
    public int CrabTrapChecks { get; set; }
    public int CrabTrapCatches { get; set; }
    public int FishingNetChecks { get; set; }
    public int FishingNetCatches { get; set; }
    public int FoodCooked { get; set; }

    public Dictionary<string, object> SerializeToDict() => new()
    {
        ["FishCaught"]        = FishCaught,
        ["CrabsCaught"]       = CrabsCaught,
        ["CrabTrapChecks"]    = CrabTrapChecks,
        ["CrabTrapCatches"]   = CrabTrapCatches,
        ["FishingNetChecks"]  = FishingNetChecks,
        ["FishingNetCatches"] = FishingNetCatches,
        ["FoodCooked"]        = FoodCooked
    };

    public void DeserializeFromDict(Dictionary<string, JsonElement> data)
    {
        if (data.TryGetValue("FishCaught",        out var fc))  FishCaught        = fc.GetInt32();
        if (data.TryGetValue("CrabsCaught",       out var cc))  CrabsCaught       = cc.GetInt32();
        if (data.TryGetValue("CrabTrapChecks",    out var ctc)) CrabTrapChecks    = ctc.GetInt32();
        if (data.TryGetValue("CrabTrapCatches",   out var ctca))CrabTrapCatches   = ctca.GetInt32();
        if (data.TryGetValue("FishingNetChecks",  out var fnc)) FishingNetChecks  = fnc.GetInt32();
        if (data.TryGetValue("FishingNetCatches", out var fnca))FishingNetCatches = fnca.GetInt32();
        if (data.TryGetValue("FoodCooked",        out var fd))  FoodCooked        = fd.GetInt32();
    }
}
