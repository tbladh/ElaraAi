namespace ErnestAi.Core.Interfaces.Storage
{
    /// <summary>
    /// Interface for caching and managing AI models
    /// </summary>
    public interface IModelCache
    {
        /// <summary>
        /// Gets the path to a cached model
        /// </summary>
        /// <param name="modelName">The name of the model</param>
        /// <returns>The path to the cached model, or null if not cached</returns>
        Task<string?> GetModelPathAsync(string modelName);
        
        /// <summary>
        /// Adds a model to the cache
        /// </summary>
        /// <param name="modelName">The name of the model</param>
        /// <param name="modelPath">The path to the model file</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task AddModelAsync(string modelName, string modelPath);
        
        /// <summary>
        /// Checks if a model is cached
        /// </summary>
        /// <param name="modelName">The name of the model</param>
        /// <returns>True if the model is cached, false otherwise</returns>
        Task<bool> IsModelCachedAsync(string modelName);
        
        /// <summary>
        /// Downloads a model from a remote source
        /// </summary>
        /// <param name="modelName">The name of the model</param>
        /// <param name="modelUrl">The URL to download the model from</param>
        /// <param name="progressCallback">An optional callback to report download progress</param>
        /// <returns>The path to the downloaded model</returns>
        Task<string> DownloadModelAsync(string modelName, string modelUrl, IProgress<float>? progressCallback = null);
        
        /// <summary>
        /// Removes a model from the cache
        /// </summary>
        /// <param name="modelName">The name of the model</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task RemoveModelAsync(string modelName);
        
        /// <summary>
        /// Gets the list of cached models
        /// </summary>
        /// <returns>A list of cached model names</returns>
        Task<IEnumerable<string>> GetCachedModelsAsync();
        
        /// <summary>
        /// Gets the total size of all cached models
        /// </summary>
        /// <returns>The total size in bytes</returns>
        Task<long> GetTotalCacheSizeAsync();
        
        /// <summary>
        /// Clears all cached models
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        Task ClearCacheAsync();
    }
}
