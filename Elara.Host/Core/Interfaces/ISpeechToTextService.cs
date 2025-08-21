using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Elara.Host.Core.Interfaces
{
    /// <summary>
    /// Interface for speech-to-text services
    /// </summary>
    public interface ISpeechToTextService
    {
        Task<string> TranscribeAsync(Stream audioStream);
        Task<string> TranscribeFileAsync(string audioFilePath);
        IAsyncEnumerable<TranscriptionSegment> TranscribeStreamAsync(
            IAsyncEnumerable<byte[]> audioStream,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Represents a segment of transcribed speech
    /// </summary>
    public class TranscriptionSegment
    {
        public string Text { get; set; } = string.Empty;
        public double StartTime { get; set; }
        public double EndTime { get; set; }
        public float Confidence { get; set; }
    }
}
