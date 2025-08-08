using ErnestAi.Core.Interfaces;
using NAudio.Wave;
using NAudio.Utils;
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
        
        // Synchronization and diagnostics
        private TaskCompletionSource<bool> _firstDataTcs;
        private TaskCompletionSource<bool> _recordingStoppedTcs;
        private long _bytesWritten;

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
                // Use IgnoreDisposeStream so disposing the writer does NOT close _memoryStream
                _writer = new WaveFileWriter(new IgnoreDisposeStream(_memoryStream), _waveFormat);
                
                _bytesWritten = 0;
                _firstDataTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _recordingStoppedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                
                _waveIn = new WaveInEvent
                {
                    WaveFormat = _waveFormat,
                    BufferMilliseconds = 250
                };

                _waveIn.DataAvailable += (s, a) =>
                {
                    if (_writer != null)
                    {
                        _writer.Write(a.Buffer, 0, a.BytesRecorded);
                        _bytesWritten += a.BytesRecorded;
                        if (!_firstDataTcs.Task.IsCompleted)
                        {
                            _firstDataTcs.TrySetResult(true);
                        }
                    }
                };

                _waveIn.RecordingStopped += (s, a) =>
                {
                    if (!_recordingStoppedTcs.Task.IsCompleted)
                    {
                        _recordingStoppedTcs.TrySetResult(true);
                    }
                };

                _isRecording = true;
                _waveIn.StartRecording();
                
                // Ensure we actually receive audio before returning
                var firstDataWait = await Task.WhenAny(_firstDataTcs.Task, Task.Delay(1000));
                if (firstDataWait != _firstDataTcs.Task)
                {
                    throw new InvalidOperationException("No audio data received from input device.");
                }
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
                if (!_isRecording || _memoryStream == null)
                {
                    return new MemoryStream();
                }

                // Signal stop and wait for the device to fully stop delivering data
                if (_recordingStoppedTcs == null)
                {
                    _recordingStoppedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                }

                _waveIn?.StopRecording();
                _isRecording = false;
                
                // Wait briefly for RecordingStopped to ensure all buffers flushed
                await Task.WhenAny(_recordingStoppedTcs.Task, Task.Delay(1000));
                
                _waveIn?.Dispose();
                _waveIn = null;
                
                _writer?.Flush();
                _writer?.Dispose();
                _writer = null;
                
                // Create a new memory stream with the recorded data
                var resultStream = new MemoryStream();
                
                try
                {
                    // Only try to copy if the memory stream is still usable
                    if (_memoryStream != null && _memoryStream.CanRead)
                    {
                        _memoryStream.Position = 0;
                        await _memoryStream.CopyToAsync(resultStream);
                    }
                }
                catch (ObjectDisposedException)
                {
                    // If the stream was already disposed, just return an empty stream
                }
                finally
                {
                    try
                    {
                        _memoryStream?.Dispose();
                    }
                    catch
                    {
                        // Ignore any errors during disposal
                    }
                    _memoryStream = null;
                }
                
                resultStream.Position = 0;
                
                // Validate minimal size: header + ~1s of audio
                var minLength = 44 + _waveFormat.AverageBytesPerSecond; // WAV header + ~1 second
                if (resultStream.Length < minLength)
                {
                    throw new InvalidOperationException($"Recorded audio too short ({resultStream.Length} bytes, wrote {_bytesWritten} bytes).");
                }
                
                return resultStream;
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
