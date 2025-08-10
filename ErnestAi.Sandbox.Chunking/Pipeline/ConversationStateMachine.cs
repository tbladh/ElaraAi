using System;

namespace ErnestAi.Sandbox.Chunking;

public sealed class ConversationStateMachine
{
    public ConversationMode Mode { get; private set; } = ConversationMode.Quiescent;
    public bool IsProcessing { get; private set; }

    public string WakeWord { get; }
    public TimeSpan ProcessingSilence { get; }
    public TimeSpan EndSilence { get; }
    private readonly CompactConsole _console;

    public DateTimeOffset? ListeningSince { get; private set; }
    public DateTimeOffset? LastHeardAt { get; private set; }

    public ConversationStateMachine(string wakeWord, TimeSpan processingSilence, TimeSpan endSilence, CompactConsole console)
    {
        WakeWord = wakeWord ?? string.Empty;
        ProcessingSilence = processingSilence;
        EndSilence = endSilence;
        _console = console;
    }

    public void HandleTranscription(DateTimeOffset timestampUtc, string? text, bool meaningful)
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
        Log($"Listening -> Quiescent ({reason})");
    }

    private void Log(string message)
    {
        var stamp = DateTimeOffset.Now.ToLocalTime().ToString("HH:mm:ss");
        _console.WriteStateLine($"[{stamp}] [STATE] {message}");
    }
}
