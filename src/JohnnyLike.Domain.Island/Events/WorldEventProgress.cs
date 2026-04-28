using System.Text.Json;

namespace JohnnyLike.Domain.Island.Events;

/// <summary>
/// Persists the progress of all <see cref="WorldEventScript"/>s on the island.
/// </summary>
public class WorldEventProgress
{
    public HashSet<string> TriggeredChapterIds { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, long> LastCheckedTick { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, long> TriggeredTick { get; } = new(StringComparer.Ordinal);

    public bool HasTriggered(string chapterId) => TriggeredChapterIds.Contains(chapterId);

    public void MarkTriggered(string chapterId, long currentTick)
    {
        TriggeredChapterIds.Add(chapterId);
        TriggeredTick[chapterId] = currentTick;
    }

    public void MarkChecked(string chapterId, long currentTick)
    {
        LastCheckedTick[chapterId] = currentTick;
    }

    public long GetLastCheckedTick(string chapterId) =>
        LastCheckedTick.TryGetValue(chapterId, out var t) ? t : 0L;

    public Dictionary<string, object> SerializeToDict() => new()
    {
        ["TriggeredChapterIds"] = TriggeredChapterIds.ToList(),
        ["LastCheckedTick"]     = LastCheckedTick.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value),
        ["TriggeredTick"]       = TriggeredTick.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value)
    };

    public void DeserializeFromDict(Dictionary<string, JsonElement> data)
    {
        TriggeredChapterIds.Clear();
        LastCheckedTick.Clear();
        TriggeredTick.Clear();

        if (data.TryGetValue("TriggeredChapterIds", out var ids))
        {
            var list = JsonSerializer.Deserialize<List<string>>(ids.GetRawText());
            if (list != null)
                foreach (var id in list)
                    TriggeredChapterIds.Add(id);
        }

        if (data.TryGetValue("LastCheckedTick", out var lct))
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, long>>(lct.GetRawText());
            if (dict != null)
                foreach (var (k, v) in dict)
                    LastCheckedTick[k] = v;
        }

        if (data.TryGetValue("TriggeredTick", out var tt))
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, long>>(tt.GetRawText());
            if (dict != null)
                foreach (var (k, v) in dict)
                    TriggeredTick[k] = v;
        }
    }
}
