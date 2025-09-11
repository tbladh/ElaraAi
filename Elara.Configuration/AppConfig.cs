namespace Elara.Configuration;

/// <summary>
/// Root application configuration bound from appsettings.json.
/// Groups segmenter (VAD), STT, LLM, and TTS settings.
/// </summary>
public sealed class AppConfig
{
    /// <summary>
    /// Host-level settings (wake word, capacities, timers, session recording, etc.).
    /// </summary>
    public required HostConfig Host { get; init; }

/// <summary>
/// Conversation context and storage configuration.
/// </summary>
public sealed class ContextConfig
{
    /// <summary>Default number of messages to fetch for Last-N strategies.</summary>
    public int LastN { get; init; } = 6;
    /// <summary>Context provider to use: "last-n" (default) or "rag" (future).</summary>
    public string Provider { get; init; } = "last-n";
    /// <summary>Optional explicit storage root; null uses cache root via AppPaths.</summary>
    public string? StorageRoot { get; init; }
    /// <summary>Symmetric encryption key string (hashed with SHA-256 before use). Replace for deployment.</summary>
    public string EncryptionKey { get; init; } = "replace-me-before-deployment";
}

    /// <summary>
    /// Logging settings (directories, file name pattern, levels).
    /// </summary>
    public required LoggingConfig ElaraLogging { get; init; }

    /// <summary>
    /// Audio segmentation (VAD) parameters controlling when speech starts/ends.
    /// </summary>
    public required SegmenterConfig Segmenter { get; init; }
    /// <summary>
    /// Speech-to-text engine configuration (language, model file/url).
    /// </summary>
    public required SttConfig Stt { get; init; }
    /// <summary>
    /// Language model provider and prompt settings.
    /// </summary>
    public required LanguageModelConfig LanguageModel { get; init; }
    /// <summary>
    /// Text-to-speech settings.
    /// </summary>
    public required TextToSpeechConfig TextToSpeech { get; init; }

    /// <summary>
    /// Short randomized phrases for acknowledgements.
    /// </summary>
    public AnnouncementsOptions Announcements { get; init; } = new();

    /// <summary>
    /// Conversation context management configuration (optional).
    /// </summary>
    public ContextConfig Context { get; init; } = new();

}

/// <summary>
/// Parameters for audio segmentation based on RMS/peak/active ratio thresholds.
/// Times are in milliseconds unless specified.
/// </summary>
public sealed class SegmenterConfig
{
    /// <summary>Target sample rate (e.g., 16000).</summary>
    public int SampleRate { get; init; }
    /// <summary>Channel count (1 = mono).</summary>
    public int Channels { get; init; }
    /// <summary>Frame size in milliseconds used by the segmenter.</summary>
    public int FrameMs { get; init; }

    /// <summary>RMS threshold to enter speech.</summary>
    public double EnterRms { get; init; }
    /// <summary>Active ratio threshold to enter speech.</summary>
    public double EnterActiveRatio { get; init; }
    /// <summary>RMS threshold to exit speech.</summary>
    public double ExitRms { get; init; }
    /// <summary>Active ratio threshold to exit speech.</summary>
    public double ExitActiveRatio { get; init; }

    /// <summary>Consecutive frames above enter thresholds required to start speech.</summary>
    public int EnterConsecutive { get; init; }
    /// <summary>Consecutive frames below exit thresholds required to end speech.</summary>
    public int ExitConsecutive { get; init; }

    /// <summary>Padding in ms to prepend before detected start.</summary>
    public int PrependPaddingMs { get; init; }
    /// <summary>Padding in ms to append after detected end.</summary>
    public int AppendPaddingMs { get; init; }

    /// <summary>Minimum segment length in ms.</summary>
    public int MinSegmentMs { get; init; }
    /// <summary>Maximum segment length in ms (safety cap).</summary>
    public int MaxSegmentMs { get; init; }

    // Active ratio sample threshold
    // A sample is considered "active" if abs(sample) > ActiveSampleAbsThreshold (sample normalized to [-1,1])
    /// <summary>Absolute sample threshold used to compute active ratio per frame.</summary>
    public double ActiveSampleAbsThreshold { get; init; }

    // Burst mode: immediately enter on high RMS and hold a short window to capture short utterances
    /// <summary>RMS threshold that triggers immediate enter (burst).</summary>
    public double BurstEnterRms { get; init; }
    /// <summary>Window in ms to hold after a burst enter.</summary>
    public int BurstWindowMs { get; init; }
    /// <summary>Minimum segment length when started via burst.</summary>
    public int BurstMinSegmentMs { get; init; }
    /// <summary>Consecutive quiet frames that end a burst.</summary>
    public int BurstQuietConsecutive { get; init; }

    // Adaptive thresholds using a running noise floor (RMS)
    /// <summary>Enable adaptive thresholds using a running noise floor.</summary>
    public bool UseAdaptiveThresholds { get; init; }
    /// <summary>EMA alpha for noise floor (0..1).</summary>
    public double NoiseFloorAlpha { get; init; }
    /// <summary>Multiplier applied to noise floor for enter.</summary>
    public double NoiseFloorEnterMultiplier { get; init; }
    /// <summary>Multiplier applied to noise floor for exit.</summary>
    public double NoiseFloorExitMultiplier { get; init; }

