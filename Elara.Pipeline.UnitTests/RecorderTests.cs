using System.Threading.Channels;
using Elara.Core;
using Elara.Pipeline;

namespace Elara.Pipeline.UnitTests;

public class RecorderTests
{
    [Fact]
    public async Task Emits_Chunks_And_Completes_On_Cancel()
    {
        var log = new TestLog();
        var payload = new byte[] { 1, 2, 3, 4 };
        var audio = new FakeAudioProcessor(payload);
        var channel = Channel.CreateUnbounded<AudioChunk>();
        var recorder = new Recorder(audio, channel.Writer, chunkMs: 10, log);

        using var cts = new CancellationTokenSource();
        var runTask = recorder.RunAsync(cts.Token);

        // Read a couple of chunks
        var r1 = await channel.Reader.ReadAsync(CancellationToken.None);
        var r2 = await channel.Reader.ReadAsync(CancellationToken.None);

        Assert.True(r1.DurationMs >= 10);
        Assert.True(r2.DurationMs >= 10);
        Assert.NotEqual(r1.Sequence, r2.Sequence);
        Assert.True(r1.Stream.Length > 0);
        Assert.True(r2.Stream.Length > 0);

        // Cancel and ensure completion
        cts.Cancel();
        await runTask;

        // Channel should be completed eventually
        Assert.True(channel.Reader.Completion.IsCompleted);

        // Audio processor should have been started/stopped at least once
        Assert.True(audio.StartCalls >= 1);
        Assert.True(audio.StopCalls >= 1);

        await r1.DisposeAsync();
        await r2.DisposeAsync();
    }
}
