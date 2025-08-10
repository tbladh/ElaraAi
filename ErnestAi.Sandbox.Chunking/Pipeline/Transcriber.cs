using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ErnestAi.Core.Interfaces;

namespace ErnestAi.Sandbox.Chunking;

public sealed class Transcriber
{
    private readonly ISpeechToTextService _stt;
    private readonly ChannelReader<AudioChunk> _reader;

    private const int MinWords = 1; // relaxed heuristic for sensitivity

    public Transcriber(ISpeechToTextService stt, ChannelReader<AudioChunk> reader)
    {
        _stt = stt;
        _reader = reader;
    }

    public async Task RunAsync(CancellationToken token)
    {
        await foreach (var chunk in _reader.ReadAllAsync(token))
        {
            try
            {
                var text = await _stt.TranscribeAsync(chunk.Stream);
                var wordCount = CountWords(text);
                var meaningful = !string.IsNullOrWhiteSpace(text) && wordCount >= MinWords;

                var stamp = chunk.TimestampUtc.LocalDateTime.ToString("HH:mm:ss");
                var label = meaningful ? "ok" : "weak";
                Console.WriteLine($"[{stamp}] #{chunk.Sequence} ({chunk.DurationMs}ms,{label}): {text}");
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Transcriber] Error on chunk #{chunk.Sequence}: {ex.Message}");
            }
            finally
            {
                await chunk.DisposeAsync();
            }
        }
    }

    private static int CountWords(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        var parts = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length;
    }
}
