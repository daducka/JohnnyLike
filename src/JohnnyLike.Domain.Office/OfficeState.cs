using JohnnyLike.Domain.Abstractions;
using System.Text.Json;

namespace JohnnyLike.Domain.Office;

public class OfficeActorState : ActorState
{
    public double Hunger { get; set; } = 0.0;
    public double Energy { get; set; } = 100.0;
    public double Social { get; set; } = 50.0;
    public string? LastChatRedeem { get; set; }
    public double LastChatRedeemTime { get; set; }

    public override string Serialize()
    {
        return JsonSerializer.Serialize(new
        {
            Id = Id.Value,
            Status,
            CurrentAction = CurrentAction?.Id.Value,
            CurrentScene = CurrentScene?.Value,
            LastDecisionTime,
            Hunger,
            Energy,
            Social,
            LastChatRedeem,
            LastChatRedeemTime
        });
    }

    public override void Deserialize(string json)
    {
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        if (data == null) return;

        Id = new ActorId(data["Id"].GetString()!);
        Status = Enum.Parse<ActorStatus>(data["Status"].GetString()!);
        LastDecisionTime = data["LastDecisionTime"].GetDouble();
        Hunger = data["Hunger"].GetDouble();
        Energy = data["Energy"].GetDouble();
        Social = data["Social"].GetDouble();
        
        if (data.TryGetValue("LastChatRedeem", out var lcr) && lcr.ValueKind == JsonValueKind.String)
        {
            LastChatRedeem = lcr.GetString();
        }
        if (data.TryGetValue("LastChatRedeemTime", out var lcrt))
        {
            LastChatRedeemTime = lcrt.GetDouble();
        }
    }
}

public class OfficeWorldState : WorldState
{
    public Dictionary<string, bool> ResourceAvailability { get; set; } = new()
    {
        ["printer"] = true,
        ["kitchen"] = true,
        ["desk_jim"] = true,
        ["desk_pam"] = true,
        ["conference_room"] = true
    };

    public Dictionary<string, double> LastUsedTime { get; set; } = new();

    public override string Serialize()
    {
        return JsonSerializer.Serialize(new
        {
            ResourceAvailability,
            LastUsedTime
        });
    }

    public override void Deserialize(string json)
    {
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        if (data == null) return;

        if (data.TryGetValue("ResourceAvailability", out var ra))
        {
            ResourceAvailability = JsonSerializer.Deserialize<Dictionary<string, bool>>(ra.GetRawText())!;
        }
        if (data.TryGetValue("LastUsedTime", out var lut))
        {
            LastUsedTime = JsonSerializer.Deserialize<Dictionary<string, double>>(lut.GetRawText())!;
        }
    }
}
