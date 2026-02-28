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
    private readonly TimeSpan _requestTimeout;

    public OllamaLlmClient(
        string model,
        HttpClient? http = null,
        int maxRetries = 3,
        TimeSpan? retryDelay = null,
        TimeSpan? requestTimeout = null)
    {
        _model = model;
        _maxRetries = maxRetries;
        _retryDelay = retryDelay ?? TimeSpan.FromSeconds(1);
        _requestTimeout = requestTimeout ?? TimeSpan.FromSeconds(20);
        _http = http ?? new HttpClient
        {
            BaseAddress = new Uri("http://localhost:11434"),
            Timeout = _requestTimeout
        };
    }

    public async Task<string> GenerateAsync(string prompt, CancellationToken ct = default)
    {
        var request = new OllamaRequest { Model = _model, Prompt = prompt, Stream = false };

        for (int attempt = 0; attempt <= _maxRetries; attempt++)
        {
            try
            {
                using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                requestCts.CancelAfter(_requestTimeout);

                var response = await _http.PostAsJsonAsync("/api/generate", request, requestCts.Token).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<OllamaResponse>(cancellationToken: requestCts.Token).ConfigureAwait(false);
                return result?.Response ?? string.Empty;
            }
            catch (OperationCanceledException)
            {
                if (ct.IsCancellationRequested)
                    throw;
                // Treat request timeout as retryable when caller cancellation was not requested.
                if (attempt < _maxRetries)
                    await Task.Delay(_retryDelay, ct).ConfigureAwait(false);
                else
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
