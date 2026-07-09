using JohnnyLike.Domain.Island.Items;

namespace JohnnyLike.Domain.Island.Events;

/// <summary>A single condition that must be satisfied for a world event chapter to trigger.</summary>
public abstract class WorldEventRequirement
{
    public abstract bool IsSatisfied(IslandWorldState world, WorldEventProgress progress);
}

/// <summary>Requires the calendar day count to be at least <see cref="MinDay"/>.</summary>
public sealed class MinDayRequirement : WorldEventRequirement
{
    public required int MinDay { get; init; }

    public override bool IsSatisfied(IslandWorldState world, WorldEventProgress progress)
    {
        var calendar = world.GetItem<CalendarItem>("calendar");
        return calendar != null && calendar.DayCount >= MinDay;
    }
}

/// <summary>Requires a specific chapter to have already triggered.</summary>
public sealed class ChapterTriggeredRequirement : WorldEventRequirement
{
    public required string ChapterId { get; init; }

    public override bool IsSatisfied(IslandWorldState world, WorldEventProgress progress)
        => progress.HasTriggered(ChapterId);
}

/// <summary>Requires the current precipitation band to match <see cref="Required"/>.</summary>
public sealed class WeatherRequirement : WorldEventRequirement
{
    public required PrecipitationBand Required { get; init; }

    public override bool IsSatisfied(IslandWorldState world, WorldEventProgress progress)
    {
        var weather = world.GetItem<WeatherItem>("weather");
        return weather != null && weather.Precipitation == Required;
    }
}

/// <summary>Requires a world item with the given ID to exist.</summary>
public sealed class ItemExistsRequirement : WorldEventRequirement
{
    public required string ItemId { get; init; }

    public override bool IsSatisfied(IslandWorldState world, WorldEventProgress progress)
        => world.WorldItems.Any(i => i.Id == ItemId);
}
