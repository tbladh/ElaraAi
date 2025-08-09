using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ErnestAi.Audio;
using ErnestAi.Configuration;
using ErnestAi.Core.Interfaces;
using ErnestAi.Tools;

namespace ErnestAi.Host
{
    public class AcknowledgementService
    {
        private readonly AppConfig _config;
        private readonly ITextToSpeechService _tts;
        private readonly IAudioPlayer _audioPlayer;
        private readonly ICacheService _cache;
        private readonly IContentHashProvider _hash;
        private byte[]? _cachedBytes;
        private string? _cachePath;

        public bool Enabled => _config.Acknowledgement?.Enabled == true;

        public AcknowledgementService(
            AppConfig config,
            ITextToSpeechService tts,
            IAudioPlayer audioPlayer,
            ICacheService cache,
            IContentHashProvider hash)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _tts = tts ?? throw new ArgumentNullException(nameof(tts));
            _audioPlayer = audioPlayer ?? throw new ArgumentNullException(nameof(audioPlayer));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _hash = hash ?? throw new ArgumentNullException(nameof(hash));
        }

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            if (!Enabled) return;
            var phrase = (_config.Acknowledgement?.Phrase ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(phrase)) return;

            // Include TTS params in the key for uniqueness
            var keyBuilder = new StringBuilder();
            keyBuilder.Append(phrase).Append("|")
                      .Append(_tts.CurrentVoice ?? string.Empty).Append("|")
                      .Append(_tts.SpeechRate.ToString("F2")).Append("|")
                      .Append(_tts.SpeechPitch.ToString("F2"));
            var key = keyBuilder.ToString();
            var hash = _hash.ComputeMd5(key);

            var dir = _config.Acknowledgement?.CacheDirectory;
            if (string.IsNullOrWhiteSpace(dir))
            {
                dir = Path.Combine(AppContext.BaseDirectory, "cache", "tts");
            }

            string Map(string h) => Path.Combine(dir!, h + ".wav");
            _cachePath = Map(hash);

            // Try read from cache to memory
            using (var ms = new MemoryStream())
            {
                var hit = await _cache.TryReadAsync(hash, Map, ms);
                if (!hit)
                {
                    // Generate and write to cache
                    using var wav = await _tts.SpeakAsync(phrase);
                    await _cache.WriteAsync(hash, Map, wav);
                    ms.SetLength(0);
                    await _cache.TryReadAsync(hash, Map, ms);
                }
                _cachedBytes = ms.ToArray();
            }

            Console.WriteLine($"[Ack] Ready (cache: {(_cachePath ?? "<mem>")})");
        }

        public async Task PlayAsync(CancellationToken ct = default)
        {
            if (!Enabled) return;
            if (_cachedBytes == null || _cachedBytes.Length == 0)
            {
                // Fallback: synthesize on the fly
                var phrase = _config.Acknowledgement?.Phrase ?? "Yes";
                using var wav = await _tts.SpeakAsync(phrase);
                await _audioPlayer.PlayWavAsync(wav);
                return;
            }

            using var ms = new MemoryStream(_cachedBytes, writable: false);
            await _audioPlayer.PlayWavAsync(ms);
        }
    }
}
