using System.IO;
using System.Threading.Tasks;

namespace ErnestAi.Tools
{
    public interface ICacheService
    {
        Task<bool> TryReadAsync(string key, Func<string, string> keyToPath, Stream destination);
        Task WriteAsync(string key, Func<string, string> keyToPath, Stream source);
        Task<Stream> GetOrCreateAsync(string key, Func<Task<Stream>> factory, Func<string, string> keyToPath);
    }
}
