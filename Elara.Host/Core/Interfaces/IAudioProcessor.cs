using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Elara.Host.Core.Interfaces
{
    /// <summary>
    /// Interface for audio processing components that handle audio input and output streams
    /// </summary>
    public interface IAudioProcessor
    {
        Task StartRecordingAsync();
        Task<Stream> StopRecordingAsync();
        IAsyncEnumerable<byte[]> GetAudioStreamAsync(CancellationToken cancellationToken);
        Task PlayAudioAsync(Stream audioData);
    }
}
