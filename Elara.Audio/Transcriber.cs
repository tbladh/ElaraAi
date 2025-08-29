using System.Threading.Channels;
using Elara.Core.Interfaces;
using Elara.Logging;
using NAudio.Wave;
using Elara.Core;

namespace Elara.Audio;

/// <summary>
/// Transcribes audio chunks from an input channel into text using an <see cref="ISpeechToTextService"/>.
/// Applies a simple RMS-based silence gate and a minimal word-count heuristic to mark items as meaningful.
/// Writes <see cref="TranscriptionItem"/> objects into the downstream channel for the FSM to consume.
/// </summary>
public sealed class Transcriber
{
    private readonly ISpeechToTextService _stt;
    private readonly ChannelReader<AudioChunk> _reader;
    private readonly ChannelWriter<TranscriptionItem>? _outWriter;
    private readonly ILog _log;

    // Heuristics for classifying a transcription as meaningful.
    private const int MinWords = 1; // relaxed heuristic for sensitivity
    private const double RmsSilenceThreshold = 0.015; // ~1.5% full scale (tweak as needed)

    /// <summary>
    /// Create a transcriber connected to input audio <paramref name="reader"/> and optional output <paramref name="outWriter"/>.
    /// </summary>
    public Transcriber(ISpeechToTextService stt, ChannelReader<AudioChunk> reader, ChannelWriter<TranscriptionItem>? outWriter = null, ILog? log = null)
    {
        _stt = stt;
        _reader = reader;
        _outWriter = outWriter;
        _log = log ?? new ComponentLogger("Transcriber");
        _log.Info("reporting in");
    }

    /// <summary>
    /// Main loop that reads audio chunks, performs optional RMS gating, calls STT, and publishes <see cref="TranscriptionItem"/>s.
    /// </summary>
    public async Task RunAsync(CancellationToken token)
    {
        try
        {
            // Iterate over incoming audio chunks until cancellation.
            await foreach (var chunk in _reader.ReadAllAsync(token))
            {
                try
                {
                    // Analyze chunk energy and optionally skip STT on silence
                    var rms = ComputeRms(chunk.Stream);
                    string text;
                    if (rms < RmsSilenceThreshold)
                    {
                        // Treat as silence: do not call STT; produce empty text item.
                        text = string.Empty;
                        // Reset position for any downstream readers
                        if (chunk.Stream.CanSeek) chunk.Stream.Position = 0;
                    }
                    else
                    {
                        // Non-silent: call STT to obtain transcription.
                        if (chunk.Stream.CanSeek) chunk.Stream.Position = 0; // reset after analysis
                        text = await _stt.TranscribeAsync(chunk.Stream);
                    }
                    var wordCount = CountWords(text);
                    var meaningful = !string.IsNullOrWhiteSpace(text) && wordCount >= MinWords;

                    var item = new TranscriptionItem
                    {
                        Sequence = chunk.Sequence,
                        TimestampUtc = chunk.TimestampUtc,
                        Text = text,
                        IsMeaningful = meaningful,
                        WordCount = wordCount
                    };

                    var stamp = chunk.TimestampUtc.LocalDateTime.ToString("HH:mm:ss");
                    var label = meaningful ? "ok" : "weak";

                    if (meaningful)
                    {
                        _log.Info($"#{chunk.Sequence} ({chunk.DurationMs}ms,{label}): {text}");
                    }
                    else
                    {
                        // no console output for silence in service; optionally could emit Debug
                    }

                    // Publish to downstream pipeline
                    if (_outWriter is not null)
                    {
                        try
                        {
                            await _outWriter.WriteAsync(item, token);
                        }
                        catch (OperationCanceledException) { /* ignore on shutdown */ }
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _log.Error($"Error on chunk #{chunk.Sequence}: {ex.Message}");
                }
                finally
                {
                    // Ensure audio resources are disposed
                    await chunk.DisposeAsync();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown: channel enumeration canceled
        }
        finally
        {
        }
    }

    /// <summary>
    /// Count words using whitespace splitting.
    /// </summary>
    private static int CountWords(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        var parts = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length;
    }

    /// <summary>
    /// Compute RMS energy of a WAV stream by reading samples and averaging squares.
    /// Returns 0.0 on failure as a conservative silence default.
    /// </summary>
    private static double ComputeRms(Stream wavStream)
    {
        try
        {
            if (wavStream.CanSeek) wavStream.Position = 0;
            using var reader = new WaveFileReader(wavStream);
            var sp = reader.ToSampleProvider();
            float[] buffer = new float[reader.WaveFormat.SampleRate];
            long samples = 0;
            double sumSquares = 0.0;
            int read;
            while ((read = sp.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int i = 0; i < read; i++)
                {
                    var s = buffer[i];
                    sumSquares += s * s;
                }
                samples += read;
            }
            if (samples == 0) return 0.0;
            return Math.Sqrt(sumSquares / samples);
        }
        catch
        {
            return 0.0; // On failure, err on the side of silence
        }
        finally
        {
            if (wavStream.CanSeek) wavStream.Position = 0;
        }
    }
}
