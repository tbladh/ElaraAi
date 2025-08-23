using System;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Elara.Host.Core.Interfaces;
using Elara.Host.Logging;

namespace Elara.Host.Pipeline;

/// <summary>
/// Simple time-sliced recorder that produces fixed-duration <see cref="AudioChunk"/> items.
/// Uses <see cref="IAudioProcessor"/> start/stop to acquire a WAV per slice; primarily for debugging/tests.
/// </summary>
public sealed class Recorder
{
    private readonly IAudioProcessor _audio;
    private readonly ChannelWriter<AudioChunk> _writer;
    private readonly int _chunkMs;
    private readonly ILog _log;

    /// <summary>
    /// Create a new recorder that writes chunks of length <paramref name="chunkMs"/> to <paramref name="writer"/>.
    /// </summary>
    public Recorder(IAudioProcessor audio, ChannelWriter<AudioChunk> writer, int chunkMs, ILog log)
    {
        _audio = audio;
        _writer = writer;
        _chunkMs = chunkMs;
        _log = log;
        _log.Info("reporting in");
    }

    /// <summary>
    /// Loop: start recording, wait for the configured slice duration, stop and emit a chunk.
    /// Caller cancels via <see cref="CancellationToken"/>. Completes channel on exit.
    /// </summary>
    public async Task RunAsync(CancellationToken token)
    {
        long seq = 0;
        while (!token.IsCancellationRequested)
        {
            var chunk = new AudioChunk
            {
                Sequence = seq++,
                TimestampUtc = DateTimeOffset.UtcNow,
                DurationMs = _chunkMs
            };

            try
            {
                await _audio.StartRecordingAsync();
                await Task.Delay(_chunkMs, token);
                var stream = await _audio.StopRecordingAsync();

                // Ensure independent stream ownership
                chunk.Stream.Dispose();
                chunk.Stream = new MemoryStream(((MemoryStream)stream).ToArray());
            }
            catch (OperationCanceledException)
            {
                try { await _audio.StopRecordingAsync(); } catch { }
                await chunk.DisposeAsync();
                break;
            }
            catch
            {
                await chunk.DisposeAsync();
                continue;
            }

            if (!_writer.TryWrite(chunk))
            {
                await chunk.DisposeAsync();
            }
        }

        _writer.TryComplete();
    }
}
