namespace ErnestAi.Core.Interfaces.Plugins
{
    /// <summary>
    /// Interface for managing plugins in the ErnestAi system
    /// </summary>
    public interface IPluginManager
    {
        /// <summary>
        /// Discovers and loads available plugins
        /// </summary>
        /// <param name="pluginDirectory">The directory to search for plugins</param>
        /// <returns>The number of plugins loaded</returns>
        Task<int> LoadPluginsAsync(string pluginDirectory);
        
        /// <summary>
        /// Gets all loaded plugins
        /// </summary>
        /// <returns>A collection of loaded plugins</returns>
        Task<IEnumerable<IPlugin>> GetLoadedPluginsAsync();
        
        /// <summary>
        /// Gets a plugin by its ID
        /// </summary>
        /// <param name="pluginId">The ID of the plugin to get</param>
        /// <returns>The plugin, or null if not found</returns>
        Task<IPlugin?> GetPluginAsync(string pluginId);
        
        /// <summary>
        /// Enables a plugin
        /// </summary>
        /// <param name="pluginId">The ID of the plugin to enable</param>
        /// <returns>True if the plugin was enabled, false otherwise</returns>
        Task<bool> EnablePluginAsync(string pluginId);
        
        /// <summary>
        /// Disables a plugin
        /// </summary>
        /// <param name="pluginId">The ID of the plugin to disable</param>
        /// <returns>True if the plugin was disabled, false otherwise</returns>
        Task<bool> DisablePluginAsync(string pluginId);
        
        /// <summary>
        /// Unloads a plugin
        /// </summary>
        /// <param name="pluginId">The ID of the plugin to unload</param>
        /// <returns>True if the plugin was unloaded, false otherwise</returns>
        Task<bool> UnloadPluginAsync(string pluginId);
        
        /// <summary>
        /// Gets the tools provided by all enabled plugins
        /// </summary>
        /// <returns>A collection of tool descriptions</returns>
        Task<IEnumerable<ToolDescription>> GetPluginToolsAsync();
        
        /// <summary>
        /// Executes a tool provided by a plugin
        /// </summary>
        /// <param name="toolName">The name of the tool to execute</param>
        /// <param name="parameters">The parameters for the tool</param>
        /// <returns>The result of the tool execution</returns>
        Task<ToolExecutionResult> ExecutePluginToolAsync(string toolName, IDictionary<string, object> parameters);
    }
    
    /// <summary>
    /// Interface for a plugin in the ErnestAi system
    /// </summary>
    public interface IPlugin
    {
        /// <summary>
        /// Gets the unique identifier for the plugin
        /// </summary>
        string Id { get; }
        
        /// <summary>
        /// Gets the name of the plugin
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// Gets the version of the plugin
        /// </summary>
        string Version { get; }
        
        /// <summary>
        /// Gets the description of the plugin
        /// </summary>
        string Description { get; }
        
        /// <summary>
        /// Gets the author of the plugin
        /// </summary>
        string Author { get; }
        
        /// <summary>
        /// Gets a value indicating whether the plugin is enabled
        /// </summary>
        bool IsEnabled { get; }
        
        /// <summary>
        /// Initializes the plugin
        /// </summary>
        /// <param name="serviceProvider">The service provider for dependency injection</param>
        /// <returns>True if initialization was successful, false otherwise</returns>
        Task<bool> InitializeAsync(IServiceProvider serviceProvider);
        
        /// <summary>
        /// Gets the tools provided by this plugin
        /// </summary>
        /// <returns>A collection of tool descriptions</returns>
        IEnumerable<ToolDescription> GetTools();
        
        /// <summary>
        /// Executes a tool provided by this plugin
        /// </summary>
        /// <param name="toolName">The name of the tool to execute</param>
        /// <param name="parameters">The parameters for the tool</param>
        /// <returns>The result of the tool execution</returns>
        Task<ToolExecutionResult> ExecuteToolAsync(string toolName, IDictionary<string, object> parameters);
    }
}
