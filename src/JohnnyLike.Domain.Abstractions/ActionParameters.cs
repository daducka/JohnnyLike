namespace JohnnyLike.Domain.Abstractions;

/// <summary>
/// Base class for strongly-typed action parameters.
/// </summary>
public abstract record ActionParameters
{
    /// <summary>
    /// Converts the parameters to a dictionary for serialization/legacy compatibility.
    /// </summary>
    public abstract Dictionary<string, object> ToDictionary();
}

/// <summary>
/// Empty parameters for actions that don't require any parameters.
/// </summary>
public record EmptyActionParameters : ActionParameters
{
    public static readonly EmptyActionParameters Instance = new();
    
    public override Dictionary<string, object> ToDictionary() => new();
}

/// <summary>
/// Parameters for joining a scene.
/// </summary>
public record JoinSceneActionParameters(
    string SceneId,
    string Role,
    double Timeout
) : ActionParameters
{
    public override Dictionary<string, object> ToDictionary() => new()
    {
        ["sceneId"] = SceneId,
        ["role"] = Role,
        ["timeout"] = Timeout
    };
}

/// <summary>
/// Generic single-value parameters with a reason/description.
/// </summary>
public record ReasonActionParameters(
    string Reason
) : ActionParameters
{
    public override Dictionary<string, object> ToDictionary() => new()
    {
        ["reason"] = Reason
    };
}
