namespace JohnnyLike.Domain.Island.Candidates;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class IslandCandidateProviderAttribute : Attribute
{
    public int Order { get; }
    public string[] ActionIds { get; }

    /// <summary>
    /// Creates an attribute for an island candidate provider.
    /// </summary>
    /// <param name="order">Provider execution order (lower = earlier)</param>
    /// <param name="actionIds">Action IDs this provider handles effects for. Leave empty if provider doesn't apply effects.</param>
    public IslandCandidateProviderAttribute(int order = 100, params string[] actionIds)
    {
        Order = order;
        ActionIds = actionIds ?? Array.Empty<string>();
    }
}
