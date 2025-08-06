namespace ErnestAi.Core.Interfaces
{
    /// <summary>
    /// Interface for audio processing components that handle audio input and output streams
    /// </summary>
    public interface IAudioProcessor
    {
        /// <summary>
        /// Starts recording audio from the default input device
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        Task StartRecordingAsync();
        
        /// <summary>
        /// Stops recording audio and returns the recorded data
        /// </summary>
        /// <returns>A stream containing the recorded audio data</returns>
        Task<Stream> StopRecordingAsync();
        
        /// <summary>
        /// Gets a stream of audio data in real-time
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the streaming operation</param>
        /// <returns>An asynchronous stream of audio data chunks</returns>
        IAsyncEnumerable<byte[]> GetAudioStreamAsync(CancellationToken cancellationToken);
        
        /// <summary>
        /// Plays audio data through the default output device
        /// </summary>
        /// <param name="audioData">The audio data to play</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task PlayAudioAsync(Stream audioData);
    }
}
