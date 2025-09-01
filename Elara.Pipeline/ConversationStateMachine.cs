using Elara.Logging;
using Elara.Core;
using Elara.Core.Time;

namespace Elara.Pipeline;
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

    /// <summary>
    /// UTC timestamp when the current <see cref="Mode"/> was entered. Updated on every transition.
    /// </summary>
    public DateTimeOffset ModeEnteredAt { get; private set; }

    // Edge-trigger guard: ensure we only consider Processing once per Listening session
    // (until new meaningful speech arrives). Prevents log spam when buffer is empty.
    private bool _processingConsidered;

    // Emitted when we switch to Processing due to a brief silence window.
    // Carries the aggregated buffered text as a single prompt string.
    /// <summary>
    /// Raised once when transitioning Listening -&gt; Processing, carrying the buffered prompt.
    /// </summary>
    public event Action<string>? PromptReady;

    /// <summary>
    /// Raised on any state change with previous mode, new mode, reason, and timestamp.
    /// </summary>
    public event Action<ConversationMode, ConversationMode, string, DateTimeOffset>? StateChanged;

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
    private readonly ITimeProvider _time;

    public ConversationStateMachine(string wakeWord, TimeSpan processingSilence, TimeSpan endSilence, ILog log, ITimeProvider time)
    {
        WakeWord = wakeWord ?? string.Empty;
        ProcessingSilence = processingSilence;
        EndSilence = endSilence;
        _log = log;
        _time = time;
        ModeEnteredAt = _time.UtcNow;
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
                        // Anchor the listening start to current time to avoid retroactive silence
                        var now = _time.UtcNow;
                        TransitionToListening(now, reason: $"wake word '{WakeWord}' detected");
                        // If the same utterance includes content beyond the wake word, capture it immediately
                        // and preserve the wake word in the buffered text (LLMs handle it and it's polite to include).
                        if (meaningful && !string.IsNullOrWhiteSpace(text))
                        {
                            // Use current time for anchors and buffered item to keep silence calculations consistent
                            LastHeardAt = now;
                            _buffer.Add(new TranscriptionItem { TimestampUtc = now, Text = text, IsMeaningful = true });
                            _processingConsidered = false;
                        }
                    }
                    break;

                case ConversationMode.Listening:
                    if (meaningful)
                    {
                        // Record last speech time and accumulate meaningful text to buffer.
                        // Use wall-clock now for silence anchoring to avoid retroactive long-silence from stale timestamps
                        var now = _time.UtcNow;
                        LastHeardAt = now;
                        // If this is the first meaningful content in this Listening session,
                        // re-anchor ListeningSince to now so ProcessingSilence starts from actual speech
                        if (_buffer.Count == 0)
                        {
                            ListeningSince = now;
                        }
                        _buffer.Add(new TranscriptionItem { TimestampUtc = timestampUtc, Text = text, IsMeaningful = true });
                        // New speech resets processing consideration edge
                        _processingConsidered = false;
                    }

                    // Evaluate silence windows using wall-clock now to avoid stale timestamp effects
                    EvaluateSilence(_time.UtcNow);
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

        // Separate timers for clarity and to avoid anchor mutation side-effects
        var sinceLastMeaningful = nowUtc - (LastHeardAt ?? ListeningSince ?? nowUtc);
        var sinceListeningStart = nowUtc - (ListeningSince ?? nowUtc);

        if (!_processingConsidered && sinceLastMeaningful >= ProcessingSilence)
        {
            if (_buffer.Count > 0)
            {
                // Listening -> Processing: emit buffered prompt once.
                TransitionToProcessing(reason: $"silence {sinceLastMeaningful.TotalSeconds:F1}s");
                _processingConsidered = true;
            }
            else
            {
                // No content captured; keep listening, do not reset anchors to allow EndSilence to accrue
                Log("Skip Processing: empty buffer");
                _processingConsidered = true;
            }
        }

        if (sinceListeningStart >= EndSilence)
        {
            // Listening -> Quiescent on extended silence.
            TransitionToQuiescent(reason: $"extended silence {sinceListeningStart.TotalSeconds:F1}s");
        }
    }

    /// <summary>
    /// Transition to Listening with timestamps adjusted and logging.
    /// </summary>
    private void TransitionToListening(DateTimeOffset nowUtc, string reason)
    {
        var from = Mode;
        Mode = ConversationMode.Listening;
        ListeningSince = nowUtc;
        LastHeardAt = null;
        ModeEnteredAt = nowUtc;
        _processingConsidered = false;
        try { StateChanged?.Invoke(from, Mode, reason, nowUtc); } catch { }
        Log($"-> Listening ({reason})");
    }

    /// <summary>
    /// Transition to Quiescent and clear buffer.
    /// </summary>
    private void TransitionToQuiescent(string reason)
    {
        var from = Mode;
        Mode = ConversationMode.Quiescent;
        ListeningSince = null;
        LastHeardAt = null;
        _buffer.Clear();
        var now = _time.UtcNow;
        ModeEnteredAt = now;
        try { StateChanged?.Invoke(from, Mode, reason, now); } catch { }
        Log($"-> Quiescent ({reason})");
    }

    /// <summary>
    /// Transition to Processing, emit the prompt by joining buffered items, and clear buffer.
    /// </summary>
    private void TransitionToProcessing(string reason)
    {
        var from = Mode;
        Mode = ConversationMode.Processing;
        Log($"Listening -> Processing ({reason})");
        var now = _time.UtcNow;
        ModeEnteredAt = now;
        try { StateChanged?.Invoke(from, Mode, reason, now); } catch { }
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
                var from = Mode;
                Mode = ConversationMode.Speaking;
                _buffer.Clear();
                var now = _time.UtcNow;
                ModeEnteredAt = now;
                try { StateChanged?.Invoke(from, Mode, "begin speaking", now); } catch { }
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
                var now = _time.UtcNow;
                var from = Mode;
                TransitionToListening(now, "speech completed");
                // TransitionToListening already raises StateChanged
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
                var now = _time.UtcNow;
                TransitionToListening(now, "processing completed");
                // TransitionToListening already raises StateChanged
            }
        }
    }

    private void Log(string message)
    {
        // File-backed log with source prefix via injected logger
        _log.Info(message);
    }
}
