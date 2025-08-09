using ErnestAi.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Speech.Synthesis;
using System.Threading.Tasks;
using System.Linq;

namespace ErnestAi.Speech
{
    /// <summary>
    /// Implementation of ITextToSpeechService using System.Speech for text-to-speech synthesis
    /// </summary>
    public class TextToSpeechService : ITextToSpeechService
    {
        private readonly SpeechSynthesizer _synthesizer;
        private bool _sealed;

        /// <summary>
        /// Gets the name of the text-to-speech service provider
        /// </summary>
        public string ProviderName => "System.Speech";

        /// <summary>
        /// Gets or sets the currently selected voice
        /// </summary>
        private string _currentVoice;
        public string CurrentVoice => _currentVoice;

        /// <summary>
        /// Gets or sets the speech rate
        /// </summary>
        private float _speechRate = 1.0f;
        public float SpeechRate => _speechRate;

        /// <summary>
        /// Gets or sets the speech pitch
        /// </summary>
        private float _speechPitch = 1.0f;
        public float SpeechPitch => _speechPitch;

        /// <summary>
        /// Gets or sets the speech volume
        /// </summary>
        private float _speechVolume = 100.0f;
        public float SpeechVolume => _speechVolume;

        /// <summary>
        /// Creates a new instance of the TextToSpeechService
        /// </summary>
        public TextToSpeechService()
        {
            _synthesizer = new SpeechSynthesizer();
            
            // Set default voice
            try
            {
                // Prefer current synthesizer default; otherwise pick the first installed voice
                var installed = _synthesizer.GetInstalledVoices();
                if (installed != null && installed.Count > 0)
                {
                    var first = installed.First().VoiceInfo.Name;
                    _synthesizer.SelectVoice(first);
                    _currentVoice = _synthesizer.Voice?.Name;
                }
                else
                {
                    // Fallback to hints, though on some systems this may still be the same as default
                    _synthesizer.SelectVoiceByHints(VoiceGender.NotSet, VoiceAge.NotSet);
                    _currentVoice = _synthesizer.Voice?.Name;
                }
            }
            catch
            {
                // If no voice is available, we'll handle this gracefully
                _currentVoice = null;
            }
        }

        /// <summary>
        /// Initialize this service exactly once and seal parameters for the process lifetime.
        /// Subsequent calls are no-ops.
        /// </summary>
        public void InitializeOnce(string? voice, float? rate, float? pitch)
        {
            if (_sealed) return;
            if (!string.IsNullOrWhiteSpace(voice))
            {
                _currentVoice = voice;
            }
            if (rate.HasValue) _speechRate = rate.Value;
            if (pitch.HasValue) _speechPitch = pitch.Value;
            _sealed = true;
            Console.WriteLine($"[TTS] Initialized (voice='{_currentVoice ?? "<none>"}', rate={_speechRate:F2}, pitch={_speechPitch:F2})");
        }

        /// <summary>
        /// Converts text to speech and returns the audio as a stream
        /// </summary>
        public Task<Stream> SpeakAsync(string text)
        {
            return Task.Run(() =>
            {
                var stream = new MemoryStream();
                _synthesizer.SetOutputToWaveStream(stream);
                
                ApplyVoiceSettings();
                _synthesizer.Speak(text);
                
                stream.Position = 0;
                return (Stream)stream;
            });
        }

        /// <summary>
        /// Converts text to speech and saves the audio to a file
        /// </summary>
        public Task SpeakToFileAsync(string text, string outputFilePath)
        {
            return Task.Run(() =>
            {
                _synthesizer.SetOutputToWaveFile(outputFilePath);
                
                ApplyVoiceSettings();
                _synthesizer.Speak(text);
                
                _synthesizer.SetOutputToNull();
            });
        }

        /// <summary>
        /// Converts text to speech and plays it through the default audio output device
        /// </summary>
        public Task SpeakToDefaultOutputAsync(string text)
        {
            return Task.Run(() =>
            {
                _synthesizer.SetOutputToDefaultAudioDevice();
                
                ApplyVoiceSettings();
                _synthesizer.Speak(text);
            });
        }

        /// <summary>
        /// Gets the available voices for this service
        /// </summary>
        public Task<IEnumerable<string>> GetAvailableVoicesAsync()
        {
            return Task.FromResult<IEnumerable<string>>(
                _synthesizer.GetInstalledVoices()
                    .Select(v => v.VoiceInfo.Name)
            );
        }

        /// <summary>
        /// Applies the current voice settings to the synthesizer
        /// </summary>
        private void ApplyVoiceSettings()
        {
            // Set voice if specified
            if (!string.IsNullOrEmpty(CurrentVoice))
            {
                try
                {
                    _synthesizer.SelectVoice(CurrentVoice);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TTS] Error setting voice '{CurrentVoice}': {ex.Message}");
                    // Try case-insensitive match among installed voices
                    try
                    {
                        var match = _synthesizer.GetInstalledVoices()
                            .Select(v => v.VoiceInfo.Name)
                            .FirstOrDefault(name => string.Equals(name, CurrentVoice, StringComparison.OrdinalIgnoreCase));
                        if (!string.IsNullOrEmpty(match))
                        {
                            _synthesizer.SelectVoice(match);
                            _currentVoice = match;
                        }
                        else
                        {
                            // Fallback to first installed voice
                            var first = _synthesizer.GetInstalledVoices().FirstOrDefault()?.VoiceInfo.Name;
                            if (!string.IsNullOrEmpty(first))
                            {
                                _synthesizer.SelectVoice(first);
                                _currentVoice = first;
                                Console.WriteLine($"[TTS] Falling back to voice '{first}'.");
                            }
                        }
                    }
                    catch (Exception ex2)
                    {
                        Console.WriteLine($"[TTS] Voice fallback failed: {ex2.Message}");
                    }
                }
            }

            // Set rate (-10 to 10)
            _synthesizer.Rate = (int)Math.Clamp(((_speechRate) - 1) * 10, -10, 10);
            
            // Set volume (0 to 100)
            _synthesizer.Volume = (int)Math.Clamp(_speechVolume, 0, 100);
        }
    }
}
