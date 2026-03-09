using System.Text;

namespace JohnnyLike.SimRunner;

/// <summary>Fans out writes to multiple <see cref="TextWriter"/> destinations simultaneously.</summary>
internal sealed class TeeWriter : TextWriter
{
    private readonly TextWriter[] _writers;

    public TeeWriter(params TextWriter[] writers)
    {
        if (writers.Length == 0)
            throw new ArgumentException("At least one writer must be provided.", nameof(writers));
        _writers = writers;
    }

    public override Encoding Encoding => _writers[0].Encoding;

    public override void Write(char value)
    {
        foreach (var w in _writers) w.Write(value);
    }

    public override void WriteLine(string? value)
    {
        foreach (var w in _writers) w.WriteLine(value);
    }

    public override void WriteLine()
    {
        foreach (var w in _writers) w.WriteLine();
    }

    public override void Flush()
    {
        foreach (var w in _writers) w.Flush();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var w in _writers)
            {
                if (!ReferenceEquals(w, Console.Out))
                    w.Dispose();
            }
        }
        base.Dispose(disposing);
    }
}
