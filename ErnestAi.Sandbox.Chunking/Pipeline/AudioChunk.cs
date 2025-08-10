using System;
using System.IO;

namespace ErnestAi.Sandbox.Chunking;

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
