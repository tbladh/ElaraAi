using ErnestAi.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Whisper.net;
using System.Linq;
using ErnestAi.Configuration;

namespace ErnestAi.Speech
{
    /// <summary>
    /// Implementation of ISpeechToTextService using Whisper.net for speech recognition
    /// </summary>
    public class SpeechToTextService : ISpeechToTextService, IDisposable
    {
        private readonly string _modelFileName;
        private readonly string _modelUrl;
        private WhisperProcessor _processor;
        private readonly SemaphoreSlim _initSemaphore = new SemaphoreSlim(1, 1);
        private bool _isInitialized;
        private readonly SpeechToTextConfig _config;
        
        

        /// <summary>
        /// Creates a new instance of the SpeechToTextService
        /// </summary>
        /// <param name="modelFileName">The filename of the Whisper model to use</param>
        /// <param name="modelUrl">URL to download the model from if it doesn't exist locally</param>
        /// <param name="config">Configuration for the speech-to-text service</param>
        public SpeechToTextService(string modelFileName = "ggml-base.en.bin", 
            string modelUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.en.bin",
            SpeechToTextConfig config = null)
        {
            _modelFileName = modelFileName;
            _modelUrl = modelUrl;
            _config = config ?? new SpeechToTextConfig();
        }

        /// <summary>
        /// Initializes the speech-to-text service
        /// </summary>
        private async Task InitializeAsync()
        {
            await _initSemaphore.WaitAsync();
            try
            {
                if (_isInitialized)
                {
                    return;
                }

                string modelPath = await EnsureModelAsync(_modelFileName, _modelUrl);

                var whisperFactory = WhisperFactory.FromPath(modelPath);
                _processor = whisperFactory.CreateBuilder()
                    .WithLanguage("en")
                    .Build();

                _isInitialized = true;
            }
            finally
            {
                _initSemaphore.Release();
            }
        }

        /// <summary>
        /// Transcribes speech from an audio stream to text
        /// </summary>
        public async Task<string> TranscribeAsync(Stream audioStream)
        {
            await InitializeAsync();

            string result = "";
            await foreach (var segment in _processor.ProcessAsync(audioStream))
            {
                result += segment.Text;
            }
            return result.Trim();
        }

        /// <summary>
        /// Transcribes speech from an audio file to text
        /// </summary>
        public async Task<string> TranscribeFileAsync(string audioFilePath)
        {
            await InitializeAsync();

            await using var fileStream = File.OpenRead(audioFilePath);
            return await TranscribeAsync(fileStream);
        }

        /// <summary>
        /// Transcribes speech from a streaming audio source
        /// </summary>
        public async IAsyncEnumerable<TranscriptionSegment> TranscribeStreamAsync(
            IAsyncEnumerable<byte[]> audioStream, 
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await InitializeAsync();

            // Buffer for accumulating audio data
            using var buffer = new MemoryStream();
            
            // Process audio in chunks
            await foreach (var chunk in audioStream.WithCancellation(cancellationToken))
            {
                buffer.Write(chunk, 0, chunk.Length);
                
                // Process when we have enough data (1 second of audio at 16kHz, 16-bit mono)
                if (buffer.Length >= 32000)
                {
                    buffer.Position = 0;
                    
                    await foreach (var whisperSegment in _processor.ProcessAsync(buffer))
                    {
                        if (cancellationToken.IsCancellationRequested)
                            yield break;
                            
                        var segment = new TranscriptionSegment
                        {
                            Text = whisperSegment.Text,
                            StartTime = whisperSegment.Start.TotalSeconds,
                            EndTime = whisperSegment.End.TotalSeconds,
                            Confidence = 1.0f // Whisper.net doesn't provide confidence scores
                        };
                        
                        // Report transcribed text to host if configured
                        if (_config.OutputTranscriptionToConsole)
                        {
                            Console.WriteLine($"[Transcribed {segment.StartTime:F1}s-{segment.EndTime:F1}s]: {segment.Text}");
                        }
                        
                        // Eventing removed; host is responsible for logging the final transcription.
                        
                        yield return segment;
                    }
                    
                    buffer.SetLength(0);
                }
            }
            
            // Process any remaining audio
            if (buffer.Length > 0)
            {
                buffer.Position = 0;
                List<TranscriptionSegment> segments = new List<TranscriptionSegment>();
                
                await foreach (var whisperSegment in _processor.ProcessAsync(buffer))
                {
                    if (cancellationToken.IsCancellationRequested)
                        yield break;
                        
                    var segment = new TranscriptionSegment
                    {
                        Text = whisperSegment.Text,
                        StartTime = whisperSegment.Start.TotalSeconds,
                        EndTime = whisperSegment.End.TotalSeconds,
                        Confidence = 1.0f
                    };
                    
                    // Report transcribed text to host if configured
                    if (_config.OutputTranscriptionToConsole)
                    {
                        Console.WriteLine($"[Transcribed {segment.StartTime:F1}s-{segment.EndTime:F1}s]: {segment.Text}");
                    }
                    
                    // Eventing removed; host is responsible for logging the final transcription.
                    
                    segments.Add(segment);
                }
                
                foreach (var segment in segments)
                {
                    yield return segment;
                }
            }
        }

        /// <summary>
        /// Ensures the Whisper model is present in the executable folder.
        /// Downloads it if missing.
        /// </summary>
        private async Task<string> EnsureModelAsync(string fileName, string url)
        {
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string modelPath = Path.Combine(exeDir, fileName);

            if (!File.Exists(modelPath))
            {
                Console.WriteLine($"Downloading Whisper model '{fileName}'...");
                using var client = new HttpClient();
                var bytes = await client.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(modelPath, bytes);
                Console.WriteLine("Model downloaded.");
            }

            return modelPath;
        }

        public void Dispose()
        {
            _processor?.Dispose();
            _initSemaphore?.Dispose();
        }
    }
}
