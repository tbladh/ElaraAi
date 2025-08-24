using System;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using Elara.Host.Core.Interfaces;

namespace Elara.Host.Speech
{
    /// <summary>
    /// Cross-platform placeholder TTS implementation that performs no audio output.
    /// Useful on non-Windows platforms where System.Speech is unavailable.
    /// </summary>
    public sealed class NoOpTextToSpeechService : ITextToSpeechService
    {
        private string? _voice;
        private float _rate = 1.0f;
        private float _pitch = 1.0f;
        private bool _sealed;
        private int _preambleMs;

        public string ProviderName => "NoOp";
        public string CurrentVoice => _voice ?? string.Empty;
        public float SpeechRate => _rate;
        public float SpeechPitch => _pitch;
        public int PreambleMs => _preambleMs;

        public void InitializeOnce(string? voice, float? rate, float? pitch, int? preambleMs = null)
        {
            if (_sealed) return;
            _voice = voice;
            if (rate.HasValue) _rate = rate.Value;
            if (pitch.HasValue) _pitch = pitch.Value;
            if (preambleMs.HasValue) _preambleMs = Math.Max(0, preambleMs.Value);
            _sealed = true;
        }

        public Task<Stream> SpeakAsync(string text)
        {
            // Return an empty wave-like stream to satisfy contracts
            return Task.FromResult<Stream>(new MemoryStream());
        }

        public Task SpeakToFileAsync(string text, string outputFilePath)
        {
            // No output; create an empty file to satisfy expectations
            try
            {
                using var fs = File.Create(outputFilePath);
            }
            catch { }
            return Task.CompletedTask;
        }

        public Task SpeakToDefaultOutputAsync(string text)
            => Task.CompletedTask;

        public Task SpeakToDefaultOutputAsync(string text, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<IEnumerable<string>> GetAvailableVoicesAsync()
            => Task.FromResult<IEnumerable<string>>(Array.Empty<string>());
    }
}
