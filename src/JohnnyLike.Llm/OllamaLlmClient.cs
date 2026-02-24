using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JohnnyLike.Llm;

/// <summary>
/// Calls the local Ollama HTTP API (http://localhost:11434/api/generate) to generate text.
/// </summary>
public sealed class OllamaLlmClient : ILlmClient
{
    private readonly HttpClient _http;
    private readonly string _model;
    private readonly int _maxRetries;
    private readonly TimeSpan _retryDelay;

    public OllamaLlmClient(string model, HttpClient? http = null, int maxRetries = 3, TimeSpan? retryDelay = null)
    {
        _model = model;
        _http = http ?? new HttpClient { BaseAddress = new Uri("http://localhost:11434") };
        _maxRetries = maxRetries;
        _retryDelay = retryDelay ?? TimeSpan.FromSeconds(1);
    }

    public async Task<string> GenerateAsync(string prompt, CancellationToken ct = default)
    {
        var request = new OllamaRequest { Model = _model, Prompt = prompt, Stream = false };

        for (int attempt = 0; attempt <= _maxRetries; attempt++)
        {
            try
            {
                var response = await _http.PostAsJsonAsync("/api/generate", request, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<OllamaResponse>(cancellationToken: ct).ConfigureAwait(false);
                return result?.Response ?? string.Empty;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception) when (attempt < _maxRetries)
            {
                await Task.Delay(_retryDelay, ct).ConfigureAwait(false);
            }
        }

        return string.Empty;
    }

    private sealed class OllamaRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = string.Empty;

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }
    }

    private sealed class OllamaResponse
    {
        [JsonPropertyName("response")]
        public string? Response { get; set; }
    }
}
