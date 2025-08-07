using ErnestAi.Core.Interfaces;
using NAudio.Utils;
using NAudio.Wave;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Whisper.net;

namespace ErnestAi.Audio
{
    /// <summary>
    /// Implementation of IWakeWordDetector using Whisper.net for wake word detection
    /// </summary>
    public class WakeWordDetector : IWakeWordDetector, IDisposable
    {
        private readonly string _modelFileName;
        private readonly string _modelUrl;
        private WhisperProcessor _processor;
        private WaveInEvent _waveIn;
        private MemoryStream _buffer;
        private CancellationTokenSource _cts;
        private bool _isListening;
        private readonly SemaphoreSlim _listeningSemaphore = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Event triggered when the wake word is detected
        /// </summary>
        public event EventHandler<WakeWordDetectedEventArgs> WakeWordDetected;

        /// <summary>
        /// Gets or sets the wake word to listen for
        /// </summary>
        public string WakeWord { get; set; } = "ernest";

        /// <summary>
        /// Gets or sets the confidence threshold for wake word detection
        /// </summary>
        public float ConfidenceThreshold { get; set; } = 0.5f;

        /// <summary>
        /// Creates a new instance of the WakeWordDetector
        /// </summary>
        /// <param name="modelFileName">The filename of the Whisper model</param>
        /// <param name="modelUrl">The URL to download the Whisper model from if not present</param>
        public WakeWordDetector(string modelFileName = "ggml-tiny.en.bin", 
            string modelUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.en.bin")
        {
            _modelFileName = modelFileName;
            _modelUrl = modelUrl;
        }

        /// <summary>
        /// Starts listening for the wake word
        /// </summary>
        public async Task StartListeningAsync(CancellationToken cancellationToken)
        {
            await _listeningSemaphore.WaitAsync();
            try
            {
                if (_isListening)
                {
                    return;
                }

                string modelPath = await EnsureModelAsync(_modelFileName, _modelUrl);

                var whisperFactory = WhisperFactory.FromPath(modelPath);
                _processor = whisperFactory.CreateBuilder()
                    .WithLanguage("en")
                    .Build();

                _waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(16000, 1),
                    BufferMilliseconds = 1500
                };

                _buffer = new MemoryStream();
                _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                _waveIn.DataAvailable += WaveIn_DataAvailable;
                _isListening = true;
                _waveIn.StartRecording();

                Console.WriteLine($"Listening for wake word: \"{WakeWord}\"...");
            }
            finally
            {
                _listeningSemaphore.Release();
            }
        }

        /// <summary>
        /// Stops listening for the wake word
        /// </summary>
        public async Task StopListeningAsync()
        {
            await _listeningSemaphore.WaitAsync();
            try
            {
                if (!_isListening)
                {
                    return;
                }

                _cts?.Cancel();
                _waveIn?.StopRecording();
                _isListening = false;

                _waveIn?.Dispose();
                _waveIn = null;
                _processor?.Dispose();
                _processor = null;
                _buffer?.Dispose();
                _buffer = null;
            }
            finally
            {
                _listeningSemaphore.Release();
            }
        }

        private async void WaveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            if (_cts.Token.IsCancellationRequested || _buffer == null)
                return;

            _buffer.Write(e.Buffer, 0, e.BytesRecorded);

            if (_buffer.Length >= _waveIn.WaveFormat.AverageBytesPerSecond)
            {
                _buffer.Position = 0;

                // Create a WAV header around raw PCM without disposing the underlying stream
                using var wavStream = new MemoryStream();
                using (var writer = new WaveFileWriter(new IgnoreDisposeStream(wavStream), _waveIn.WaveFormat))
                {
                    writer.Write(_buffer.ToArray(), 0, (int)_buffer.Length);
                    writer.Flush();
                }

                wavStream.Position = 0; // rewind for Whisper.NET

                string transcript = "";
                try
                {
                    await foreach (var segment in _processor.ProcessAsync(wavStream))
                    {
                        transcript += segment.Text;
                    }

                    if (!string.IsNullOrWhiteSpace(transcript) &&
                        transcript.IndexOf(WakeWord, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Console.WriteLine($"Wake word '{WakeWord}' detected: {transcript}");
                        
                        // Calculate a simple confidence score based on the length of the transcript
                        // This is a simplification; in a real implementation, you'd use the model's confidence
                        float confidence = 1.0f;
                        
                        OnWakeWordDetected(new WakeWordDetectedEventArgs
                        {
                            DetectedText = transcript,
                            Confidence = confidence,
                            Timestamp = DateTime.Now
                        });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing audio: {ex.Message}");
                }

                _buffer.SetLength(0);
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

        protected virtual void OnWakeWordDetected(WakeWordDetectedEventArgs e)
        {
            WakeWordDetected?.Invoke(this, e);
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _waveIn?.Dispose();
            _processor?.Dispose();
            _buffer?.Dispose();
            _listeningSemaphore?.Dispose();
        }
    }
}
