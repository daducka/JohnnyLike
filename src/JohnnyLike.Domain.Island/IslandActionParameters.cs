using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Domain.Island;

/// <summary>
/// Parameters for location-based actions without skill checks.
/// </summary>
public record LocationActionParameters(
    string Location
) : ActionParameters
{
    public override Dictionary<string, object> ToDictionary() => new()
    {
        ["location"] = Location
    };
}

/// <summary>
/// Parameters for emote actions.
/// </summary>
public record EmoteActionParameters(
    string EmoteType,
    string? Name = null,
    string? Location = null
) : ActionParameters
{
    public override Dictionary<string, object> ToDictionary()
    {
        var dict = new Dictionary<string, object>();
        
        if (Name != null)
            dict["name"] = Name;
        if (EmoteType != null)
            dict["emote"] = EmoteType;
        if (Location != null)
            dict["location"] = Location;
            
        return dict;
    }
}
