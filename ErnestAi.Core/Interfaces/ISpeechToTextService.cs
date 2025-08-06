namespace ErnestAi.Core.Interfaces
{
    /// <summary>
    /// Interface for speech-to-text services
    /// </summary>
    public interface ISpeechToTextService
    {
        /// <summary>
        /// Transcribes speech from an audio stream to text
        /// </summary>
        /// <param name="audioStream">The audio stream containing speech</param>
        /// <returns>The transcribed text</returns>
        Task<string> TranscribeAsync(Stream audioStream);
        
        /// <summary>
        /// Transcribes speech from an audio file to text
        /// </summary>
        /// <param name="audioFilePath">Path to the audio file</param>
        /// <returns>The transcribed text</returns>
        Task<string> TranscribeFileAsync(string audioFilePath);
        
        /// <summary>
        /// Transcribes speech from a streaming audio source
        /// </summary>
        /// <param name="audioStream">An asynchronous stream of audio data chunks</param>
        /// <param name="cancellationToken">Token to cancel the transcription operation</param>
        /// <returns>An asynchronous stream of transcription segments</returns>
        IAsyncEnumerable<TranscriptionSegment> TranscribeStreamAsync(
            IAsyncEnumerable<byte[]> audioStream, 
            CancellationToken cancellationToken);
    }
    
    /// <summary>
    /// Represents a segment of transcribed speech
    /// </summary>
    public class TranscriptionSegment
    {
        /// <summary>
        /// The transcribed text for this segment
        /// </summary>
        public string Text { get; set; }
        
        /// <summary>
        /// The start time of this segment in the audio (in seconds)
        /// </summary>
        public double StartTime { get; set; }
        
        /// <summary>
        /// The end time of this segment in the audio (in seconds)
        /// </summary>
        public double EndTime { get; set; }
        
        /// <summary>
        /// The confidence score for this transcription segment
        /// </summary>
        public float Confidence { get; set; }
    }
}
