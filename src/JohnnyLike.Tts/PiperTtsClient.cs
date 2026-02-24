using System.Diagnostics;

namespace JohnnyLike.Tts;

/// <summary>
/// Wraps the Piper TTS CLI (piper.exe) to synthesize text to a WAV file.
/// Piper reads text from stdin and writes WAV to stdout (or a file via --output_file).
/// </summary>
public sealed class PiperTtsClient : ITtsClient
{
    private readonly string _piperExePath;
    private readonly string _voiceModelPath;

    public PiperTtsClient(string piperExePath, string voiceModelPath)
    {
        _piperExePath = piperExePath;
        _voiceModelPath = voiceModelPath;
    }

    public async Task SynthesizeToWavAsync(string text, string outputWavPath, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _piperExePath,
            Arguments = $"--model \"{_voiceModelPath}\" --output_file \"{outputWavPath}\"",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        await process.StandardInput.WriteLineAsync(text).ConfigureAwait(false);
        process.StandardInput.Close();

        var stderr = await process.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);

        await process.WaitForExitAsync(ct).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Piper TTS failed (exit {process.ExitCode}): {stderr}");
        }
    }
}
