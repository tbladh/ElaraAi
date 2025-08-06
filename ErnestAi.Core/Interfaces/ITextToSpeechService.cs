namespace ErnestAi.Core.Interfaces
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
        /// Gets or sets the currently selected voice
        /// </summary>
        string CurrentVoice { get; set; }
        
        /// <summary>
        /// Gets or sets the speech rate
        /// </summary>
        float SpeechRate { get; set; }
        
        /// <summary>
        /// Gets or sets the speech pitch
        /// </summary>
        float SpeechPitch { get; set; }
        
        /// <summary>
        /// Gets the name of the text-to-speech service provider
        /// </summary>
        string ProviderName { get; }
    }
}
