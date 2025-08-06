namespace ErnestAi.Core.Interfaces.Storage
{
    /// <summary>
    /// Interface for storing and retrieving conversation history
    /// </summary>
    public interface IConversationHistory
    {
        /// <summary>
        /// Adds a user message to the conversation history
        /// </summary>
        /// <param name="message">The user message</param>
        /// <returns>The ID of the added message</returns>
        Task<string> AddUserMessageAsync(string message);
        
        /// <summary>
        /// Adds an assistant message to the conversation history
        /// </summary>
        /// <param name="message">The assistant message</param>
        /// <returns>The ID of the added message</returns>
        Task<string> AddAssistantMessageAsync(string message);
        
        /// <summary>
        /// Gets the recent conversation history
        /// </summary>
        /// <param name="messageCount">The number of recent messages to retrieve</param>
        /// <returns>A list of conversation messages</returns>
        Task<IEnumerable<ConversationMessage>> GetRecentMessagesAsync(int messageCount);
        
        /// <summary>
        /// Gets the conversation history for a specific time range
        /// </summary>
        /// <param name="startTime">The start time</param>
        /// <param name="endTime">The end time</param>
        /// <returns>A list of conversation messages</returns>
        Task<IEnumerable<ConversationMessage>> GetMessagesByTimeRangeAsync(DateTime startTime, DateTime endTime);
        
        /// <summary>
        /// Searches the conversation history for messages containing the specified text
        /// </summary>
        /// <param name="searchText">The text to search for</param>
        /// <returns>A list of matching conversation messages</returns>
        Task<IEnumerable<ConversationMessage>> SearchMessagesAsync(string searchText);
        
        /// <summary>
        /// Clears the conversation history
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        Task ClearHistoryAsync();
        
        /// <summary>
        /// Gets the total number of messages in the conversation history
        /// </summary>
        /// <returns>The number of messages</returns>
        Task<int> GetMessageCountAsync();
    }
    
    /// <summary>
    /// Represents a message in a conversation
    /// </summary>
    public class ConversationMessage
    {
        /// <summary>
        /// Gets or sets the unique identifier for the message
        /// </summary>
        public string Id { get; set; }
        
        /// <summary>
        /// Gets or sets the role of the message sender (user or assistant)
        /// </summary>
        public MessageRole Role { get; set; }
        
        /// <summary>
        /// Gets or sets the content of the message
        /// </summary>
        public string Content { get; set; }
        
        /// <summary>
        /// Gets or sets the timestamp when the message was created
        /// </summary>
        public DateTime Timestamp { get; set; }
    }
    
    /// <summary>
    /// Represents the role of a message sender
    /// </summary>
    public enum MessageRole
    {
        /// <summary>
        /// The message was sent by the user
        /// </summary>
        User,
        
        /// <summary>
        /// The message was sent by the assistant
        /// </summary>
        Assistant,
        
        /// <summary>
        /// The message is a system message
        /// </summary>
        System
    }
}
