using Elara.Core.Interfaces;
using Elara.Logging;

namespace Elara.Host.Utilities
{
    /// <summary>
    /// Plays short system announcements via TTS with structured logging.
    /// </summary>
    public sealed class AnnouncementPlayer
    {
        private readonly ITextToSpeechService _tts;
        private readonly SemaphoreSlim _gate = new(1, 1);

        public AnnouncementPlayer(ITextToSpeechService tts)
        {
            _tts = tts;
        }

        /// <summary>
        /// Plays an announcement line and logs before/after with elapsed time.
        /// </summary>
        /// <param name="category">Logical announcement category (e.g., Startup, Wake, Prompt, Quiescence)</param>
        /// <param name="text">Announcement text to speak</param>
        public async Task PlayAsync(string category, string text)
        {
            var source = string.IsNullOrWhiteSpace(category) ? "Announcements" : $"Announcements/{category}";
            await _gate.WaitAsync();
            try
            {
                Logger.Info(source, $"Speaking announcement: {Truncate(text, 160)}");
                var started = DateTimeOffset.UtcNow;
                await _tts.SpeakToDefaultOutputAsync(text);
                var elapsed = DateTimeOffset.UtcNow - started;
                Logger.Info(source, $"Announcement finished in {elapsed.TotalMilliseconds:F0} ms");
            }
            catch (Exception ex)
            {
                Logger.Warn(source, $"Announcement failed: {ex.Message}");
            }
            finally
            {
                _gate.Release();
            }
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
            return s.Substring(0, Math.Max(0, max - 1)) + "…";
        }
    }
}
