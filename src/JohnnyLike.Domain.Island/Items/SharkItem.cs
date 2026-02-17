using JohnnyLike.Domain.Abstractions;
using System.Text.Json;

namespace JohnnyLike.Domain.Island.Items;

public class SharkItem : MaintainableWorldItem
{
    public double ExpiresAt { get; set; } = 0.0;
    private bool _shouldRemove = false;

    public SharkItem(string id = "shark") 
        : base(id, "shark", baseDecayPerSecond: 0.0)
    {
    }

    public override void Tick(double dtSeconds, IslandWorldState world)
    {
        base.Tick(dtSeconds, world);
        
        // Mark for removal when expired (will be removed after tick cycle)
        if (world.CurrentTime >= ExpiresAt)
        {
            _shouldRemove = true;
        }
    }

    public bool ShouldRemove() => _shouldRemove;

    public override Dictionary<string, object> SerializeToDict()
    {
        var dict = base.SerializeToDict();
        dict["ExpiresAt"] = ExpiresAt;
        return dict;
    }

    public override void DeserializeFromDict(Dictionary<string, JsonElement> data)
    {
        base.DeserializeFromDict(data);
        ExpiresAt = data["ExpiresAt"].GetDouble();
    }
}
