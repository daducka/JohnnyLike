namespace JohnnyLike.Llm;

public interface ILlmClient
{
    Task<string> GenerateAsync(string prompt, CancellationToken ct = default);
}
