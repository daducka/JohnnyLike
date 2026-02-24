using System.Text.Json;
using System.Text.Json.Serialization;

namespace JohnnyLike.Narration;

/// <summary>Result of parsing the LLM JSON response.</summary>
public sealed record NarrationResponse(string Narration, string? UpdatedSummary);

/// <summary>
/// Parses the strict JSON response from the LLM.
/// Falls back to a deterministic template string on any parse failure.
/// </summary>
public static class NarrationResponseParser
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static NarrationResponse Parse(string llmOutput, NarrationJob job)
    {
        if (!string.IsNullOrWhiteSpace(llmOutput))
        {
            // Attempt to extract JSON even if surrounded by markdown fences
            var json = ExtractJson(llmOutput);
            if (json != null)
            {
                try
                {
                    var dto = JsonSerializer.Deserialize<LlmResponseDto>(json, _jsonOptions);
                    if (dto != null && !string.IsNullOrWhiteSpace(dto.Narration))
                        return new NarrationResponse(dto.Narration.Trim(), dto.UpdatedSummary?.Trim());
                }
                catch (JsonException)
                {
                    // fall through to fallback
                }
            }
        }

        return Fallback(job);
    }

    private static NarrationResponse Fallback(NarrationJob job)
    {
        var subject = job.SubjectId ?? "Something";
        var text = job.Kind switch
        {
            NarrationJobKind.Attempt =>
                $"{subject} prepares to attempt an action.",
            NarrationJobKind.Outcome =>
                $"{subject} finishes their action.",
            NarrationJobKind.WorldEvent =>
                "Something stirs in the world.",
            _ => $"{subject} does something."
        };
        return new NarrationResponse(text, null);
    }

    private static string? ExtractJson(string raw)
    {
        // String-aware balanced-brace scanner.
        // Skips over characters inside quoted strings (including escaped quotes)
        // so that braces within string values do not affect brace depth.
        var start = raw.IndexOf('{');
        if (start < 0) return null;

        int depth = 0;
        bool inString = false;
        bool escaped = false;

        for (int i = start; i < raw.Length; i++)
        {
            var c = raw[i];

            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (c == '\\' && inString)
            {
                escaped = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString) continue;

            if (c == '{') depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                    return raw[start..(i + 1)];
            }
        }

        return null;
    }

    private sealed class LlmResponseDto
    {
        [JsonPropertyName("narration")]
        public string? Narration { get; set; }

        [JsonPropertyName("updatedSummary")]
        public string? UpdatedSummary { get; set; }
    }
}
