using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Elara.Core.Paths
{
    /// <summary>
    /// Shared model/cache path resolution used by Host and Tests. No environment overrides.
    /// </summary>
    public static class ModelPaths
    {
        /// <summary>
        /// Gets the OS-specific cache root for Elara.
        /// </summary>
        public static string GetCacheRoot()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(baseDir, "ElaraAi", "Cache");
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(home, "Library", "Caches", "ElaraAi");
            }
            var xdg = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
            if (!string.IsNullOrWhiteSpace(xdg))
            {
                return Path.Combine(xdg, "ElaraAi");
            }
            var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userHome, ".cache", "ElaraAi");
        }

        /// <summary>
        /// Gets the Whisper models directory beneath the cache root. Ensures the directory exists.
        /// </summary>
        public static string GetWhisperModelsDir()
        {
            var root = GetCacheRoot();
            var dir = Path.Combine(root, "Models", "Whisper");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
