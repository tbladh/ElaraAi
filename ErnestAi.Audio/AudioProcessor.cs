using ErnestAi.Core.Interfaces;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ErnestAi.Audio
{
    /// <summary>
    /// Implementation of IAudioProcessor using NAudio for audio recording and playback
    /// </summary>
    public class AudioProcessor : IAudioProcessor, IDisposable
    {
        private WaveInEvent _waveIn;
        private WaveFileWriter _writer;
        private MemoryStream _memoryStream;
        private readonly WaveFormat _waveFormat;
        private bool _isRecording;
        private readonly SemaphoreSlim _recordingSemaphore = new SemaphoreSlim(1, 1);

        public AudioProcessor(int sampleRate = 16000, int channels = 1)
        {
            _waveFormat = new WaveFormat(sampleRate, channels);
        }

        /// <summary>
        /// Starts recording audio from the default input device
        /// </summary>
        public async Task StartRecordingAsync()
        {
            await _recordingSemaphore.WaitAsync();
            try
            {
                if (_isRecording)
                {
                    return;
                }

                _memoryStream = new MemoryStream();
                _writer = new WaveFileWriter(_memoryStream, _waveFormat);
                
                _waveIn = new WaveInEvent
                {
                    WaveFormat = _waveFormat
                };

                _waveIn.DataAvailable += (s, a) =>
                {
                    if (_writer != null)
                    {
                        _writer.Write(a.Buffer, 0, a.BytesRecorded);
                    }
                };

                _isRecording = true;
                _waveIn.StartRecording();
            }
            finally
            {
                _recordingSemaphore.Release();
            }
        }

        /// <summary>
        /// Stops recording audio and returns the recorded data
        /// </summary>
        public async Task<Stream> StopRecordingAsync()
        {
            await _recordingSemaphore.WaitAsync();
            try
            {
                if (!_isRecording)
                {
                    return new MemoryStream();
                }

                _waveIn.StopRecording();
                _isRecording = false;
                
                _waveIn.Dispose();
                _waveIn = null;
                
                _writer.Flush();
                _writer.Dispose();
                _writer = null;
                
                _memoryStream.Position = 0;
                return _memoryStream;
            }
            finally
            {
                _recordingSemaphore.Release();
            }
        }

        /// <summary>
        /// Gets a stream of audio data in real-time
        /// </summary>
        public async IAsyncEnumerable<byte[]> GetAudioStreamAsync(CancellationToken cancellationToken)
        {
            var waveIn = new WaveInEvent
            {
                WaveFormat = _waveFormat,
                BufferMilliseconds = 100
            };

            var bufferQueue = new Queue<byte[]>();
            var bufferAvailable = new SemaphoreSlim(0);

            waveIn.DataAvailable += (s, a) =>
            {
                var buffer = new byte[a.BytesRecorded];
                Array.Copy(a.Buffer, buffer, a.BytesRecorded);
                
                lock (bufferQueue)
                {
                    bufferQueue.Enqueue(buffer);
                }
                
                bufferAvailable.Release();
            };

            waveIn.StartRecording();

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await bufferAvailable.WaitAsync(cancellationToken);
                    
                    byte[] buffer;
                    lock (bufferQueue)
                    {
                        buffer = bufferQueue.Dequeue();
                    }
                    
                    yield return buffer;
                }
            }
            finally
            {
                waveIn.StopRecording();
                waveIn.Dispose();
            }
        }

        /// <summary>
        /// Plays audio data through the default output device
        /// </summary>
        public Task PlayAudioAsync(Stream audioData)
        {
            return Task.Run(() =>
            {
                audioData.Position = 0;
                using var reader = new WaveFileReader(audioData);
                using var waveOut = new WaveOutEvent();
                waveOut.Init(reader);
                waveOut.Play();
                
                while (waveOut.PlaybackState == PlaybackState.Playing)
                {
                    Thread.Sleep(100);
                }
            });
        }

        public void Dispose()
        {
            _waveIn?.Dispose();
            _writer?.Dispose();
            _memoryStream?.Dispose();
            _recordingSemaphore?.Dispose();
        }
    }
}
