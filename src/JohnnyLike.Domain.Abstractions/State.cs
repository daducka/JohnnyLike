using System.Text.Json;

namespace JohnnyLike.Domain.Abstractions;

public enum ActorStatus
{
    Ready,      // Can receive new task
    Busy        // Executing action
}

public abstract class ActorState
{
    public ActorId Id { get; set; }
    public ActorStatus Status { get; set; }
    public ActionSpec? CurrentAction { get; set; }
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

    /// <summary>
    /// Room index. Maps room ID strings to Room objects.
    /// The engine uses this to scope candidate visibility by actor room.
    /// </summary>
    public Dictionary<string, Room> Rooms { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Returns the room the given item belongs to, or null if the item is not in any room.
    /// </summary>
    public string? GetItemRoomId(string itemId)
    {
        foreach (var room in Rooms.Values)
            if (room.Contains(itemId))
                return room.RoomId;
        return null;
    }

    /// <summary>
    /// Adds an item to the specified room, creating the room if it doesn't exist.
    /// </summary>
    public void AddItemToRoom(string roomId, string itemId)
    {
        if (!Rooms.TryGetValue(roomId, out var room))
        {
            room = new Room(roomId);
            Rooms[roomId] = room;
        }
        room.AddItem(itemId);
    }

    /// <summary>
    /// Removes an item from whatever room it currently belongs to.
    /// </summary>
    public void RemoveItemFromRooms(string itemId)
    {
        foreach (var room in Rooms.Values)
            room.RemoveItem(itemId);
    }

    /// <summary>Returns all world items for engine-level tick orchestration.</summary>
    public abstract IReadOnlyList<WorldItem> GetAllItems();

    public abstract string Serialize();
    public abstract void Deserialize(string json);
}

