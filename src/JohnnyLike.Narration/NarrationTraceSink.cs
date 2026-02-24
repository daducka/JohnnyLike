using System.Threading.Channels;
using JohnnyLike.Domain.Abstractions;

namespace JohnnyLike.Narration;

/// <summary>
/// <see cref="ITraceSink"/> that fans each recorded event to an in-memory log
/// and an async <see cref="ChannelReader{T}"/> so the narration pipeline can
/// consume events without polling.
/// </summary>
public sealed class NarrationTraceSink : ITraceSink
{
    private readonly List<TraceEvent> _all = new();
    private readonly Channel<TraceEvent> _channel =
        Channel.CreateUnbounded<TraceEvent>(new UnboundedChannelOptions { SingleReader = true });

    /// <summary>
    /// Async reader for the narration pipeline.  Use
    /// <c>await foreach (var evt in sink.Events.ReadAllAsync(ct))</c>
    /// to receive events without busy-waiting.
    /// </summary>
    public ChannelReader<TraceEvent> Events => _channel.Reader;

    public void Record(TraceEvent evt)
    {
        lock (_all) _all.Add(evt);
        _channel.Writer.TryWrite(evt); // always succeeds for unbounded channels
    }

    public List<TraceEvent> GetEvents() { lock (_all) return new List<TraceEvent>(_all); }

    public void Clear() { lock (_all) _all.Clear(); }

    /// <summary>
    /// Signals that no more events will be written.
    /// Call after the simulation completes so downstream pipeline tasks can finish cleanly.
    /// </summary>
    public void CompleteAdding() => _channel.Writer.TryComplete();
}
