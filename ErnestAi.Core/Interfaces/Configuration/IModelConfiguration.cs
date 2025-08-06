namespace ErnestAi.Core.Interfaces.Configuration
{
    /// <summary>
    /// Interface for AI model configuration settings
    /// </summary>
    public interface IModelConfiguration
    {
        /// <summary>
        /// Gets or sets the wake word model path
        /// </summary>
        string WakeWordModelPath { get; set; }
        
        /// <summary>
        /// Gets or sets the speech-to-text model path
        /// </summary>
        string SpeechToTextModelPath { get; set; }
        
        /// <summary>
        /// Gets or sets the language model name or path
        /// </summary>
        string LanguageModelName { get; set; }
        
        /// <summary>
        /// Gets or sets the wake word to listen for
        /// </summary>
        string WakeWord { get; set; }
        
        /// <summary>
        /// Gets or sets the wake word detection confidence threshold
        /// </summary>
        float WakeWordConfidenceThreshold { get; set; }
        
        /// <summary>
        /// Gets or sets the URL for the Ollama API
        /// </summary>
        string OllamaApiUrl { get; set; }
        
        /// <summary>
        /// Gets or sets the system prompt for the language model
        /// </summary>
        string SystemPrompt { get; set; }
        
        /// <summary>
        /// Gets or sets the temperature for language model responses
        /// </summary>
        float Temperature { get; set; }
        
        /// <summary>
        /// Gets or sets the maximum number of tokens in language model responses
        /// </summary>
        int MaxTokens { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether to use streaming responses
        /// </summary>
        bool UseStreamingResponses { get; set; }
        
        /// <summary>
        /// Gets or sets the voice name for text-to-speech
        /// </summary>
        string VoiceName { get; set; }
    }
}
