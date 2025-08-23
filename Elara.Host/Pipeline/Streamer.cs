using System.Buffers;
using System.Globalization;
using System.Threading.Channels;
using NAudio.Utils;
using NAudio.Wave;
using Elara.Host.Logging;
using Elara.Host.Configuration;
using Elara.Host.Core.Interfaces;

namespace Elara.Host.Pipeline;

/// <summary>
/// Continuous audio streamer with simple VAD-based segmentation.
/// Emits AudioChunk items at speech boundaries with pre/post padding.
/// </summary>
public sealed class Streamer
{
    private readonly IAudioProcessor _audio;
    private readonly ChannelWriter<AudioChunk> _writer;
    private readonly SegmenterConfig _cfg;
    private readonly ILog _log;
    private WaveFileWriter? _sessionWriter; // optional full-session sink

    // Metrics accumulators
    private long _lastMetricsTick;
    private double _sumRms;
    private double _sumActiveRatio;
    private int _metricsFrames;
    private double _noiseFloorRms; // adaptive noise floor

    /// <summary>
    /// Creates a new streamer that reads raw audio from <see cref="IAudioProcessor"/> and writes WAV segments to a channel.
    /// Segmentation is driven by RMS and active sample ratio thresholds in <see cref="SegmenterConfig"/>.
    /// </summary>
    public Streamer(IAudioProcessor audio, ChannelWriter<AudioChunk> writer, SegmenterConfig cfg, ILog log)
    {
        _audio = audio;
        _writer = writer;
        _cfg = cfg;
        _log = log;
        _log.Info("reporting in");
    }

    /// <summary>
    /// Enable full-session recording by teeing raw input buffers into the provided writer.
    /// Caller owns the file path; Streamer will dispose the writer on completion.
    /// </summary>
    public void SetSessionWriter(WaveFileWriter writer)
    {
        _sessionWriter = writer;
    }

