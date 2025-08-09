using System.IO;
using System.Threading.Tasks;

namespace ErnestAi.Audio
{
    public interface IAudioPlayer
    {
        Task PlayWavAsync(Stream wavStream);
    }
}
