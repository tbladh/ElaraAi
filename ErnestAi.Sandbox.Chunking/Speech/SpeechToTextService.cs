using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using ErnestAi.Sandbox.Chunking.Core.Interfaces;
using ErnestAi.Sandbox.Chunking.Tools;
using Whisper.net;

namespace ErnestAi.Sandbox.Chunking.Speech
{
    /// <summary>
    /// Minimal Whisper.net-backed STT for the sandbox
    /// </summary>
    public sealed class SpeechToTextService : ISpeechToTextService, IDisposable
    {
        private readonly string _modelPath;
        private WhisperFactory? _factory;
        private readonly SemaphoreSlim _initSemaphore = new(1, 1);
        private bool _isInitialized;

        public SpeechToTextService(string modelPath)
        {
            if (string.IsNullOrWhiteSpace(modelPath))
                throw new ArgumentException("Model path must be provided", nameof(modelPath));
            _modelPath = modelPath;
        }

        private async Task InitializeAsync()
        {
            await _initSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_isInitialized) return;

                // Verify model presence at provided path; fatal if missing
                if (!File.Exists(_modelPath))
                {
                    throw new FileNotFoundException(
                        $"Whisper model not found: '{_modelPath}'. Pre-download is required at the composition root.");
                }
                _factory = WhisperFactory.FromPath(_modelPath);

                _isInitialized = true;
            }
            finally
            {
                _initSemaphore.Release();
            }
        }

        public async Task<string> TranscribeAsync(Stream audioStream)
        {
            await InitializeAsync().ConfigureAwait(false);
            string result = string.Empty;
            using var processor = _factory!.CreateBuilder()
                .WithLanguage("en")
                .Build();
            await foreach (var segment in processor.ProcessAsync(audioStream).ConfigureAwait(false))
            {
                result += segment.Text;
            }
            return result.Trim();
        }

        public async Task<string> TranscribeFileAsync(string audioFilePath)
        {
            await using var fs = File.OpenRead(audioFilePath);
            return await TranscribeAsync(fs).ConfigureAwait(false);
        }

        public async IAsyncEnumerable<TranscriptionSegment> TranscribeStreamAsync(
            IAsyncEnumerable<byte[]> audioStream,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await InitializeAsync().ConfigureAwait(false);

            using var buffer = new MemoryStream();
            await foreach (var chunk in audioStream.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                buffer.Write(chunk, 0, chunk.Length);
                if (buffer.Length >= 32000) // ~1s at 16kHz 16-bit mono
                {
                    buffer.Position = 0;
                    using (var processor = _factory!.CreateBuilder().WithLanguage("en").Build())
                    await foreach (var seg in processor.ProcessAsync(buffer).ConfigureAwait(false))
                    {
                        if (cancellationToken.IsCancellationRequested)
                            yield break;

                        yield return new TranscriptionSegment
                        {
                            Text = seg.Text,
                            StartTime = seg.Start.TotalSeconds,
                            EndTime = seg.End.TotalSeconds,
                            Confidence = 1.0f
                        };
                    }
                    buffer.SetLength(0);
                }
            }

            if (buffer.Length > 0)
            {
                buffer.Position = 0;
                using var processor = _factory!.CreateBuilder().WithLanguage("en").Build();
                await foreach (var seg in processor.ProcessAsync(buffer).ConfigureAwait(false))
                {
                    if (cancellationToken.IsCancellationRequested)
                        yield break;

                    yield return new TranscriptionSegment
                    {
                        Text = seg.Text,
                        StartTime = seg.Start.TotalSeconds,
                        EndTime = seg.End.TotalSeconds,
                        Confidence = 1.0f
                    };
                }
            }
        }

        // Model acquisition handled by Tools.FileDownloader

        public void Dispose()
        {
            _factory = null;
            _initSemaphore?.Dispose();
        }
    }
}
