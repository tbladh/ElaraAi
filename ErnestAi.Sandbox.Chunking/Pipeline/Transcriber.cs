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
    private readonly ConversationStateMachine _fsm;
    private readonly CompactConsole _console;
    private bool _inSilenceRun;

    private const int MinWords = 1; // relaxed heuristic for sensitivity

    public Transcriber(ISpeechToTextService stt, ChannelReader<AudioChunk> reader, ConversationStateMachine fsm, CompactConsole console)
    {
        _stt = stt;
        _reader = reader;
        _fsm = fsm;
        _console = console;
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

                // Notify FSM of the transcription for wake/silence handling
                _fsm.HandleTranscription(chunk.TimestampUtc, text, meaningful);
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

        // Ensure we end with a newline if the last chunks were silence
        if (_inSilenceRun)
        {
            _console.FlushSilence();
            _inSilenceRun = false;
        }
    }

    private static int CountWords(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        var parts = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length;
    }
}
