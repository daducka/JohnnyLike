using System.Text;

namespace JohnnyLike.Narration;

/// <summary>
/// Builds LLM prompts that request strict JSON output with fields:
///   narration (1-2 sentences)
///   updatedSummary (nullable; 2-4 sentences when requested)
/// </summary>
public sealed class NarrationPromptBuilder
{
    private readonly NarrationTone _tone;
    private string _storySummary;

    public string StorySummary => _storySummary;

    public NarrationPromptBuilder(NarrationTone tone, string initialSummary = "")
    {
        _tone = tone;
        _storySummary = initialSummary;
    }

    public void UpdateSummary(string newSummary) => _storySummary = newSummary;

    public string BuildAttemptPrompt(
        Beat beat,
        CanonicalFacts facts,
        IReadOnlyList<Beat> recentBeats,
        bool requestSummaryUpdate)
    {
        var sb = new StringBuilder();
        AppendSystemInstructions(sb, requestSummaryUpdate);
        sb.AppendLine();
        AppendTone(sb);
        AppendCanonicalFacts(sb, facts);
        AppendActorFacts(sb, facts, beat.ActorId);
        AppendRecentBeats(sb, recentBeats);
        AppendCurrentSummary(sb);
        sb.AppendLine("## Current Event");
        sb.AppendLine($"Actor \"{beat.ActorId}\" is about to: {beat.Subject} " +
                      $"(kind: {beat.ActionKind}) at sim-time {beat.SimTime:F1}.");
        sb.AppendLine("Write the ATTEMPT narration line. Do NOT reveal the outcome.");
        return sb.ToString();
    }

    public string BuildOutcomePrompt(
        Beat beat,
        CanonicalFacts facts,
        IReadOnlyList<Beat> recentBeats,
        bool requestSummaryUpdate)
    {
        var sb = new StringBuilder();
        AppendSystemInstructions(sb, requestSummaryUpdate);
        sb.AppendLine();
        AppendTone(sb);
        AppendCanonicalFacts(sb, facts);
        AppendActorFacts(sb, facts, beat.ActorId);
        AppendRecentBeats(sb, recentBeats);
        AppendCurrentSummary(sb);
        sb.AppendLine("## Current Event");
        var outcomeWord = beat.Success == true ? "succeeded" : "failed";
        sb.AppendLine($"Actor \"{beat.ActorId}\" has {outcomeWord}: {beat.Subject} " +
                      $"(kind: {beat.ActionKind}) at sim-time {beat.SimTime:F1}.");

        // Domain-provided stats — output whatever was snapshotted, with no hardcoded names
        if (beat.StatsAfter != null && beat.StatsAfter.Count > 0)
        {
            var statLine = string.Join(", ", beat.StatsAfter.Select(kv => $"{kv.Key}={kv.Value}"));
            sb.AppendLine($"Actor stats: {statLine}.");
        }

        sb.AppendLine("Write the OUTCOME narration line. Include success/failure and relevant stats if meaningful.");
        return sb.ToString();
    }

    /// <summary>
    /// Builds a prompt for a domain-authored NarrationBeat event.
    /// </summary>
    public string BuildNarrationBeatPrompt(
        Beat beat,
        string domainText,
        CanonicalFacts facts,
        IReadOnlyList<Beat> recentBeats,
        bool requestSummaryUpdate)
    {
        var sb = new StringBuilder();
        AppendSystemInstructions(sb, requestSummaryUpdate);
        sb.AppendLine();
        AppendTone(sb);
        sb.AppendLine($"## Domain: {facts.Domain}");
        AppendCanonicalFacts(sb, facts);
        AppendRecentBeats(sb, recentBeats);
        AppendCurrentSummary(sb);
        sb.AppendLine("## Domain Beat");
        sb.AppendLine(domainText);
        if (beat.ActorId != null)
            sb.AppendLine($"Actor: \"{beat.ActorId}\"");
        if (!string.IsNullOrEmpty(beat.ActionKind))
            sb.AppendLine($"Phase: {beat.ActionKind}");
        sb.AppendLine("Narrate this beat in one or two sentences from the observer's perspective.");
        return sb.ToString();
    }

