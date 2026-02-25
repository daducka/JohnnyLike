namespace JohnnyLike.Tts;

public interface ITtsClient
{
    Task SynthesizeToWavAsync(string text, string outputWavPath, CancellationToken ct = default);
}
