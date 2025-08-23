using System;
using System.Collections.Generic;
using Elara.Host.Logging;

namespace Elara.Host.Pipeline;
// TODO: Should handle maximum rambling cut-off (e.g., user keeps talking without pause).
public sealed class ConversationStateMachine
{
    public ConversationMode Mode { get; private set; } = ConversationMode.Quiescent;
    public bool IsSpeaking => Mode == ConversationMode.Speaking;

    public string WakeWord { get; }
    public TimeSpan ProcessingSilence { get; }
    public TimeSpan EndSilence { get; }
    private readonly ILog _log;
    private readonly object _sync = new();

    // Emitted when we switch to Processing due to a brief silence window.
    // Carries the aggregated buffered text as a single prompt string.
    public event Action<string>? PromptReady;

    public DateTimeOffset? ListeningSince { get; private set; }
    public DateTimeOffset? LastHeardAt { get; private set; }
    private readonly List<TranscriptionItem> _buffer = new();

    public ConversationStateMachine(string wakeWord, TimeSpan processingSilence, TimeSpan endSilence, ILog log)
    {
        WakeWord = wakeWord ?? string.Empty;
        ProcessingSilence = processingSilence;
        EndSilence = endSilence;
        _log = log;
        _log.Info("reporting in");
    }

    /// <summary>
    /// Periodic tick to advance silence timers when no new transcription items arrive.
    /// Safe to call from a background timer.
    /// </summary>
    public void Tick(DateTimeOffset nowUtc)
    {
        lock (_sync)
        {
            if (Mode == ConversationMode.Listening)
            {
                EvaluateSilence(nowUtc);
            }
        }
    }

    public void HandleTranscription(DateTimeOffset timestampUtc, string? text, bool meaningful)
    {
        lock (_sync)
        {
            text ??= string.Empty;
            var lower = text.ToLowerInvariant();
            var hasWake = !string.IsNullOrWhiteSpace(WakeWord) && lower.Contains(WakeWord.ToLowerInvariant());

            switch (Mode)
            {
                case ConversationMode.Quiescent:
                    if (hasWake)
                    {
                        TransitionToListening(timestampUtc, reason: $"wake word '{WakeWord}' detected");
                    }
                    break;

                case ConversationMode.Listening:
                    if (meaningful)
                    {
                        LastHeardAt = timestampUtc;
                        _buffer.Add(new TranscriptionItem { TimestampUtc = timestampUtc, Text = text, IsMeaningful = true });
                    }

                    // Evaluate silence windows
                    EvaluateSilence(timestampUtc);
                    break;

                case ConversationMode.Processing:
                case ConversationMode.Speaking:
                    // While processing or speaking, ignore incoming transcriptions entirely.
                    break;
            }
        }
    }

    /// <summary>
    /// Preferred entry: consume a TranscriptionItem and update state/buffer.
    /// </summary>
    public void HandleTranscription(TranscriptionItem item)
    {
        // Wake detection uses raw text
        HandleTranscription(item.TimestampUtc, item.Text, item.IsMeaningful);
    }

    private void EvaluateSilence(DateTimeOffset nowUtc)
    {
        if (Mode != ConversationMode.Listening)
            return;

        var anchor = LastHeardAt ?? ListeningSince ?? nowUtc;
        var silence = nowUtc - anchor;

        if (silence >= ProcessingSilence)
        {
            if (_buffer.Count > 0)
            {
                TransitionToProcessing(reason: $"silence {silence.TotalSeconds:F1}s");
            }
            else
            {
                // No content captured; keep listening and reset anchor to avoid immediate retrigger
                ListeningSince = nowUtc;
                Log("Skip Processing: empty buffer");
            }
        }

        if (silence >= EndSilence)
        {
            TransitionToQuiescent(reason: $"extended silence {silence.TotalSeconds:F1}s");
        }
    }

    private void TransitionToListening(DateTimeOffset nowUtc, string reason)
    {
        Mode = ConversationMode.Listening;
        ListeningSince = nowUtc;
        LastHeardAt = null;
        Log($"-> Listening ({reason})");
    }

    private void TransitionToQuiescent(string reason)
    {
        Mode = ConversationMode.Quiescent;
        ListeningSince = null;
        LastHeardAt = null;
        _buffer.Clear();
        Log($"-> Quiescent ({reason})");
    }

    private void TransitionToProcessing(string reason)
    {
        Mode = ConversationMode.Processing;
        Log($"Listening -> Processing ({reason})");
        if (_buffer.Count > 0)
        {
            var joined = string.Join(" ", _buffer.ConvertAll(i => i.Text));
            _buffer.Clear();
            try { PromptReady?.Invoke(joined); } catch { }
        }
    }

    public void BeginSpeaking()
    {
        lock (_sync)
        {
            if (Mode != ConversationMode.Speaking)
            {
                Mode = ConversationMode.Speaking;
                _buffer.Clear();
                Log("-> Speaking (audio output in progress)");
            }
        }
    }

    public void EndSpeaking()
    {
        lock (_sync)
        {
            if (Mode == ConversationMode.Speaking)
            {
                TransitionToListening(DateTimeOffset.UtcNow, "speech completed");
            }
        }
    }

    public void EndProcessing()
    {
        lock (_sync)
        {
            if (Mode == ConversationMode.Processing)
            {
                TransitionToListening(DateTimeOffset.UtcNow, "processing completed");
            }
        }
    }

    private void Log(string message)
    {
        // File-backed log with source prefix via injected logger
        _log.Info(message);
    }
}
