namespace JohnnyLike.Narration;

public enum NarrationJobKind
{
    Attempt,
    Outcome,
    WorldEvent,
    SummaryOptional
}

/// <summary>A unit of work: build a prompt, call LLM, synthesize TTS, enqueue audio clip.</summary>
/// <param name="SubjectId">
/// Actor name for actor-action jobs; world-object identifier for world-event jobs; <c>null</c> if unknown.
/// </param>
public sealed record NarrationJob(
    Guid JobId,
    double PlayAtSimTime,
    double DeadlineSimTime,
    NarrationJobKind Kind,
    string? SubjectId,
    string Prompt
);
