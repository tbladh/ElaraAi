namespace Elara.Host.Configuration;

/// <summary>
/// Root application configuration bound from appsettings.json.
/// Groups segmenter (VAD), STT, LLM, and TTS settings.
/// </summary>
public sealed class AppConfig
{
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

    /*public static AppConfig Default => new AppConfig
    {
        Segmenter = new SegmenterConfig
        {
            SampleRate = 16000,
            Channels = 1,
            FrameMs = 20,
            EnterRms = 0.015,         // start speech if above this
            EnterActiveRatio = 0.10, // or active ratio above this
            ExitRms = 0.01,          // end speech if below this
            ExitActiveRatio = 0.05,
            EnterConsecutive = 2,    // ~40ms
            ExitConsecutive = 8,     // ~160ms (ensures tail padding)
            PrependPaddingMs = 300,
            AppendPaddingMs = 400,
            MinSegmentMs = 500,
            MaxSegmentMs = 12000,
            ActiveSampleAbsThreshold = 0.02, // per-sample absolute threshold for active ratio calc
            BurstEnterRms = 0.06,    // immediate enter when exceeded (short words)
            BurstWindowMs = 500,
            BurstMinSegmentMs = 300, // allow very short segments when started via burst
            BurstQuietConsecutive = 4,
            // Adaptive thresholds
            UseAdaptiveThresholds = true,
            NoiseFloorAlpha = 0.02,
            NoiseFloorEnterMultiplier = 3.0,
            NoiseFloorExitMultiplier = 1.5,
            // Peak-based burst (handles plosives/very short spikes)
            BurstPeakAbsThreshold = 0.20,
            EnableMetrics = true,
            MetricsIntervalMs = 1000
        },
        Stt = new SttConfig
        {
            Language = "en",
            ModelFile = "ggml-medium.en.bin",
            ModelUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-medium.en.bin"
        },
        LanguageModel = new LanguageModelConfig
        {
            Provider = "ollama",
            BaseUrl = "http://localhost:11434",
            ModelName = "llama3.1:8b",
            SystemPrompt = "You are Ernest, a concise and helpful AI assistant.",
            OutputFilters = Array.Empty<string>()
        },
        TextToSpeech = new TextToSpeechConfig
        {
            Enabled = true,
            Voice = null,
            Rate = 1.0f,
            Pitch = 1.0f
        }
    };*/
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
}
