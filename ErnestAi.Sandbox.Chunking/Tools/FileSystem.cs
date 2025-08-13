using System;
using System.IO;

namespace ErnestAi.Sandbox.Chunking.Tools
{
    /// <summary>
    /// Minimal filesystem helpers for the sandbox composition root.
    /// </summary>
    public static class FileSystem
    {
        public static string Combine(params string[] parts) => Path.Combine(parts);
        public static string BaseDirectory => AppDomain.CurrentDomain.BaseDirectory;
        public static void EnsureDirectory(string directory)
        {
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
        public static bool FileExists(string path) => File.Exists(path);
    }
}
