namespace ErnestAi.Core.Interfaces
{
    /// <summary>
    /// Interface for components that can execute tools or commands
    /// </summary>
    public interface IToolExecutor
    {
        /// <summary>
        /// Executes a tool with the given parameters
        /// </summary>
        /// <param name="toolName">The name of the tool to execute</param>
        /// <param name="parameters">The parameters for the tool</param>
        /// <returns>The result of the tool execution</returns>
        Task<ToolExecutionResult> ExecuteToolAsync(string toolName, IDictionary<string, object> parameters);
        
        /// <summary>
        /// Gets the available tools that can be executed
        /// </summary>
        /// <returns>A list of available tool descriptions</returns>
        Task<IEnumerable<ToolDescription>> GetAvailableToolsAsync();
        
        /// <summary>
        /// Checks if a specific tool is available
        /// </summary>
        /// <param name="toolName">The name of the tool to check</param>
        /// <returns>True if the tool is available, false otherwise</returns>
        Task<bool> IsToolAvailableAsync(string toolName);
    }
    
    /// <summary>
    /// Represents the result of executing a tool
    /// </summary>
    public class ToolExecutionResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the tool execution was successful
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// Gets or sets the result data from the tool execution
        /// </summary>
        public object? ResultData { get; set; }
        
        /// <summary>
        /// Gets or sets the error message if the execution failed
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
    
    /// <summary>
    /// Describes a tool that can be executed
    /// </summary>
    public class ToolDescription
    {
        /// <summary>
        /// Gets or sets the name of the tool
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Gets or sets the description of what the tool does
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// Gets or sets the parameters that the tool accepts
        /// </summary>
        public IDictionary<string, ParameterDescription> Parameters { get; set; }
    }
    
    /// <summary>
    /// Describes a parameter for a tool
    /// </summary>
    public class ParameterDescription
    {
        /// <summary>
        /// Gets or sets the name of the parameter
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Gets or sets the description of the parameter
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether the parameter is required
        /// </summary>
        public bool Required { get; set; }
        
        /// <summary>
        /// Gets or sets the type of the parameter
        /// </summary>
        public string Type { get; set; }
    }
}
