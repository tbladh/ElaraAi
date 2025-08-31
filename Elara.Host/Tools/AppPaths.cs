using System;
using System.IO;
using System.Threading.Tasks;
using Elara.Core.Paths;

namespace Elara.Host.Tools
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

        public static string GetCacheRoot() => ModelPaths.GetCacheRoot();

        public static async Task<string> GetModelCacheDirAsync()
        {
            // Preserve async shape; delegate to Core
            var dir = ModelPaths.GetWhisperModelsDir();
            return await Task.FromResult(dir).ConfigureAwait(false);
        }

        public static string GetModelCacheDir()
        {
            return ModelPaths.GetWhisperModelsDir();
        }
    }
}
