namespace JohnnyLike.Narration;

public enum NarrationJobKind
{
    Attempt,
    Outcome,
    SummaryOptional
}

/// <summary>A unit of work: build a prompt, call LLM, synthesize TTS, enqueue audio clip.</summary>
public sealed record NarrationJob(
    Guid JobId,
    double PlayAtSimTime,
    double DeadlineSimTime,
    NarrationJobKind Kind,
    string ActorId,
    string Prompt
);