    /// <summary>
    /// Main loop: captures audio, assembles fixed-size frames, performs VAD/burst logic, and emits WAV segments to the channel.
    /// Completes the writer when the audio stream ends or cancellation is requested.
    /// </summary>
    public async Task RunAsync(CancellationToken token)
    {
        var fmt = new WaveFormat(_cfg.SampleRate, _cfg.Channels);
        int bytesPerMs = fmt.AverageBytesPerSecond / 1000; // 32 at 16kHz mono 16-bit
        int frameBytes = _cfg.FrameMs * bytesPerMs;        // 20ms -> 640 bytes

        // Ring buffer for pre-speech padding
        int preFrames = Math.Max(0, _cfg.PrependPaddingMs / _cfg.FrameMs);
        var preRing = new Queue<byte[]>(preFrames);

        // Accumulators
        var segmentBuffers = new List<byte[]>(capacity: 1024);
        long seq = 0;

        // State
        bool inSpeech = false;
        int enterCount = 0;
        int exitCount = 0;
        long burstHoldUntil = 0; // Environment.TickCount64 deadline to hold speech state
        bool inBurst = false;     // indicates we entered via burst
        int burstQuietCount = 0;  // consecutive quiet frames after hold for burst end

        // Frame assembly from variable-size input buffers
        byte[] carry = Array.Empty<byte>();

        await foreach (var buffer in _audio.GetAudioStreamAsync(token).ConfigureAwait(false))
        {
            if (token.IsCancellationRequested) break;

            // Tee raw buffers to session recorder if enabled
            if (_sessionWriter != null)
            {
                await _sessionWriter.WriteAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
            }

            // Append to carry and slice into fixed-size frames
            var tmp = ArrayPool<byte>.Shared.Rent(carry.Length + buffer.Length);
            try
            {
                Buffer.BlockCopy(carry, 0, tmp, 0, carry.Length);
                Buffer.BlockCopy(buffer, 0, tmp, carry.Length, buffer.Length);
                int total = carry.Length + buffer.Length;

                int offset = 0;
                while (total - offset >= frameBytes)
                {
                    var frame = new byte[frameBytes];
                    Buffer.BlockCopy(tmp, offset, frame, 0, frameBytes);
                    offset += frameBytes;

                    AnalyzeFrame(frame, fmt, out double rms, out double activeRatio, out double peakAbs);
                    // Metrics accumulation
                    if (_cfg.EnableMetrics)
                    {
                        _sumRms += rms;
                        _sumActiveRatio += activeRatio;
                        _metricsFrames++;
                    }

                    // Adaptive noise floor update when not in speech
                    if (_cfg.UseAdaptiveThresholds && !inSpeech)
                    {
                        double a = Math.Clamp(_cfg.NoiseFloorAlpha, 0.0001, 1.0);
                        _noiseFloorRms = (1 - a) * _noiseFloorRms + a * rms;
                    }

                    // Current thresholds (adaptive if enabled)
                    double enterRms = _cfg.UseAdaptiveThresholds
                        ? Math.Max(_cfg.EnterRms, _noiseFloorRms * _cfg.NoiseFloorEnterMultiplier)
                        : _cfg.EnterRms;
                    double exitRms = _cfg.UseAdaptiveThresholds
                        ? Math.Max(_cfg.ExitRms, _noiseFloorRms * _cfg.NoiseFloorExitMultiplier)
                        : _cfg.ExitRms;
                    double enterAct = _cfg.EnterActiveRatio;
                    double exitAct = _cfg.ExitActiveRatio;

                    if (!inSpeech)
                    {
                        // Maintain pre-speech ring
                        if (preFrames > 0)
                        {
                            if (preRing.Count == preFrames) preRing.Dequeue();
                            preRing.Enqueue(frame);
                        }

                        var nowTick = Environment.TickCount64;
                        bool burstEnter = _cfg.BurstEnterRms > 0 && rms >= _cfg.BurstEnterRms
                                          || _cfg.BurstPeakAbsThreshold > 0 && peakAbs >= _cfg.BurstPeakAbsThreshold;
                        if (burstEnter || IsEnter(rms, activeRatio, ref enterCount, enterRms, enterAct))
                        {
                            inSpeech = true;
                            exitCount = 0;
                            inBurst = burstEnter;
                            burstQuietCount = 0;
                            // Hold speech for at least BurstWindowMs to capture very short utterances
                            if (_cfg.BurstWindowMs > 0)
                                burstHoldUntil = nowTick + _cfg.BurstWindowMs;
                            // start segment with pre-padding
                            segmentBuffers.Clear();
                            foreach (var f in preRing)
                                segmentBuffers.Add(f);
                            segmentBuffers.Add(frame);
                        }
                    }
                    else
                    {
                        segmentBuffers.Add(frame);
                        var nowTick2 = Environment.TickCount64;
                        bool hold = burstHoldUntil != 0 && nowTick2 < burstHoldUntil;

                        // Track quiet frames for burst end once hold expires
                        if (inBurst && !hold)
                        {
                            if (rms <= exitRms && activeRatio <= exitAct)
                                burstQuietCount++;
                            else
                                burstQuietCount = 0;
                        }

                        // Normal VAD exit path
                        if (!hold && IsExit(rms, activeRatio, ref exitCount, exitRms, exitAct))
                        {
                            // Apply implicit post-padding: we already buffered exitCount frames below threshold
                            var ms = segmentBuffers.Count * _cfg.FrameMs;
                            if (ms >= _cfg.MinSegmentMs)
                            {
                                await EmitSegmentAsync(segmentBuffers, fmt, seq++, token, reason: "vad").ConfigureAwait(false);
                            }
                            // Reset state
                            inSpeech = false;
                            enterCount = 0;
                            exitCount = 0;
                            burstHoldUntil = 0;
                            inBurst = false;
                            burstQuietCount = 0;
                            segmentBuffers.Clear();
                            preRing.Clear();
                        }
                        else
                        {
                            // Burst-mode end: after hold window, if we observed enough quiet frames, emit even if VAD not yet met
                            if (inBurst && !hold)
                            {
                                var msDur = segmentBuffers.Count * _cfg.FrameMs;
                                int burstMin = _cfg.BurstMinSegmentMs > 0 ? _cfg.BurstMinSegmentMs : _cfg.MinSegmentMs;
                                if (burstQuietCount >= Math.Max(1, _cfg.BurstQuietConsecutive) && msDur >= burstMin)
                                {
                                    await EmitSegmentAsync(segmentBuffers, fmt, seq++, token, reason: "burst").ConfigureAwait(false);
                                    inSpeech = false;
                                    enterCount = 0;
                                    exitCount = 0;
                                    burstHoldUntil = 0;
                                    inBurst = false;
                                    burstQuietCount = 0;
                                    segmentBuffers.Clear();
                                    preRing.Clear();
                                    goto ContinueFramesLoop;
                                }
                            }

                            // Force-flush very long segments
                            var ms = segmentBuffers.Count * _cfg.FrameMs;
                            if (ms >= _cfg.MaxSegmentMs)
                            {
                                await EmitSegmentAsync(segmentBuffers, fmt, seq++, token, reason: "max").ConfigureAwait(false);
                                inSpeech = false;
                                enterCount = 0;
                                exitCount = 0;
                                burstHoldUntil = 0;
                                inBurst = false;
                                burstQuietCount = 0;
                                segmentBuffers.Clear();
                                preRing.Clear();
                            }
                        }
                        MaybeEmitMetrics(inSpeech);
                    }
                ContinueFramesLoop: ;
                }

                // Save leftover
                int leftover = total - offset;
                if (leftover > 0)
                {
                    carry = new byte[leftover];
                    Buffer.BlockCopy(tmp, offset, carry, 0, leftover);
                }
                else
                {
                    carry = Array.Empty<byte>();
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(tmp);
            }
        }

        _writer.TryComplete();
        // finalize session writer
        _sessionWriter?.Dispose();
    }

    /// <summary>
    /// Emits periodic metrics as a compact JSON line when enabled via configuration.
    /// </summary>
    private void MaybeEmitMetrics(bool inSpeech)
    {
        if (!_cfg.EnableMetrics) return;
        var now = Environment.TickCount64;
        if (_lastMetricsTick == 0) _lastMetricsTick = now;
        if (now - _lastMetricsTick < _cfg.MetricsIntervalMs) return;

        double avgRms = _metricsFrames > 0 ? _sumRms / _metricsFrames : 0.0;
        double avgAct = _metricsFrames > 0 ? _sumActiveRatio / _metricsFrames : 0.0;

        double dynEnter = _cfg.UseAdaptiveThresholds ? Math.Max(_cfg.EnterRms, _noiseFloorRms * _cfg.NoiseFloorEnterMultiplier) : _cfg.EnterRms;
        double dynExit = _cfg.UseAdaptiveThresholds ? Math.Max(_cfg.ExitRms, _noiseFloorRms * _cfg.NoiseFloorExitMultiplier) : _cfg.ExitRms;

        // Emit as one-line JSON for metrics consumers
        var json = string.Create(CultureInfo.InvariantCulture, $"{{\"type\":\"metrics\",\"state\":\"{(inSpeech ? "Speech" : "Silence")}\",\"avgRms\":{avgRms:F3},\"avgAct\":{avgAct:F3},\"noise\":{_noiseFloorRms:F3},\"enter\":{{\"rms\":{dynEnter:F3},\"act\":{_cfg.EnterActiveRatio:F2},\"n\":{_cfg.EnterConsecutive}}},\"exit\":{{\"rms\":{dynExit:F3},\"act\":{_cfg.ExitActiveRatio:F2},\"n\":{_cfg.ExitConsecutive}}}}}");
        _log.Metrics(json);

        _lastMetricsTick = now;
        _sumRms = 0;
        _sumActiveRatio = 0;
        _metricsFrames = 0;
    }

    /// <summary>
    /// Computes per-frame RMS, active sample ratio, and peak absolute value for simple VAD.
    /// </summary>
    private void AnalyzeFrame(byte[] frame, WaveFormat fmt, out double rms, out double activeRatio, out double peakAbs)
    {
        // 16-bit PCM little-endian assumed
        int samples = frame.Length / 2;
        if (samples == 0) { rms = 0; activeRatio = 0; peakAbs = 0; return; }

        long active = 0;
        double sumSq = 0;
        double peak = 0;
        for (int i = 0; i < samples; i++)
        {
            short s = (short)(frame[2 * i] | frame[2 * i + 1] << 8);
            double x = s / 32768.0;
            sumSq += x * x;
            if (Math.Abs(x) > _cfg.ActiveSampleAbsThreshold) active++;
            double ax = Math.Abs(x);
            if (ax > peak) peak = ax;
        }
        rms = Math.Sqrt(sumSq / samples);
        activeRatio = (double)active / samples;
        peakAbs = peak;
    }

    /// <summary>
    /// Sliding-window threshold check for entering speech state.
    /// </summary>
    private bool IsEnter(double rms, double activeRatio, ref int counter, double enterRms, double enterAct)
    {
        if (rms >= enterRms || activeRatio >= enterAct)
        {
            counter++;
            if (counter >= _cfg.EnterConsecutive) { counter = 0; return true; }
        }
        else
        {
            counter = 0;
        }
        return false;
    }

    /// <summary>
    /// Sliding-window threshold check for exiting speech state.
    /// </summary>
    private bool IsExit(double rms, double activeRatio, ref int counter, double exitRms, double exitAct)
    {
        if (rms <= exitRms && activeRatio <= exitAct)
        {
            counter++;
            if (counter >= _cfg.ExitConsecutive) { counter = 0; return true; }
        }
        else
        {
            counter = 0;
        }
        return false;
    }

    /// <summary>
    /// Builds a WAV stream from collected frames (with optional post-padding) and writes an <see cref="AudioChunk"/> to the channel.
    /// </summary>
    private async Task EmitSegmentAsync(List<byte[]> frames, WaveFormat fmt, long seq, CancellationToken token, string reason)
    {
        // Build WAV in-memory
        var ms = new MemoryStream();
        using (var writer = new WaveFileWriter(new IgnoreDisposeStream(ms), fmt))
        {
            int bytesPerMs = fmt.AverageBytesPerSecond / 1000;
            int frameBytes = _cfg.FrameMs * bytesPerMs;
            foreach (var f in frames)
            {
                await writer.WriteAsync(f, 0, f.Length, token).ConfigureAwait(false);
            }
            // Explicit post-padding to help STT with short utterances
            int padFrames = Math.Max(0, _cfg.AppendPaddingMs / _cfg.FrameMs);
            if (padFrames > 0)
            {
                var silence = new byte[frameBytes]; // zeroed
                for (int i = 0; i < padFrames; i++)
                {
                    await writer.WriteAsync(silence, 0, silence.Length, token).ConfigureAwait(false);
                }
            }
        }
        ms.Position = 0;

        var chunk = new AudioChunk
        {
            Sequence = seq,
            TimestampUtc = DateTimeOffset.UtcNow,
            DurationMs = frames.Count * _cfg.FrameMs,
            Stream = ms
        };

        if (!_writer.TryWrite(chunk))
        {
            await chunk.DisposeAsync();
        }
        else
        {
            if (_cfg.EnableMetrics)
            {
                var json = string.Create(CultureInfo.InvariantCulture, $"{{\"type\":\"segment\",\"seq\":{seq},\"ms\":{chunk.DurationMs},\"frames\":{frames.Count},\"reason\":\"{reason}\"}}");
                _log.Metrics(json);
            }
        }
    }
}
