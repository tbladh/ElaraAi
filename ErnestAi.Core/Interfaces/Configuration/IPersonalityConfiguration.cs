namespace ErnestAi.Core.Interfaces.Configuration
{
    /// <summary>
    /// Interface for AI personality configuration settings
    /// </summary>
    public interface IPersonalityConfiguration
    {
        /// <summary>
        /// Gets or sets the name of the AI assistant
        /// </summary>
        string AssistantName { get; set; }
        
        /// <summary>
        /// Gets or sets the personality description
        /// </summary>
        string PersonalityDescription { get; set; }
        
        /// <summary>
        /// Gets or sets the greeting message
        /// </summary>
        string GreetingMessage { get; set; }
        
        /// <summary>
        /// Gets or sets the farewell message
        /// </summary>
        string FarewellMessage { get; set; }
        
        /// <summary>
        /// Gets or sets the thinking message
        /// </summary>
        string ThinkingMessage { get; set; }
        
        /// <summary>
        /// Gets or sets the error message
        /// </summary>
        string ErrorMessage { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether to use polite language
        /// </summary>
        bool UsePoliteLanguage { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether to use humor
        /// </summary>
        bool UseHumor { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether to use emojis
        /// </summary>
        bool UseEmojis { get; set; }
    }
}
