using System.Diagnostics;

namespace JohnnyLike.Tts;

/// <summary>
/// Wraps the Piper TTS CLI (piper.exe) to synthesize text to a WAV file.
/// Piper reads text from stdin and writes the WAV to the path given by --output_file.
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
            // stdout is NOT redirected: piper writes audio to --output_file, not stdout.
            // Redirecting without draining would deadlock if the pipe buffer fills.
            RedirectStandardOutput = false,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        // Write text then close stdin so piper knows input is done
        // WriteLineAsync ensures a trailing newline, which Piper requires to start processing
        await process.StandardInput.WriteLineAsync(text).ConfigureAwait(false);
        process.StandardInput.Close();

        // Drain stderr concurrently with WaitForExit to avoid pipe-buffer deadlocks
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct).ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Piper TTS failed (exit {process.ExitCode}): {stderr}");
        }
    }
}
