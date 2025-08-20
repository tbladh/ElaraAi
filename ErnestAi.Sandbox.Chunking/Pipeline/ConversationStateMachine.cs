using System;
using System.Collections.Generic;
using ErnestAi.Sandbox.Chunking.Logging;

namespace ErnestAi.Sandbox.Chunking;
// TODO: Should handle maximum rambling cut-off (e.g., user keeps talking without pause).
public sealed class ConversationStateMachine
{
    public ConversationMode Mode { get; private set; } = ConversationMode.Quiescent;
    public bool IsProcessing { get; private set; } // TODO: This should simply be an event.

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
                        // Any speech resets processing flag
                        var wasProcessing = IsProcessing;
                        LastHeardAt = timestampUtc;
                        if (wasProcessing)
                        {
                            IsProcessing = false;
                            Log($"Processing -> Listening (speech resumed)");
                        }
                    }

                    // Evaluate silence windows
                    EvaluateSilence(timestampUtc);
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

        // Buffer meaningful items only while listening/processing
        if (Mode == ConversationMode.Listening && item.IsMeaningful)
        {
            _buffer.Add(item);
        }
    }

    private void EvaluateSilence(DateTimeOffset nowUtc)
    {
        if (Mode != ConversationMode.Listening)
            return;

        var anchor = LastHeardAt ?? ListeningSince ?? nowUtc;
        var silence = nowUtc - anchor;

        if (!IsProcessing && silence >= ProcessingSilence)
        {
            IsProcessing = true;
            Log($"Listening -> Processing (silence {silence.TotalSeconds:F1}s)");
            if (_buffer.Count > 0)
            {
                var joined = string.Join(" ", _buffer.ConvertAll(i => i.Text));
                _log.Info($"Prompt: {joined}");
                try { PromptReady?.Invoke(joined); } catch { /* observer errors ignored */ }
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
        IsProcessing = false;
        ListeningSince = nowUtc;
        LastHeardAt = null;
        Log($"Quiescent -> Listening ({reason})");
    }

    private void TransitionToQuiescent(string reason)
    {
        Mode = ConversationMode.Quiescent;
        IsProcessing = false;
        ListeningSince = null;
        LastHeardAt = null;
        _buffer.Clear();
        Log($"Listening -> Quiescent ({reason})");
    }

    private void Log(string message)
    {
        // File-backed log with source prefix via injected logger
        _log.Info(message);
    }
}
