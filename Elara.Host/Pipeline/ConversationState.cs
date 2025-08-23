namespace Elara.Host.Pipeline;

/// <summary>
/// Conversation modes for the state machine.
/// </summary>
public enum ConversationMode
{
    /// <summary>
    /// Idle state; waiting for the wake word.
    /// </summary>
    Quiescent = 0,
    /// <summary>
    /// Actively capturing and buffering meaningful transcriptions.
    /// </summary>
    Listening = 1,
    /// <summary>
    /// User has paused briefly; the buffered text is emitted as a prompt.
    /// Incoming transcriptions are ignored while processing.
    /// </summary>
    Processing = 2,
    /// <summary>
    /// System is speaking TTS output. Incoming transcriptions are ignored.
    /// </summary>
    Speaking = 3
}
