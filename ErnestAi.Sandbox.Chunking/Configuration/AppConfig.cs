namespace ErnestAi.Sandbox.Chunking.Configuration;

public sealed class AppConfig
{
    public required SegmenterConfig Segmenter { get; init; }

    public static AppConfig Default => new AppConfig
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
        }
    };
}

public sealed class SegmenterConfig
{
    public int SampleRate { get; init; }
    public int Channels { get; init; }
    public int FrameMs { get; init; }

    public double EnterRms { get; init; }
    public double EnterActiveRatio { get; init; }
    public double ExitRms { get; init; }
    public double ExitActiveRatio { get; init; }

    public int EnterConsecutive { get; init; }
    public int ExitConsecutive { get; init; }

    public int PrependPaddingMs { get; init; }
    public int AppendPaddingMs { get; init; }

    public int MinSegmentMs { get; init; }
    public int MaxSegmentMs { get; init; }

    // Active ratio sample threshold
    // A sample is considered "active" if abs(sample) > ActiveSampleAbsThreshold (sample normalized to [-1,1])
    public double ActiveSampleAbsThreshold { get; init; }

    // Burst mode: immediately enter on high RMS and hold a short window to capture short utterances
    public double BurstEnterRms { get; init; }
    public int BurstWindowMs { get; init; }
    public int BurstMinSegmentMs { get; init; }
    public int BurstQuietConsecutive { get; init; }

    // Adaptive thresholds using a running noise floor (RMS)
    public bool UseAdaptiveThresholds { get; init; }
    public double NoiseFloorAlpha { get; init; }
    public double NoiseFloorEnterMultiplier { get; init; }
    public double NoiseFloorExitMultiplier { get; init; }

    // Peak-based burst enter (max abs sample in frame)
    public double BurstPeakAbsThreshold { get; init; }

    // Metrics
    public bool EnableMetrics { get; init; }
    public int MetricsIntervalMs { get; init; }
}
