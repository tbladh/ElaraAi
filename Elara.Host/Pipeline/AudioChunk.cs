using System;
using System.IO;

namespace Elara.Host.Pipeline;

/// <summary>
/// Represents a single WAV-formatted audio segment emitted by the segmenter/streamer.
/// Ownership of <see cref="Stream"/> is transferred to the consumer; disposing the chunk disposes the stream.
/// </summary>
public sealed class AudioChunk : IAsyncDisposable
{
    /// <summary>
    /// Monotonic sequence number assigned at emission time.
    /// </summary>
    public long Sequence { get; init; }

    /// <summary>
    /// UTC timestamp when this chunk was created.
    /// </summary>
    public DateTimeOffset TimestampUtc { get; init; }

    /// <summary>
    /// Approximate duration in milliseconds (not including WAV headers).
    /// </summary>
    public int DurationMs { get; init; }

    /// <summary>
    /// WAV audio payload (16-bit PCM). Position is 0 for consumers.
    /// </summary>
    public MemoryStream Stream { get; set; } = new MemoryStream();

    /// <summary>
    /// Disposes the underlying audio <see cref="Stream"/>.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        Stream.Dispose();
        return ValueTask.CompletedTask;
    }
}
