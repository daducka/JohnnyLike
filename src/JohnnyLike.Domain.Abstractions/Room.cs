namespace JohnnyLike.Domain.Abstractions;

/// <summary>
/// A logical spatial partition of the world. Rooms own item membership — world items
/// do not know what room they belong to. Candidate generation is scoped to the actor's
/// current room.
/// </summary>
public class Room
{
    public string RoomId { get; }
    private readonly HashSet<string> _itemIds = new(StringComparer.Ordinal);

    public Room(string roomId)
    {
        RoomId = roomId;
    }

    /// <summary>Adds an item to this room by its ID.</summary>
    public void AddItem(string itemId) => _itemIds.Add(itemId);

    /// <summary>Removes an item from this room.</summary>
    public bool RemoveItem(string itemId) => _itemIds.Remove(itemId);

    /// <summary>Returns true if the item with the given ID belongs to this room.</summary>
    public bool Contains(string itemId) => _itemIds.Contains(itemId);

    /// <summary>All item IDs currently in this room (stable sorted for determinism).</summary>
    public IReadOnlyCollection<string> ItemIds => _itemIds
        .OrderBy(id => id, StringComparer.Ordinal)
        .ToList();
}
