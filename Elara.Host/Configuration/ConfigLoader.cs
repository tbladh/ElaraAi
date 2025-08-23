using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Elara.Host.Configuration
{
    /// <summary>
    /// Loads <see cref="AppConfig"/> from JSON (appsettings.json by default) with tolerant JSON options.
    /// </summary>
    public static class ConfigLoader
    {
        // Async-first
        /// <summary>
        /// Asynchronously loads configuration. If <paramref name="path"/> is null, uses
        /// <c>AppContext.BaseDirectory/appsettings.json</c>.
        /// </summary>
        /// <param name="path">Optional explicit path to the JSON file.</param>
        /// <returns>Deserialized <see cref="AppConfig"/> instance.</returns>
        /// <exception cref="InvalidOperationException">Thrown when deserialization fails.</exception>
        public static async Task<AppConfig> LoadAsync(string? path = null)
        {
            var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            var json = await File.ReadAllTextAsync(settingsPath).ConfigureAwait(false);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            var cfg = JsonSerializer.Deserialize<AppConfig>(json, options);
            return cfg == null ? throw new InvalidOperationException("Failed to load configuration. Ensure appsettings.json is valid.") : cfg;
        }

        /// <summary>
        /// Synchronous wrapper for <see cref="LoadAsync"/>.
        /// </summary>
        /// <param name="path">Optional explicit path (currently ignored; default path is used).</param>
        /// <returns>Deserialized <see cref="AppConfig"/>.</returns>
        public static AppConfig Load(string? path = null)
        {
            return LoadAsync(path).GetAwaiter().GetResult();
        }
    }
}
