using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ErnestAi.Sandbox.Chunking.Tools
{
    /// <summary>
    /// Cross-platform application paths for caches and models.
    /// </summary>
    public static class AppPaths
    {
        // Async-first
        public static Task<string> GetCacheRootAsync()
        {
            return Task.FromResult(GetCacheRoot());
        }

        public static string GetCacheRoot()
        {
            // Prefer OS-standard cache locations
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(baseDir, "ErnestAi", "Cache");
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // ~/Library/Caches/ErnestAi
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(home, "Library", "Caches", "ErnestAi");
            }
            // Linux/Unix: XDG_CACHE_HOME or ~/.cache/ErnestAi
            var xdg = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
            if (!string.IsNullOrWhiteSpace(xdg))
            {
                return Path.Combine(xdg, "ErnestAi");
            }
            var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userHome, ".cache", "ErnestAi");
        }

        public static async Task<string> GetModelCacheDirAsync()
        {
            var root = await GetCacheRootAsync().ConfigureAwait(false);
            var dir = Path.Combine(root, "Models", "Whisper");
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static string GetModelCacheDir()
        {
            var root = GetCacheRoot();
            var dir = Path.Combine(root, "Models", "Whisper");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
