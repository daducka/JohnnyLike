namespace JohnnyLike.Domain.Island.Candidates;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class IslandCandidateProviderAttribute : Attribute
{
    public int Order { get; }
    public string[] ActionIds { get; }

    public IslandCandidateProviderAttribute(int order = 100, params string[] actionIds)
    {
        Order = order;
        ActionIds = actionIds;
    }
}