    /// <summary>
    /// Builds a prompt for a world/environment event that has no actor subject.
    /// </summary>
    public string BuildWorldEventPrompt(
        Beat beat,
        CanonicalFacts facts,
        IReadOnlyList<Beat> recentBeats,
        bool requestSummaryUpdate)
    {
        var sb = new StringBuilder();
        AppendSystemInstructions(sb, requestSummaryUpdate);
        sb.AppendLine();
        AppendTone(sb);
        sb.AppendLine($"## Domain: {facts.Domain}");
        AppendCanonicalFacts(sb, facts);
        AppendRecentBeats(sb, recentBeats);
        AppendCurrentSummary(sb);
        sb.AppendLine("## Current Event");
        sb.AppendLine($"World event \"{beat.EventType}\" at sim-time {beat.SimTime:F1}.");
        if (beat.Subject.Length > 0)
            sb.AppendLine($"Subject: {beat.Subject}");
        sb.AppendLine("Write a narration line describing this world event from the observer's perspective.");
        return sb.ToString();
    }

    private void AppendSystemInstructions(StringBuilder sb, bool requestSummaryUpdate)
    {
        sb.AppendLine("You are a narrative AI for a life-simulation game.");
        sb.AppendLine("Respond ONLY with valid JSON. No markdown. No extra text.");
        sb.AppendLine("JSON schema:");
        sb.AppendLine("  {");
        sb.AppendLine("    \"narration\": \"<1-2 sentences>\",");
        if (requestSummaryUpdate)
            sb.AppendLine("    \"updatedSummary\": \"<2-4 sentences summarising the story so far>\"");
        else
            sb.AppendLine("    \"updatedSummary\": null");
        sb.AppendLine("  }");
        sb.AppendLine("Avoid mentioning numeric values for stats or durations in your narration; describe states qualitatively.");
    }

    private static void AppendCanonicalFacts(StringBuilder sb, CanonicalFacts facts)
    {
        if (!string.IsNullOrEmpty(facts.CurrentDayPhase))
            sb.AppendLine($"It is currently {facts.CurrentDayPhase.ToLowerInvariant()}.");
    }

    private void AppendTone(StringBuilder sb)
    {
        sb.AppendLine($"## Tone: {_tone.Name}");
        sb.AppendLine(_tone.Description);
        sb.AppendLine();
    }

    private static void AppendActorFacts(StringBuilder sb, CanonicalFacts facts, string? actorId)
    {
        sb.AppendLine($"## Domain: {facts.Domain}");
        if (actorId == null) return;

        var actorFacts = facts.GetActor(actorId);
        if (actorFacts == null) return;

        // Output whatever stats the domain chose to expose — no hardcoded field names
        if (actorFacts.Stats.Count > 0)
        {
            var statLine = string.Join(" ", actorFacts.Stats.Select(kv => $"{kv.Key}={kv.Value}"));
            sb.AppendLine($"Actor \"{actorId}\": {statLine}");
        }
        else
        {
            sb.AppendLine($"Actor \"{actorId}\"");
        }
    }

    private static void AppendRecentBeats(StringBuilder sb, IReadOnlyList<Beat> recentBeats)
    {
        if (recentBeats.Count == 0) return;

        // Separate domain beats (NarrationBeat) from action events for clarity
        var domainBeats = recentBeats.Where(b => b.EventType == "NarrationBeat").ToList();
        var actionBeats = recentBeats.Where(b => b.EventType != "NarrationBeat").ToList();

        if (domainBeats.Count > 0)
        {
            sb.AppendLine("## Domain beats (oldest first)");
            foreach (var b in domainBeats)
                sb.AppendLine($"  - {b.Subject}");
            sb.AppendLine();
        }

        if (actionBeats.Count > 0)
        {
            sb.AppendLine("## Recent events (oldest first)");
            foreach (var b in actionBeats)
            {
                var subject = b.ActorId ?? $"[{b.SubjectKind}]";
                var outcome = b.Success.HasValue ? (b.Success.Value ? " [success]" : " [failed]") : "";
                sb.AppendLine($"  t={b.SimTime:F1} {subject} {b.EventType} {b.Subject}{outcome}");
            }
        }
    }

    private void AppendCurrentSummary(StringBuilder sb)
    {
        if (string.IsNullOrWhiteSpace(_storySummary)) return;
        sb.AppendLine("## Story so far");
        sb.AppendLine(_storySummary);
        sb.AppendLine();
    }
}
