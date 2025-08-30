using Microsoft.Extensions.Configuration;

namespace Elara.Configuration
{
    /// <summary>
    /// Loads <see cref="AppConfig"/> using the standard .NET configuration pipeline:
    /// appsettings.json -> appsettings.{Environment}.json -> environment variables -> command line.
    /// </summary>
    public static class ConfigLoader
    {
        // Async-first
        /// <summary>
        /// Asynchronously loads configuration using Microsoft.Extensions.Configuration.
        /// Base path defaults to <c>AppContext.BaseDirectory</c>. If <paramref name="path"/> is provided, it points
        /// to a base JSON file to use instead of the default appsettings.json.
        /// </summary>
        /// <param name="path">Optional explicit path to a base appsettings JSON. If null, uses appsettings.json in base directory.</param>
        /// <param name="args">Optional command-line args to include in configuration.</param>
        /// <returns>Deserialized <see cref="AppConfig"/> instance.</returns>
        /// <exception cref="InvalidOperationException">Thrown when deserialization fails.</exception>
        public static async Task<AppConfig> LoadAsync(string? path = null, string[]? args = null)
        {
            var env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                      ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                      ?? "Production";

            var builder = new ConfigurationBuilder();

            // If an explicit file path is provided, use its directory as base and that file as the base json.
            if (!string.IsNullOrWhiteSpace(path))
            {
                var dir = Path.GetDirectoryName(path!) ?? AppContext.BaseDirectory;
                var fileName = Path.GetFileName(path!);
                builder.SetBasePath(dir)
                       .AddJsonFile(fileName, optional: false, reloadOnChange: true)
                       .AddJsonFile($"{Path.GetFileNameWithoutExtension(fileName)}.{env}{Path.GetExtension(fileName)}", optional: true, reloadOnChange: true);
            }
            else
            {
                builder.SetBasePath(AppContext.BaseDirectory)
                       .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                       .AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: true);
            }

            builder.AddEnvironmentVariables();
            if (args is not null)
            {
                builder.AddCommandLine(args);
            }

            var configuration = builder.Build();

            // Bind to AppConfig using the standard binder
            var cfg = configuration.Get<AppConfig>();
            if (cfg is null)
            {
                throw new InvalidOperationException("Failed to load configuration.");
            }
            // simulate async to preserve signature; actual work is sync
            await Task.Yield();
            return cfg;
        }

        /// <summary>
        /// Synchronous wrapper for <see cref="LoadAsync"/>.
        /// </summary>
        /// <param name="path">Optional explicit path to the base JSON file.</param>
        /// <param name="args">Optional command-line args to include in configuration.</param>
        /// <returns>Deserialized <see cref="AppConfig"/>.</returns>
        public static AppConfig Load(string? path = null, string[]? args = null)
        {
            return LoadAsync(path, args).GetAwaiter().GetResult();
        }
    }
}
