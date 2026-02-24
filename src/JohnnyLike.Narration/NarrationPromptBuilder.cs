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
        string actorId,
        Beat beat,
        CanonicalFacts facts,
        IReadOnlyList<Beat> recentBeats,
        bool requestSummaryUpdate)
    {
        var sb = new StringBuilder();
        AppendSystemInstructions(sb, requestSummaryUpdate);
        sb.AppendLine();
        AppendTone(sb);
        AppendFacts(sb, facts, actorId);
        AppendRecentBeats(sb, recentBeats);
        AppendCurrentSummary(sb);
        sb.AppendLine("## Current Event");
        sb.AppendLine($"Actor \"{actorId}\" is about to attempt action \"{beat.ActionId}\" (kind: {beat.ActionKind}) at sim-time {beat.SimTime:F1}.");
        sb.AppendLine("Write the ATTEMPT narration line. Do NOT reveal the outcome.");
        return sb.ToString();
    }

    public string BuildOutcomePrompt(
        string actorId,
        Beat beat,
        CanonicalFacts facts,
        IReadOnlyList<Beat> recentBeats,
        bool requestSummaryUpdate)
    {
        var sb = new StringBuilder();
        AppendSystemInstructions(sb, requestSummaryUpdate);
        sb.AppendLine();
        AppendTone(sb);
        AppendFacts(sb, facts, actorId);
        AppendRecentBeats(sb, recentBeats);
        AppendCurrentSummary(sb);
        sb.AppendLine("## Current Event");
        var outcomeWord = beat.Success == true ? "succeeded" : "failed";
        sb.AppendLine($"Actor \"{actorId}\" has {outcomeWord} at action \"{beat.ActionId}\" (kind: {beat.ActionKind}) at sim-time {beat.SimTime:F1}.");

        var actorFacts = facts.GetActor(actorId);
        if (actorFacts != null)
        {
            var stats = new List<string>();
            if (actorFacts.Satiety.HasValue) stats.Add($"satiety={actorFacts.Satiety.Value:F0}");
            if (actorFacts.Energy.HasValue) stats.Add($"energy={actorFacts.Energy.Value:F0}");
            if (actorFacts.Morale.HasValue) stats.Add($"morale={actorFacts.Morale.Value:F0}");
            if (stats.Count > 0)
                sb.AppendLine($"Actor stats: {string.Join(", ", stats)}.");
        }

        sb.AppendLine("Write the OUTCOME narration line. Include success/failure and relevant stats if meaningful.");
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
    }

    private void AppendTone(StringBuilder sb)
    {
        sb.AppendLine($"## Tone: {_tone.Name}");
        sb.AppendLine(_tone.Description);
        sb.AppendLine();
    }

    private static void AppendFacts(StringBuilder sb, CanonicalFacts facts, string focusActorId)
    {
        sb.AppendLine($"## Domain: {facts.Domain}");
        var actorFacts = facts.GetActor(focusActorId);
        if (actorFacts != null)
        {
            sb.Append($"Actor \"{focusActorId}\":");
            if (actorFacts.Satiety.HasValue) sb.Append($" satiety={actorFacts.Satiety.Value:F0}");
            if (actorFacts.Energy.HasValue) sb.Append($" energy={actorFacts.Energy.Value:F0}");
            if (actorFacts.Morale.HasValue) sb.Append($" morale={actorFacts.Morale.Value:F0}");
            sb.AppendLine();
        }
    }

    private static void AppendRecentBeats(StringBuilder sb, IReadOnlyList<Beat> recentBeats)
    {
        if (recentBeats.Count == 0) return;
        sb.AppendLine("## Recent events (oldest first)");
        foreach (var b in recentBeats)
        {
            var outcome = b.Success.HasValue ? (b.Success.Value ? " [success]" : " [failed]") : "";
            sb.AppendLine($"  t={b.SimTime:F1} {b.ActorId} {b.EventType} {b.ActionId}{outcome}");
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
