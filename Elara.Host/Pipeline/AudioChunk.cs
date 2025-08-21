using System;
using System.IO;

namespace Elara.Host.Pipeline;

public sealed class AudioChunk : IAsyncDisposable
{
    public long Sequence { get; init; }
    public DateTimeOffset TimestampUtc { get; init; }
    public int DurationMs { get; init; }
    public MemoryStream Stream { get; set; } = new MemoryStream();

    public ValueTask DisposeAsync()
    {
        Stream.Dispose();
        return ValueTask.CompletedTask;
    }
}
