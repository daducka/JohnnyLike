namespace JohnnyLike.Domain.Abstractions;

/// <summary>
/// Represents a quality dimension used to weight candidate actions based on actor state.
/// </summary>
public enum QualityType
{
    Rest,
    FoodConsumption,
    Fun,
    ForwardPlanning,
    Safety
}
