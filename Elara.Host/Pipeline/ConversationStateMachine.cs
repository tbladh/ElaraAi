using System;
using System.Collections.Generic;
using Elara.Host.Logging;

namespace Elara.Host.Pipeline;
/// <summary>
/// Coordinates the conversation flow between Quiescent, Listening, Processing, and Speaking.
/// Keeps responsibilities limited to state transitions and emitting a prompt when entering Processing.
/// While in Processing or Speaking, incoming transcriptions are ignored.
/// </summary>
// TODO: Should handle maximum rambling cut-off (e.g., user keeps talking without pause).
public sealed class ConversationStateMachine
{
    /// <summary>
    /// Current conversation mode. Transitions are driven by wake word, silence timers,
    /// and explicit calls from the host (e.g., BeginSpeaking/EndSpeaking).
    /// </summary>
    public ConversationMode Mode { get; private set; } = ConversationMode.Quiescent;
    /// <summary>
    /// Convenience flag to indicate when the system is in <see cref="ConversationMode.Speaking"/>.
    /// </summary>
    public bool IsSpeaking => Mode == ConversationMode.Speaking;

    /// <summary>
    /// Wake word that moves the FSM from Quiescent to Listening.
    /// </summary>
    public string WakeWord { get; }
    /// <summary>
    /// Silence duration after which we enter Processing from Listening.
    /// </summary>
    public TimeSpan ProcessingSilence { get; }
    /// <summary>
    /// Extended silence duration after which we return to Quiescent from Listening.
    /// </summary>
    public TimeSpan EndSilence { get; }
    private readonly ILog _log;
    private readonly object _sync = new();

    // Emitted when we switch to Processing due to a brief silence window.
    // Carries the aggregated buffered text as a single prompt string.
    /// <summary>
    /// Raised once when transitioning Listening -&gt; Processing, carrying the buffered prompt.
    /// </summary>
    public event Action<string>? PromptReady;

    /// <summary>
    /// UTC timestamp when Listening started, if any.
    /// </summary>
    public DateTimeOffset? ListeningSince { get; private set; }
    /// <summary>
    /// UTC timestamp of the last meaningful transcription while Listening.
    /// </summary>
    public DateTimeOffset? LastHeardAt { get; private set; }
    private readonly List<TranscriptionItem> _buffer = new();

    /// <summary>
    /// Construct a new <see cref="ConversationStateMachine"/> with wake word and silence parameters.
    /// </summary>
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
            // Only Listening uses the silence windows for transitions.
            if (Mode == ConversationMode.Listening)
            {
                EvaluateSilence(nowUtc);
            }
        }
    }

    /// <summary>
    /// Handle a transcription event by text components.
    /// </summary>
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
                    // Wake word moves Quiescent -> Listening.
                    if (hasWake)
                    {
                        TransitionToListening(timestampUtc, reason: $"wake word '{WakeWord}' detected");
                    }
                    break;

                case ConversationMode.Listening:
                    if (meaningful)
                    {
                        // Record last speech time and accumulate meaningful text to buffer.
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
                // Listening -> Processing: emit buffered prompt once.
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
            // Listening -> Quiescent on extended silence.
            TransitionToQuiescent(reason: $"extended silence {silence.TotalSeconds:F1}s");
        }
    }

    /// <summary>
    /// Transition to Listening with timestamps adjusted and logging.
    /// </summary>
    private void TransitionToListening(DateTimeOffset nowUtc, string reason)
    {
        Mode = ConversationMode.Listening;
        ListeningSince = nowUtc;
        LastHeardAt = null;
        Log($"-> Listening ({reason})");
    }

    /// <summary>
    /// Transition to Quiescent and clear buffer.
    /// </summary>
    private void TransitionToQuiescent(string reason)
    {
        Mode = ConversationMode.Quiescent;
        ListeningSince = null;
        LastHeardAt = null;
        _buffer.Clear();
        Log($"-> Quiescent ({reason})");
    }

    /// <summary>
    /// Transition to Processing, emit the prompt by joining buffered items, and clear buffer.
    /// </summary>
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

    /// <summary>
    /// Enter Speaking state. The host should call this immediately before starting audio output.
    /// Incoming transcriptions are ignored while speaking.
    /// </summary>
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

    /// <summary>
    /// Exit Speaking and return to Listening to await user input.
    /// </summary>
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

    /// <summary>
    /// Exit Processing and return to Listening (used when there is no TTS, or after errors).
    /// </summary>
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
