namespace Elara.Host.Core.Interfaces
{
    /// <summary>
    /// Interface for text-to-speech services
    /// </summary>
    public interface ITextToSpeechService
    {
        /// <summary>
        /// Converts text to speech and returns the audio as a stream
        /// </summary>
        /// <param name="text">The text to convert to speech</param>
        /// <returns>A stream containing the synthesized speech audio</returns>
        Task<Stream> SpeakAsync(string text);
        
        /// <summary>
        /// Converts text to speech and saves the audio to a file
        /// </summary>
        /// <param name="text">The text to convert to speech</param>
        /// <param name="outputFilePath">The path where the audio file will be saved</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task SpeakToFileAsync(string text, string outputFilePath);
        
        /// <summary>
        /// Converts text to speech and plays it through the default audio output device
        /// </summary>
        /// <param name="text">The text to convert to speech</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task SpeakToDefaultOutputAsync(string text);
        
        /// <summary>
        /// Gets the available voices for this service
        /// </summary>
        /// <returns>A list of available voice names</returns>
        Task<IEnumerable<string>> GetAvailableVoicesAsync();
        
        /// <summary>
        /// Initializes the service exactly once with desired voice, rate, and pitch, and makes it immutable thereafter.
        /// Subsequent calls are no-ops.
        /// </summary>
        /// <param name="voice">Preferred voice name (nullable)</param>
        /// <param name="rate">Preferred rate multiplier (1.0 = normal)</param>
        /// <param name="pitch">Preferred pitch multiplier (1.0 = normal)</param>
        void InitializeOnce(string? voice, float? rate, float? pitch);

        /// <summary>
        /// Gets the currently selected voice
        /// </summary>
        string CurrentVoice { get; }
        
        /// <summary>
        /// Gets the speech rate
        /// </summary>
        float SpeechRate { get; }
        
        /// <summary>
        /// Gets the speech pitch
        /// </summary>
        float SpeechPitch { get; }
        
        /// <summary>
        /// Gets the name of the text-to-speech service provider
        /// </summary>
        string ProviderName { get; }
    }
}
