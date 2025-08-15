using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ErnestAi.Sandbox.Chunking.Audio;
using ErnestAi.Sandbox.Chunking.Configuration;
using ErnestAi.Sandbox.Chunking.Core.Interfaces;
using NAudio.Utils;
using NAudio.Wave;

namespace ErnestAi.Sandbox.Chunking;

/// <summary>
/// Continuous audio streamer with simple VAD-based segmentation.
/// Emits AudioChunk items at speech boundaries with pre/post padding.
/// </summary>
public sealed class Streamer
{
    private readonly IAudioProcessor _audio;
    private readonly ChannelWriter<AudioChunk> _writer;
    private readonly SegmenterConfig _cfg;
    private readonly CompactConsole _console;

    // Metrics accumulators
    private long _lastMetricsTick;
    private double _sumRms;
    private double _sumActiveRatio;
    private int _metricsFrames;

    public Streamer(IAudioProcessor audio, ChannelWriter<AudioChunk> writer, SegmenterConfig cfg, CompactConsole console)
    {
        _audio = audio;
        _writer = writer;
        _cfg = cfg;
        _console = console;
    }

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

        // Frame assembly from variable-size input buffers
        byte[] carry = Array.Empty<byte>();

        await foreach (var buffer in _audio.GetAudioStreamAsync(token).ConfigureAwait(false))
        {
            if (token.IsCancellationRequested) break;

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

                    AnalyzeFrame(frame, fmt, out double rms, out double activeRatio);
                    // Metrics accumulation
                    if (_cfg.EnableMetrics)
                    {
                        _sumRms += rms;
                        _sumActiveRatio += activeRatio;
                        _metricsFrames++;
                    }

                    if (!inSpeech)
                    {
                        // Maintain pre-speech ring
                        if (preFrames > 0)
                        {
                            if (preRing.Count == preFrames) preRing.Dequeue();
                            preRing.Enqueue(frame);
                        }

                        if (IsEnter(rms, activeRatio, ref enterCount))
                        {
                            inSpeech = true;
                            exitCount = 0;
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
                        if (IsExit(rms, activeRatio, ref exitCount))
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
                            segmentBuffers.Clear();
                            preRing.Clear();
                        }
                        else
                        {
                            // Force-flush very long segments
                            var ms = segmentBuffers.Count * _cfg.FrameMs;
                            if (ms >= _cfg.MaxSegmentMs)
                            {
                                await EmitSegmentAsync(segmentBuffers, fmt, seq++, token, reason: "max").ConfigureAwait(false);
                                inSpeech = false;
                                enterCount = 0;
                                exitCount = 0;
                                segmentBuffers.Clear();
                                preRing.Clear();
                            }
                        }
                        MaybeEmitMetrics(inSpeech);
                    }
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
    }

    private void MaybeEmitMetrics(bool inSpeech)
    {
        if (!_cfg.EnableMetrics) return;
        var now = Environment.TickCount64;
        if (_lastMetricsTick == 0) _lastMetricsTick = now;
        if (now - _lastMetricsTick < _cfg.MetricsIntervalMs) return;

        double avgRms = _metricsFrames > 0 ? _sumRms / _metricsFrames : 0.0;
        double avgAct = _metricsFrames > 0 ? _sumActiveRatio / _metricsFrames : 0.0;

        _console.WriteStateLine(
            $"[METRICS] state={(inSpeech ? "Speech" : "Silence")} avgRms={avgRms:F3} avgAct={avgAct:F3} " +
            $"enter(rms={_cfg.EnterRms:F3},act={_cfg.EnterActiveRatio:F2},n={_cfg.EnterConsecutive}) " +
            $"exit(rms={_cfg.ExitRms:F3},act={_cfg.ExitActiveRatio:F2},n={_cfg.ExitConsecutive})");

        _lastMetricsTick = now;
        _sumRms = 0;
        _sumActiveRatio = 0;
        _metricsFrames = 0;
    }

    private void AnalyzeFrame(byte[] frame, WaveFormat fmt, out double rms, out double activeRatio)
    {
        // 16-bit PCM little-endian assumed
        int samples = frame.Length / 2;
        if (samples == 0) { rms = 0; activeRatio = 0; return; }

        long active = 0;
        double sumSq = 0;
        for (int i = 0; i < samples; i++)
        {
            short s = (short)(frame[2 * i] | (frame[2 * i + 1] << 8));
            double x = s / 32768.0;
            sumSq += x * x;
            if (Math.Abs(x) > _cfg.ActiveSampleAbsThreshold) active++;
        }
        rms = Math.Sqrt(sumSq / samples);
        activeRatio = (double)active / samples;
    }

    private bool IsEnter(double rms, double activeRatio, ref int counter)
    {
        if (rms >= _cfg.EnterRms || activeRatio >= _cfg.EnterActiveRatio)
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

    private bool IsExit(double rms, double activeRatio, ref int counter)
    {
        if (rms <= _cfg.ExitRms && activeRatio <= _cfg.ExitActiveRatio)
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

    private async Task EmitSegmentAsync(List<byte[]> frames, WaveFormat fmt, long seq, CancellationToken token, string reason)
    {
        // Build WAV in-memory
        var ms = new MemoryStream();
        using (var writer = new WaveFileWriter(new IgnoreDisposeStream(ms), fmt))
        {
            foreach (var f in frames)
            {
                await writer.WriteAsync(f, 0, f.Length, token).ConfigureAwait(false);
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
            _console.WriteSilenceDot(); // compact UI feedback
            if (_cfg.EnableMetrics)
            {
                _console.WriteStateLine($"[SEGMENT] seq={seq} ms={chunk.DurationMs} frames={frames.Count} reason={reason}");
            }
        }
    }
}
