namespace JohnnyLike.Domain.Island.Events;

/// <summary>
/// A sequential world event script consisting of one or more <see cref="WorldEventChapter"/>s.
/// </summary>
public abstract class WorldEventScript
{
    public abstract IReadOnlyList<WorldEventChapter> Chapters { get; }

    public void TryTick(IslandWorldState world, WorldEventProgress progress, long currentTick, Random rng)
    {
        // Only one chapter triggers per tick to keep events sequential and
        // prevent multiple simultaneous world-state changes in a single pass.
        foreach (var chapter in Chapters)
        {
            if (progress.HasTriggered(chapter.Id))
                continue;

            var lastChecked = progress.GetLastCheckedTick(chapter.Id);
            if (currentTick - lastChecked < chapter.CheckIntervalTicks)
                continue;

            progress.MarkChecked(chapter.Id, currentTick);

            var allMet = chapter.Requirements.All(r => r.IsSatisfied(world, progress));
            if (!allMet)
                continue;

            if (rng.NextDouble() >= chapter.TriggerChancePerCheck)
                continue;

            progress.MarkTriggered(chapter.Id, currentTick);

            foreach (var effect in chapter.Effects)
                effect.Apply(world, currentTick);

            return;
        }
    }
}
