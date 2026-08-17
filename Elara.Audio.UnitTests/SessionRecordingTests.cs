using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Elara.Audio;
using Elara.Configuration;
using Elara.Core;
using Elara.Core.Interfaces;
using Elara.Logging;
using NAudio.Wave;
using Xunit;

namespace Elara.Audio.UnitTests;

public sealed class SessionRecordingTests
{
    [Fact]
    public void Start_WithEmptyScenario_UsesDefaultSessionDirectory()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), $"ElaraAudioTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(baseDir);

        var channel = Channel.CreateUnbounded<AudioChunk>();
        var streamer = new Streamer(new StubAudioProcessor(), channel.Writer, new SegmenterConfig(), new StubLog());
        var tolerances = new SessionToleranceConfig();
        SessionRecording? recording = null;

        try
        {
            recording = SessionRecording.Start(baseDir, string.Empty, new WaveFormat(16000, 1), streamer, tolerances);

            Assert.NotNull(recording);
            Assert.Contains(Path.Combine(baseDir, "session"), recording.SessionDir, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(recording.AudioWavPath));
        }
        finally
        {
            recording?.Dispose();
            channel.Writer.TryComplete();
            if (Directory.Exists(baseDir))
            {
                Directory.Delete(baseDir, recursive: true);
            }
        }
    }

    private sealed class StubAudioProcessor : IAudioProcessor
    {
        public Task StartRecordingAsync() => Task.CompletedTask;
        public Task<Stream> StopRecordingAsync() => Task.FromResult<Stream>(new MemoryStream());
        public IAsyncEnumerable<byte[]> GetAudioStreamAsync(CancellationToken cancellationToken) => EmptyStream();
        public Task PlayAudioAsync(Stream audioData) => Task.CompletedTask;

        private static async IAsyncEnumerable<byte[]> EmptyStream()
        {
            await Task.Yield();
            yield break;
        }
    }

    private sealed class StubLog : ILog
    {
        public void Debug(string message) { }
        public void Error(string message) { }
        public void Info(string message) { }
        public void Metrics(string message) { }
        public void Warn(string message) { }
    }
}
