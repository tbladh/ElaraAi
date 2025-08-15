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
            EnterRms = 0.02,         // start speech if above this
            EnterActiveRatio = 0.10, // or active ratio above this
            ExitRms = 0.01,          // end speech if below this
            ExitActiveRatio = 0.05,
            EnterConsecutive = 3,    // ~60ms
            ExitConsecutive = 8,     // ~160ms
            PrependPaddingMs = 200,
            AppendPaddingMs = 300,
            MinSegmentMs = 700,
            MaxSegmentMs = 12000,
            ActiveSampleAbsThreshold = 0.02, // per-sample absolute threshold for active ratio calc
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

    // Metrics
    public bool EnableMetrics { get; init; }
    public int MetricsIntervalMs { get; init; }
}
