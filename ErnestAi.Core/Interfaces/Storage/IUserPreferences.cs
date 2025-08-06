namespace ErnestAi.Core.Interfaces.Storage
{
    /// <summary>
    /// Interface for storing and retrieving user preferences
    /// </summary>
    public interface IUserPreferences
    {
        /// <summary>
        /// Gets a user preference value
        /// </summary>
        /// <typeparam name="T">The type of the preference value</typeparam>
        /// <param name="key">The preference key</param>
        /// <param name="defaultValue">The default value to return if the preference doesn't exist</param>
        /// <returns>The preference value</returns>
        Task<T> GetPreferenceAsync<T>(string key, T defaultValue);
        
        /// <summary>
        /// Sets a user preference value
        /// </summary>
        /// <typeparam name="T">The type of the preference value</typeparam>
        /// <param name="key">The preference key</param>
        /// <param name="value">The preference value</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task SetPreferenceAsync<T>(string key, T value);
        
        /// <summary>
        /// Removes a user preference
        /// </summary>
        /// <param name="key">The preference key</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task RemovePreferenceAsync(string key);
        
        /// <summary>
        /// Checks if a user preference exists
        /// </summary>
        /// <param name="key">The preference key</param>
        /// <returns>True if the preference exists, false otherwise</returns>
        Task<bool> HasPreferenceAsync(string key);
        
        /// <summary>
        /// Gets all user preferences
        /// </summary>
        /// <returns>A dictionary of preference keys and values</returns>
        Task<IDictionary<string, object>> GetAllPreferencesAsync();
        
        /// <summary>
        /// Clears all user preferences
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        Task ClearPreferencesAsync();
        
        /// <summary>
        /// Gets the user's name
        /// </summary>
        Task<string> GetUserNameAsync();
        
        /// <summary>
        /// Sets the user's name
        /// </summary>
        /// <param name="name">The user's name</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task SetUserNameAsync(string name);
    }
}
