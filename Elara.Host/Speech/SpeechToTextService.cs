using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using Elara.Host.Tools;
using Whisper.net;
using Elara.Host.Core.Interfaces;

namespace Elara.Host.Speech
{
    /// <summary>
    /// Minimal Whisper.net–backed speech-to-text (STT) implementation used by the sandbox host.
    /// This service lazily initializes a <see cref="WhisperFactory"/> from a local model file and
    /// exposes simple transcription APIs for full streams, files, and chunked streaming.
    /// </summary>
    public sealed class SpeechToTextService : ISpeechToTextService, IDisposable
    {
        private readonly string _modelPath;
        private WhisperFactory? _factory;
        private readonly SemaphoreSlim _initSemaphore = new(1, 1);
        private bool _isInitialized;

        /// <summary>
        /// Creates a new STT service that will load the Whisper model from the given absolute or relative file path.
        /// </summary>
        /// <param name="modelPath">Path to a Whisper model file (e.g., ggml-*.bin). The file must exist ahead of time.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="modelPath"/> is null or whitespace.</exception>
        public SpeechToTextService(string modelPath)
        {
            if (string.IsNullOrWhiteSpace(modelPath))
                throw new ArgumentException("Model path must be provided", nameof(modelPath));
            _modelPath = modelPath;
        }

        /// <summary>
        /// One-time lazy initialization. Verifies the model file exists and creates the Whisper factory.
        /// Thread-safe via <see cref="SemaphoreSlim"/>.
        /// </summary>
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

        /// <summary>
        /// Transcribes the provided audio stream using Whisper and returns the full concatenated text.
        /// </summary>
        /// <param name="audioStream">Audio stream containing WAV/PCM data compatible with Whisper.net.</param>
        /// <returns>Trimmed transcription text, possibly empty if no speech detected.</returns>
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

        /// <summary>
        /// Transcribes an audio file by path.
        /// </summary>
        /// <param name="audioFilePath">Path to an audio file readable by Whisper.net.</param>
        /// <returns>Transcribed text.</returns>
        public async Task<string> TranscribeFileAsync(string audioFilePath)
        {
            await using var fs = File.OpenRead(audioFilePath);
            return await TranscribeAsync(fs).ConfigureAwait(false);
        }

        /// <summary>
        /// Incrementally transcribes audio data from a byte-stream. The service buffers until a threshold
        /// (~1s at 16kHz mono) and then yields segments. Honors <paramref name="cancellationToken"/>.
        /// </summary>
        /// <param name="audioStream">Asynchronous sequence of raw audio byte buffers.</param>
        /// <param name="cancellationToken">Cancellation token to stop streaming and return immediately.</param>
        /// <returns>Asynchronously yields <see cref="TranscriptionSegment"/> as they are produced.</returns>
        public async IAsyncEnumerable<TranscriptionSegment> TranscribeStreamAsync(
            IAsyncEnumerable<byte[]> audioStream,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await InitializeAsync().ConfigureAwait(false);

            using var buffer = new MemoryStream();
            await foreach (var chunk in audioStream.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                buffer.Write(chunk, 0, chunk.Length);
                if (buffer.Length >= 32000) // ~1s at 16kHz 16-bit mono (heuristic batch size)
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

        /// <summary>
        /// Disposes internal resources, including the initialization semaphore.
        /// </summary>
        public void Dispose()
        {
            _factory = null;
            _initSemaphore?.Dispose();
        }
    }
}
