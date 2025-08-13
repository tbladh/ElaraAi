using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace ErnestAi.Sandbox.Chunking.Tools
{
    /// <summary>
    /// Minimal helper to ensure a remote file exists locally.
    /// </summary>
    public static class FileDownloader
    {
        /// <summary>
        /// Ensures a file exists at targetPath by downloading it from the given URL if missing.
        /// Download is written atomically via a temp file and then moved.
        /// </summary>
        public static async Task<string> EnsureFileAsync(string targetPath, string url)
        {
            var directory = Path.GetDirectoryName(targetPath)!;
            Directory.CreateDirectory(directory);

            if (File.Exists(targetPath))
                return targetPath;

            var tempPath = targetPath + ".tmp";

            using var client = new HttpClient();
            var bytes = await client.GetByteArrayAsync(url).ConfigureAwait(false);
            await File.WriteAllBytesAsync(tempPath, bytes).ConfigureAwait(false);

            // Atomic move where possible
            if (File.Exists(targetPath))
            {
                try { File.Delete(tempPath); } catch { }
                return targetPath;
            }

            File.Move(tempPath, targetPath);
            return targetPath;
        }

        /// <summary>
        /// Helper to build a path from baseDirectory and fileName, then ensure it exists.
        /// </summary>
        public static Task<string> EnsureInDirectoryAsync(string baseDirectory, string fileName, string url)
        {
            var target = Path.Combine(baseDirectory, fileName);
            return EnsureFileAsync(target, url);
        }

        /// <summary>
        /// Downloads a file from url to targetPath, creating directories as needed.
        /// Overwrites the target file if it exists. Uses a temporary file for atomic replacement.
        /// </summary>
        public static async Task DownloadToFileAsync(string targetPath, string url)
        {
            var directory = Path.GetDirectoryName(targetPath)!;
            Directory.CreateDirectory(directory);

            var tempPath = targetPath + ".tmp";

            using var client = new HttpClient();
            var bytes = await client.GetByteArrayAsync(url).ConfigureAwait(false);
            await File.WriteAllBytesAsync(tempPath, bytes).ConfigureAwait(false);

            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }

            File.Move(tempPath, targetPath);
        }

        /// <summary>
        /// Synchronous wrapper for DownloadToFileAsync for use at composition root startup.
        /// </summary>
        public static void DownloadToFile(string targetPath, string url)
        {
            DownloadToFileAsync(targetPath, url).GetAwaiter().GetResult();
        }
    }
}
