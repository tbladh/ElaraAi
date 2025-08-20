namespace ErnestAi.Sandbox.Chunking.Core.Interfaces
{
    /// <summary>
    /// Interface for language model services that process natural language
    /// </summary>
    public interface ILanguageModelService
    {
        /// <summary>
        /// Gets a response from the language model for the given prompt
        /// </summary>
        /// <param name="prompt">The input prompt</param>
        /// <param name="cancellationToken">Token to cancel the operation</param>
        /// <returns>The model's response</returns>
        Task<string> GetResponseAsync(string prompt, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets the available models for this service
        /// </summary>
        /// <returns>A list of available model names</returns>
        Task<IEnumerable<string>> GetAvailableModelsAsync();
        
        /// <summary>
        /// Gets or sets the currently selected model
        /// </summary>
        string CurrentModel { get; set; }
        
        /// <summary>
        /// Gets or sets the system prompt to use with the model
        /// </summary>
        string SystemPrompt { get; set; }
        
        /// <summary>
        /// Gets the name of the language model service provider
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Regex patterns (as strings) used to filter/snippet out unwanted text from outputs.
        /// Patterns are applied as regular expressions using Singleline mode.
        /// </summary>
        IList<string> OutputFilters { get; set; }

        /// <summary>
        /// Gets a value indicating whether the service is available
        /// </summary>
        Task<bool> IsAvailableAsync();
    }
}
