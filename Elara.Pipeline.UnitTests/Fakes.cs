using System.Collections.Concurrent;
using Elara.Core.Interfaces;
using Elara.Logging;
using Elara.Core.Time;

namespace Elara.Pipeline.UnitTests;

internal sealed class TestLog : ILog
{
    public readonly ConcurrentQueue<string> Lines = new();
    public void Debug(string message) => Lines.Enqueue($"DBG: {message}");
    public void Info(string message) => Lines.Enqueue($"INF: {message}");
    public void Warn(string message) => Lines.Enqueue($"WRN: {message}");
    public void Error(string message) => Lines.Enqueue($"ERR: {message}");
    public void Metrics(string message) => Lines.Enqueue($"MET: {message}");
}

internal sealed class ManualTimeProvider : ITimeProvider
{
    public ManualTimeProvider(DateTimeOffset start)
    {
        UtcNow = start;
    }

    public DateTimeOffset UtcNow { get; private set; }

    public void Advance(TimeSpan delta)
    {
        UtcNow = UtcNow + delta;
    }
}

internal sealed class FakeAudioProcessor : IAudioProcessor
{
    private readonly byte[] _payload;
    private bool _recording;

    public int StartCalls { get; private set; }
    public int StopCalls { get; private set; }

    public FakeAudioProcessor(byte[] payload)
    {
        _payload = payload;
    }

    public Task StartRecordingAsync()
    {
        _recording = true;
        StartCalls++;
        return Task.CompletedTask;
    }

    public Task<Stream> StopRecordingAsync()
    {
        _recording = false;
        StopCalls++;
        return Task.FromResult<Stream>(new MemoryStream(_payload, writable: false));
    }

    public async IAsyncEnumerable<byte[]> GetAudioStreamAsync(CancellationToken cancellationToken)
    {
        // Not used by Recorder tests; yield nothing
        await Task.CompletedTask;
        yield break;
    }

    public Task PlayAudioAsync(Stream audioData)
    {
        // No-op for tests
        return Task.CompletedTask;
    }
}
