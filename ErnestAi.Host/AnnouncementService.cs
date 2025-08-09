using System;
using System.Collections.Concurrent;
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
    public class AnnouncementService
    {
        private readonly ITextToSpeechService _tts;
        private readonly IAudioPlayer _audioPlayer;
        private readonly ICacheService _cache;
        private readonly IContentHashProvider _hash;
        private readonly ConcurrentDictionary<string, byte[]> _memoryCache = new();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

        // Key policy set at initialization
        private string? _voice;
        private double _rate;
        private double _pitch;
        private string? _cacheDir;
        private volatile bool _initialized;

        public AnnouncementService(
            AppConfig config,
            ITextToSpeechService tts,
            IAudioPlayer audioPlayer,
            ICacheService cache,
            IContentHashProvider hash)
        {
            _tts = tts ?? throw new ArgumentNullException(nameof(tts));
            _audioPlayer = audioPlayer ?? throw new ArgumentNullException(nameof(audioPlayer));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _hash = hash ?? throw new ArgumentNullException(nameof(hash));
        }

        public Task InitializeAsync(string voice, double rate, double pitch, string? cacheDirectory = null, CancellationToken ct = default)
        {
            _voice = voice ?? string.Empty;
            _rate = rate;
            _pitch = pitch;
            _cacheDir = string.IsNullOrWhiteSpace(cacheDirectory)
                ? Path.Combine(AppContext.BaseDirectory, "cache", "tts")
                : cacheDirectory;
            Directory.CreateDirectory(_cacheDir);
            _initialized = true;
            Console.WriteLine($"[Ann] Initialized (dir={_cacheDir}, voice='{_voice}', rate={_rate:F2}, pitch={_pitch:F2})");
            return Task.CompletedTask;
        }

        public async Task PreloadAsync(string phrase, CancellationToken ct = default)
        {
            if (!_initialized) throw new InvalidOperationException("AnnouncementService not initialized. Call InitializeAsync first.");
            var key = BuildKey(phrase);
            if (_memoryCache.ContainsKey(key)) return;

            var path = MapToPath(key);
            using var ms = new MemoryStream();
            if (!await _cache.TryReadAsync(key, _ => path, ms))
            {
                // synthesize and write to cache
                using var wav = await _tts.SpeakAsync(phrase);
                await _cache.WriteAsync(key, _ => path, wav);
                ms.SetLength(0);
                await _cache.TryReadAsync(key, _ => path, ms);
            }
            _memoryCache[key] = ms.ToArray();
            Console.WriteLine($"[Ann] Preloaded '{phrase}' -> {path}");
        }

        public async Task PlayAsync(string phrase, CancellationToken ct = default)
        {
            if (!_initialized) throw new InvalidOperationException("AnnouncementService not initialized. Call InitializeAsync first.");
            var key = BuildKey(phrase);

            if (!_memoryCache.TryGetValue(key, out var bytes) || bytes.Length == 0)
            {
                var sem = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
                await sem.WaitAsync(ct);
                try
                {
                    if (!_memoryCache.TryGetValue(key, out bytes) || bytes.Length == 0)
                    {
                        var path = MapToPath(key);
                        using var ms = new MemoryStream();
                        var hit = await _cache.TryReadAsync(key, _ => path, ms);
                        if (!hit)
                        {
                            using var wav = await _tts.SpeakAsync(phrase);
                            await _cache.WriteAsync(key, _ => path, wav);
                            ms.SetLength(0);
                            await _cache.TryReadAsync(key, _ => path, ms);
                        }
                        bytes = ms.ToArray();
                        _memoryCache[key] = bytes;
                    }
                }
                finally
                {
                    sem.Release();
                }
            }

            using var playMs = new MemoryStream(bytes, writable: false);
            await _audioPlayer.PlayWavAsync(playMs);
        }

        private string BuildKey(string phrase)
        {
            if (phrase == null) phrase = string.Empty;
            var keyBuilder = new StringBuilder();
            keyBuilder.Append(phrase).Append("|")
                      .Append(_voice ?? string.Empty).Append("|")
                      .Append(_rate.ToString("F2")).Append("|")
                      .Append(_pitch.ToString("F2"));
            var key = keyBuilder.ToString();
            return _hash.ComputeMd5(key);
        }

        private string MapToPath(string keyHash)
        {
            var dir = _cacheDir ?? Path.Combine(AppContext.BaseDirectory, "cache", "tts");
            return Path.Combine(dir, keyHash + ".wav");
        }
    }
}
