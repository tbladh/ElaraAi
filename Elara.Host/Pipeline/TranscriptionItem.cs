using System;

namespace Elara.Host.Pipeline;

public sealed class TranscriptionItem
{
    public long Sequence { get; init; }
    public DateTimeOffset TimestampUtc { get; init; }
    public string Text { get; init; } = string.Empty;
    public bool IsMeaningful { get; init; }
    public int WordCount { get; init; }
}
