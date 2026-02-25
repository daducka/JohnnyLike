namespace JohnnyLike.Narration;

/// <summary>Describes the narrative voice/style used when generating lines.</summary>
public sealed record NarrationTone(string Name, string Description)
{
    public static readonly NarrationTone Documentary = new(
        "Documentary",
        "Calm, observational narrator describing events as they unfold, like a nature documentary.");

    public static readonly NarrationTone Gritty = new(
        "Gritty",
        "Terse, hard-boiled tone with dry wit. Short sentences. No sentimentality.");

    public static readonly NarrationTone Sardonic = new(
        "Sardonic",
        "Wry, darkly comic voice that finds the absurdity in every situation.");
}
