namespace JohnnyLike.Domain.Abstractions;

public record SceneRoleSpec(
    string RoleName,
    Func<ActorState, bool> EligibilityPredicate,
    ActionSpec ActionTemplate
);

public record SceneTemplate(
    string SceneType,
    List<SceneRoleSpec> Roles,
    Dictionary<string, object> RequiredResources,
    double JoinWindowSeconds,
    double MaxDurationSeconds,
    Dictionary<string, object> Metadata
);

public enum SceneStatus
{
    Proposed,   // Scene proposed, waiting for actors to commit
    Staging,    // Actors committing, waiting for join window
    Running,    // All actors joined, scene executing
    Complete,   // Scene completed successfully
    Aborted     // Scene failed or timed out
}

public class SceneInstance
{
    public SceneId Id { get; set; }
    public SceneTemplate Template { get; set; } = null!;
    public SceneStatus Status { get; set; }
    public double ProposedTime { get; set; }
    public double? StartTime { get; set; }
    public double? EndTime { get; set; }
    public Dictionary<string, ActorId> RoleAssignments { get; set; } = new();
    public HashSet<ActorId> JoinedActors { get; set; } = new();
    public List<ResourceId> ReservedResources { get; set; } = new();
}