    // Peak-based burst enter (max abs sample in frame)
    /// <summary>Absolute peak threshold to trigger burst enter.</summary>
    public double BurstPeakAbsThreshold { get; init; }

    // Metrics
    /// <summary>Whether to emit metrics.</summary>
    public bool EnableMetrics { get; init; }
    /// <summary>Interval in ms for metrics emission.</summary>
    public int MetricsIntervalMs { get; init; }
}

/// <summary>
/// Speech-to-text engine configuration.
/// </summary>
public sealed class SttConfig
{
    /// <summary>Target language (e.g., "en").</summary>
    public string Language { get; init; } = "en";
    /// <summary>Local model file name/path. Must exist before startup.</summary>
    public string ModelFile { get; init; } = string.Empty;
    /// <summary>URL to download the model from if needed.</summary>
    public string ModelUrl { get; init; } = string.Empty;
}

/// <summary>
/// Language model provider settings.
/// </summary>
public sealed class LanguageModelConfig
{
    /// <summary>Provider identifier (e.g., "ollama").</summary>
    public string Provider { get; init; } = "ollama";
    /// <summary>Base URL to the provider's API.</summary>
    public string BaseUrl { get; init; } = "http://localhost:11434";
    /// <summary>Model name to use (provider-specific).</summary>
    public string ModelName { get; init; } = string.Empty;
    /// <summary>System prompt prefixed to all prompts.</summary>
    public string SystemPrompt { get; init; } = string.Empty;
    /// <summary>Regex patterns applied to outputs for cleanup.</summary>
    public IEnumerable<string> OutputFilters { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Text-to-speech parameters.
/// </summary>
public sealed class TextToSpeechConfig
{
    /// <summary>Enable or disable TTS.</summary>
    public bool Enabled { get; init; } = true;
    /// <summary>Preferred voice name; null for default.</summary>
    public string? Voice { get; init; }
    /// <summary>Playback rate multiplier (1.0 = normal).</summary>
    public float Rate { get; init; } = 1.0f;
    /// <summary>Pitch multiplier (1.0 = normal).</summary>
    public float Pitch { get; init; } = 1.0f;
    /// <summary>Silent preamble duration in milliseconds to insert before each utterance.</summary>
    public int PreambleMs { get; init; } = 0;
}

/// <summary>
/// Host-level settings for the sandbox wiring.
/// </summary>
public sealed class HostConfig
{
    /// <summary>Wake word used by the conversation FSM.</summary>
    public string WakeWord { get; init; } = "elara";
    /// <summary>Silence duration (seconds) before processing LLM.</summary>
    public int ProcessingSilenceSeconds { get; init; } = 8;
    /// <summary>Silence duration (seconds) to return to quiescent.</summary>
    public int EndSilenceSeconds { get; init; } = 60;
    /// <summary>Capacity for audio chunk channel.</summary>
    public int AudioQueueCapacity { get; init; } = 16;
    /// <summary>Capacity for transcription channel.</summary>
    public int TranscriptionQueueCapacity { get; init; } = 64;
    /// <summary>Interval (ms) for FSM ticker.</summary>
    public int TickerIntervalMs { get; init; } = 200;

    /// <summary>Full-session recording configuration.</summary>
    public SessionRecordingConfig SessionRecording { get; init; } = new();
}

/// <summary>
/// Configuration for optional session recording.
/// </summary>
public sealed class SessionRecordingConfig
{
    /// <summary>If true, prompt on startup; if false, auto-use default scenario path.</summary>
    public bool EnablePrompt { get; init; } = true;
    /// <summary>Default scenario name when not prompted or empty input.</summary>
    public string DefaultScenario { get; init; } = "session";
    /// <summary>Base directory (relative to base) for sample runs.</summary>
    public string BaseDirectory { get; init; } = "SampleRuns";
    /// <summary>CER/WER tolerances emitted to expected.json.</summary>
    public SessionToleranceConfig Tolerances { get; init; } = new();
}

/// <summary>
/// Error tolerances used in expected.json for session runs.
/// </summary>
public sealed class SessionToleranceConfig
{
    public double CER { get; init; } = 0.25;
    public double WER { get; init; } = 0.4;
}

/// <summary>
/// Logging configuration for file/console output.
/// </summary>
public sealed class LoggingConfig
{
    /// <summary>Minimum level: Debug|Info|Warn|Error.</summary>
    public string Level { get; init; } = "Debug";
    /// <summary>Directory for log files (relative or absolute).</summary>
    public string Directory { get; init; } = "Logs";
    /// <summary>File name pattern supporting {date:format} token.</summary>
    public string FileNamePattern { get; init; } = "sandbox-{date:yyyyMMdd}.log";
    /// <summary>Console timestamp format (e.g., "HH:mm:ss ").</summary>
    public string ConsoleTimestampFormat { get; init; } = "HH:mm:ss ";
}
