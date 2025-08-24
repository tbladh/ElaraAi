using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using Elara.Host.Core.Interfaces;
using NAudio.Utils;
using NAudio.Wave;

namespace Elara.Host.Audio
{
    /// <summary>
    /// Minimal <see cref="IAudioProcessor"/> using NAudio for recording and playback in the sandbox.
    /// Recording lifecycle is serialized by a semaphore to prevent overlapping Start/Stop calls.
    /// </summary>
    public class AudioProcessor : IAudioProcessor, IDisposable
    {
        private WaveInEvent? _waveIn;
        private WaveFileWriter? _writer;
        private MemoryStream? _memoryStream;
        private readonly WaveFormat _waveFormat;
        private bool _isRecording;
        private readonly SemaphoreSlim _recordingSemaphore = new(1, 1);

        private TaskCompletionSource<bool>? _firstDataTcs;
        private TaskCompletionSource<bool>? _recordingStoppedTcs;
        private long _bytesWritten;

        /// <summary>
        /// Constructs the processor with the desired PCM format.
        /// </summary>
        /// <param name="sampleRate">Sample rate in Hz (default 16000).</param>
        /// <param name="channels">Channel count (default 1 = mono).</param>
        public AudioProcessor(int sampleRate = 16000, int channels = 1)
        {
            _waveFormat = new WaveFormat(sampleRate, channels);
        }

        /// <summary>
        /// Starts microphone capture to an in-memory WAV stream. Waits up to 1s for first audio to arrive
        /// to fail fast if the input device is unavailable.
        /// </summary>
        public async Task StartRecordingAsync()
        {
            await _recordingSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_isRecording) return;

                _memoryStream = new MemoryStream();
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
                        if (!_firstDataTcs!.Task.IsCompleted)
                        {
                            _firstDataTcs.TrySetResult(true);
                        }
                    }
                };

                _waveIn.RecordingStopped += (s, a) =>
                {
                    if (!_recordingStoppedTcs!.Task.IsCompleted)
                    {
                        _recordingStoppedTcs.TrySetResult(true);
                    }
                };

                _isRecording = true;
                _waveIn.StartRecording();

                var firstDataWait = await Task.WhenAny(_firstDataTcs.Task, Task.Delay(1000)).ConfigureAwait(false);
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
        /// Stops microphone capture and returns a new seekable stream positioned at 0.
        /// Throws if total bytes are less than ~1s of audio to avoid downstream STT failures.
        /// </summary>
        /// <returns>MemoryStream containing a complete WAV.</returns>
        public async Task<Stream> StopRecordingAsync()
        {
            await _recordingSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!_isRecording || _memoryStream == null)
                {
                    return new MemoryStream();
                }

                _recordingStoppedTcs ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                _waveIn?.StopRecording();
                _isRecording = false;

                await Task.WhenAny(_recordingStoppedTcs.Task, Task.Delay(1000)).ConfigureAwait(false);

                _waveIn?.Dispose();
                _waveIn = null;

                _writer?.Flush();
                _writer?.Dispose();
                _writer = null;

                var resultStream = new MemoryStream();

                try
                {
                    if (_memoryStream != null && _memoryStream.CanRead)
                    {
                        _memoryStream.Position = 0;
                        await _memoryStream.CopyToAsync(resultStream).ConfigureAwait(false);
                    }
                }
                catch (ObjectDisposedException)
                {
                }
                finally
                {
                    try { _memoryStream?.Dispose(); } catch { }
                    _memoryStream = null;
                }

                resultStream.Position = 0;

                var minLength = 44 + _waveFormat.AverageBytesPerSecond;
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
        /// Provides a live raw byte stream from the microphone as an async enumerable.
        /// Each yielded buffer contains a copy of the input device buffer. Honors cancellation.
        /// </summary>
        public async IAsyncEnumerable<byte[]> GetAudioStreamAsync([EnumeratorCancellation] CancellationToken cancellationToken)
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
                    try
                    {
                        await bufferAvailable.WaitAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break; // graceful shutdown
                    }

                    byte[] buffer;
                    lock (bufferQueue)
                    {
                        if (bufferQueue.Count == 0)
                        {
                            // could be a spurious wake due to cancellation
                            if (cancellationToken.IsCancellationRequested) break;
                            continue;
                        }
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
        /// Plays a WAV stream to the default output device, blocking until playback completes.
        /// </summary>
        /// <param name="audioData">WAV stream; position will be reset to 0.</param>
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

        /// <summary>
        /// Releases unmanaged resources and disposes active NAudio objects if present.
        /// </summary>
        public void Dispose()
        {
            _waveIn?.Dispose();
            _writer?.Dispose();
            _memoryStream?.Dispose();
            _recordingSemaphore?.Dispose();
        }
    }
}
