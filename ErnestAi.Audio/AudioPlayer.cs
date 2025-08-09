using System.IO;
using System.Threading.Tasks;
using NAudio.Wave;

namespace ErnestAi.Audio
{
    public class AudioPlayer : IAudioPlayer
    {
        public async Task PlayWavAsync(Stream wavStream)
        {
            if (wavStream == null) return;
            if (wavStream.CanSeek) wavStream.Position = 0;

            // Copy to an owned MemoryStream so the caller can dispose their stream safely
            using var ms = new MemoryStream();
            await wavStream.CopyToAsync(ms).ConfigureAwait(false);
            ms.Position = 0;

            using var reader = new WaveFileReader(ms);
            using var output = new WaveOutEvent();
            var tcs = new TaskCompletionSource<bool>();
            output.Init(reader);
            output.PlaybackStopped += (s, e) =>
            {
                // NAudio calls this on a worker thread; ensure TCS only set once
                if (!tcs.Task.IsCompleted)
                    tcs.SetResult(true);
            };
            output.Play();
            await tcs.Task.ConfigureAwait(false);
        }
    }
}
