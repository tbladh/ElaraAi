using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Elara.Host.Configuration
{
    public static class ConfigLoader
    {
        // Async-first
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

        public static AppConfig Load(string? path = null)
        {
            return LoadAsync(path).GetAwaiter().GetResult();
        }
    }
}
