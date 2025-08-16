using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ErnestAi.Sandbox.Chunking.Core.Interfaces;
using NAudio.Wave;

namespace ErnestAi.Sandbox.Chunking;

public sealed class Transcriber
{
    private readonly ISpeechToTextService _stt;
    private readonly ChannelReader<AudioChunk> _reader;
    private readonly ChannelWriter<TranscriptionItem>? _outWriter;
    private readonly CompactConsole _console;
    private bool _inSilenceRun;

    private const int MinWords = 1; // relaxed heuristic for sensitivity
    private const double RmsSilenceThreshold = 0.015; // ~1.5% full scale (tweak as needed)

    public Transcriber(ISpeechToTextService stt, ChannelReader<AudioChunk> reader, CompactConsole console, ChannelWriter<TranscriptionItem>? outWriter = null)
    {
        _stt = stt;
        _reader = reader;
        _outWriter = outWriter;
        _console = console;
    }

    public async Task RunAsync(CancellationToken token)
    {
        try
        {
            await foreach (var chunk in _reader.ReadAllAsync(token))
            {
                try
                {
                    // Analyze chunk energy and optionally skip STT on silence
                    var rms = ComputeRms(chunk.Stream);
                    string text;
                    if (rms < RmsSilenceThreshold)
                    {
                        text = string.Empty;
                        // Reset position for any downstream readers
                        if (chunk.Stream.CanSeek) chunk.Stream.Position = 0;
                    }
                    else
                    {
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
                        // Print speech line (helper ensures newline if needed)
                        _console.WriteSpeechLine($"[{stamp}] #{chunk.Sequence} ({chunk.DurationMs}ms,{label}): {text}");
                        _inSilenceRun = false;
                    }
                    else
                    {
                        // Compact dot for silence (no newline)
                        _console.WriteSilenceDot();
                        _inSilenceRun = true;
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
                    _console.WriteSpeechLine($"[Transcriber] Error on chunk #{chunk.Sequence}: {ex.Message}");
                }
                finally
                {
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
            // Ensure we end with a newline if the last chunks were silence
            if (_inSilenceRun)
            {
                _console.FlushSilence();
                _inSilenceRun = false;
            }
        }
    }

    private static int CountWords(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        var parts = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length;
    }

    private static double ComputeRms(System.IO.Stream wavStream)
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
