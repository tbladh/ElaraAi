using System.Text.Json;
using System.IO;
using System.Collections.Generic;
using Elara.Host.Configuration;
using Elara.Host.Logging;
using Elara.Host.Pipeline;
using NAudio.Wave;

namespace Elara.Host.Utilities;

/// <summary>
/// Manages optional full-session recording (audio WAV + expected.json).
/// Encapsulates directory layout, WaveFileWriter lifetime, item collection, and JSON emission.
/// </summary>
public sealed class SessionRecording : IDisposable
{
    private readonly WaveFileWriter _writer;
    private readonly SessionToleranceConfig _tolerances;

    public string SessionDir { get; }
    public string AudioWavPath { get; }
    public string ExpectedJsonPath { get; }
    public List<TranscriptionItem> Items { get; } = new(capacity: 256);

    private SessionRecording(string sessionDir, WaveFileWriter writer, SessionToleranceConfig tolerances)
    {
        _writer = writer;
        _tolerances = tolerances;
        SessionDir = sessionDir;
        AudioWavPath = Path.Combine(SessionDir, "audio.wav");
        ExpectedJsonPath = Path.Combine(SessionDir, "expected.json");
    }

    public static SessionRecording Start(string baseDir, string scenario, WaveFormat format, Streamer streamer, SessionToleranceConfig tolerances)
    {
        var runsBase = Path.IsPathRooted(baseDir) ? baseDir : Path.Combine(AppContext.BaseDirectory, baseDir);
        Directory.CreateDirectory(runsBase);
        var safeScenario = string.IsNullOrWhiteSpace(scenario) ? "session" : scenario;
        var stamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
        var sessionDir = Path.Combine(runsBase, safeScenario, stamp);
        Directory.CreateDirectory(sessionDir);
        var wavPath = Path.Combine(sessionDir, "audio.wav");
        var writer = new WaveFileWriter(wavPath, format);
        streamer.SetSessionWriter(writer);
        return new SessionRecording(sessionDir, writer, tolerances);
    }

    public void Add(TranscriptionItem item) => Items.Add(item);

    public async Task WriteExpectedAsync(AppConfig config)
    {
        var expectedObj = new
        {
            settings = new
            {
                wakeWord = config.Host.WakeWord,
                segmenter = config.Segmenter,
                stt = new { modelFile = config.Stt.ModelFile }
            },
            transcripts = Items,
            tolerances = new { cer = _tolerances.CER, wer = _tolerances.WER }
        };
        var json = JsonSerializer.Serialize(expectedObj, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(ExpectedJsonPath, json);
        Logger.Info("Recorder", $"Wrote expected.json with {Items.Count} items");
    }

    public void Dispose()
    {
        try { _writer?.Dispose(); } catch { }
    }
}
