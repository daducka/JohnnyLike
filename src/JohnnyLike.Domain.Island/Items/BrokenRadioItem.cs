using System.Text.Json;
using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Domain.Island.Items;

/// <summary>
/// A broken radio found in wreckage. Serves as a placeholder world item that can be found
/// during loot investigation. Full radio mechanics are out of scope for this feature.
/// </summary>
public class BrokenRadioItem : WorldItem
{
    public bool IsRepaired { get; set; } = false;

    public BrokenRadioItem(string id = "broken_radio") : base(id, "broken_radio") { }

    public override Dictionary<string, object> SerializeToDict()
    {
        var dict = base.SerializeToDict();
        dict["IsRepaired"] = IsRepaired;
        return dict;
    }

    public override void DeserializeFromDict(Dictionary<string, JsonElement> data)
    {
        base.DeserializeFromDict(data);
        if (data.TryGetValue("IsRepaired", out var r))
            IsRepaired = r.GetBoolean();
    }
}
