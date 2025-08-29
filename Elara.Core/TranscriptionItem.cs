using System;

namespace Elara.Core;

/// <summary>
/// Represents a single transcription result produced from an <see cref="AudioChunk"/>.
/// Used by the conversation FSM to buffer prompt text and detect meaningful content.
/// </summary>
public sealed class TranscriptionItem
{
    /// <summary>
    /// Sequence number copied from the originating <see cref="AudioChunk"/>.
    /// </summary>
    public long Sequence { get; init; }

    /// <summary>
    /// Timestamp of the originating audio chunk in UTC.
    /// </summary>
    public DateTimeOffset TimestampUtc { get; init; }

    /// <summary>
    /// Recognized text for this segment. May be empty when gated by silence.
    /// </summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// True when the text passes the minimal meaningfulness heuristic (e.g., non-whitespace, min word count).
    /// </summary>
    public bool IsMeaningful { get; init; }

    /// <summary>
    /// Word count computed via whitespace splitting; used to qualify <see cref="IsMeaningful"/>.
    /// </summary>
    public int WordCount { get; init; }
}
