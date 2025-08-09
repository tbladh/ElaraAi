using System;
using System.IO;
using System.Threading.Tasks;

namespace ErnestAi.Tools
{
    public class FileCacheService : ICacheService
    {
        public async Task<bool> TryReadAsync(string key, Func<string, string> keyToPath, Stream destination)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (keyToPath == null) throw new ArgumentNullException(nameof(keyToPath));
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            var path = keyToPath(key);
            if (File.Exists(path))
            {
                using var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                await fs.CopyToAsync(destination);
                if (destination.CanSeek) destination.Position = 0;
                return true;
            }
            return false;
        }

        public async Task WriteAsync(string key, Func<string, string> keyToPath, Stream source)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (keyToPath == null) throw new ArgumentNullException(nameof(keyToPath));
            if (source == null) throw new ArgumentNullException(nameof(source));

            var path = keyToPath(key);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            if (source.CanSeek) source.Position = 0;
            using var fs = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
            await source.CopyToAsync(fs);
        }

        public async Task<Stream> GetOrCreateAsync(string key, Func<Task<Stream>> factory, Func<string, string> keyToPath)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (keyToPath == null) throw new ArgumentNullException(nameof(keyToPath));

            var path = keyToPath(key);
            if (File.Exists(path))
            {
                return File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            }

            using var created = await factory();
            await WriteAsync(key, keyToPath, created);
            return File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
    }
}
