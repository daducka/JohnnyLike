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
    public long LastDecisionTick { get; set; }
    public string CurrentRoomId { get; set; } = "beach";

    public abstract string Serialize();
    public abstract void Deserialize(string json);
}

public abstract class WorldState
{
    /// <summary>
    /// Active tracer for emitting narration beats from within world-tick handlers.
    /// Set by the engine before calling <c>TickWorldState</c> and cleared afterwards.
    /// Defaults to <see cref="NullEventTracer.Instance"/> when no tracer is wired up.
    /// </summary>
    public IEventTracer Tracer { get; set; } = NullEventTracer.Instance;

    public abstract string Serialize();
    public abstract void Deserialize(string json);
}
