using System.Text.Json;

namespace JohnnyLike.Domain.Abstractions;

public enum ActorStatus
{
    Ready,      // Can receive new task
    Busy,       // Executing action
    Waiting     // Waiting to join scene
}

public abstract class ActorState
{
    public ActorId Id { get; set; }
    public ActorStatus Status { get; set; }
    public ActionSpec? CurrentAction { get; set; }
    public SceneId? CurrentScene { get; set; }
    public double LastDecisionTime { get; set; }

    public abstract string Serialize();
    public abstract void Deserialize(string json);
}

public abstract class WorldState
{
    public abstract string Serialize();
    public abstract void Deserialize(string json);
}
