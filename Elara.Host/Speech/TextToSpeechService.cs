using Elara.Core.Interfaces;
using System.Speech.Synthesis;
using System.Threading;
using System.Runtime.Versioning;

namespace Elara.Host.Speech
{
    /// <summary>
    /// Implementation of ITextToSpeechService using System.Speech for text-to-speech synthesis
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class TextToSpeechService : ITextToSpeechService
    {
        private readonly SpeechSynthesizer _synthesizer;
        private bool _sealed;
        private int _preambleMs;

        /// <summary>
        /// Gets the name of the text-to-speech service provider
        /// </summary>
        public string ProviderName => "System.Speech";

        /// <summary>
        /// Gets or sets the currently selected voice
        /// </summary>
        private string _currentVoice = string.Empty;
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
        /// Duration in ms of a silent preamble inserted before each utterance.
        /// </summary>
        public int PreambleMs => _preambleMs;

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
                    _currentVoice = _synthesizer.Voice?.Name ?? string.Empty;
                }
                else
                {
                    // Fallback to hints, though on some systems this may still be the same as default
                    _synthesizer.SelectVoiceByHints(VoiceGender.NotSet, VoiceAge.NotSet);
                    _currentVoice = _synthesizer.Voice?.Name ?? string.Empty;
                }
            }
            catch
            {
                // If no voice is available, keep empty
                _currentVoice = string.Empty;
            }
        }

        /// <summary>
        /// Initialize this service exactly once and seal parameters for the process lifetime.
        /// Subsequent calls are no-ops.
        /// </summary>
        public void InitializeOnce(string? voice, float? rate, float? pitch, int? preambleMs = null)
        {
            if (_sealed) return;
            if (!string.IsNullOrWhiteSpace(voice))
            {
                _currentVoice = voice;
            }
            if (rate.HasValue) _speechRate = rate.Value;
            if (pitch.HasValue) _speechPitch = pitch.Value;
            if (preambleMs.HasValue) _preambleMs = Math.Max(0, preambleMs.Value);
            _sealed = true;
            Console.WriteLine($"[TTS] Initialized (voice='{_currentVoice ?? "<none>"}', rate={_speechRate:F2}, pitch={_speechPitch:F2}, preambleMs={_preambleMs})");
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
                var builder = BuildPrompt(text);
                _synthesizer.Speak(builder);
                
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
                var builder = BuildPrompt(text);
                _synthesizer.Speak(builder);
                
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
                var builder = BuildPrompt(text);
                _synthesizer.Speak(builder);
            });
        }

        /// <summary>
        /// Converts text to speech and plays it through the default audio output device. Supports cancellation.
        /// </summary>
        public Task SpeakToDefaultOutputAsync(string text, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            // Ensure output and settings on thread where we start async speak
            void OnCompleted(object? s, SpeakCompletedEventArgs e)
            {
                _synthesizer.SpeakCompleted -= OnCompleted;
                if (e.Cancelled)
                {
                    tcs.TrySetCanceled(cancellationToken);
                }
                else if (e.Error != null)
                {
                    tcs.TrySetException(e.Error);
                }
                else
                {
                    tcs.TrySetResult(true);
                }
            }

            // Register cancellation to abort ongoing speech
            var reg = cancellationToken.Register(() =>
            {
                try { _synthesizer.SpeakAsyncCancelAll(); } catch { }
            });

            try
            {
                _synthesizer.SetOutputToDefaultAudioDevice();
                ApplyVoiceSettings();
                _synthesizer.SpeakCompleted += OnCompleted;
                var builder = BuildPrompt(text);
                _synthesizer.SpeakAsync(builder);
            }
            catch (Exception ex)
            {
                reg.Dispose();
                _synthesizer.SpeakCompleted -= OnCompleted;
                return Task.FromException(ex);
            }

            return tcs.Task.ContinueWith(t =>
            {
                reg.Dispose();
                return t; // unwrap
            }).Unwrap();
        }

        /// <summary>
        /// Gets the available voices for this service
        /// </summary>
        public Task<IEnumerable<string>> GetAvailableVoicesAsync()
        {
            return Task.FromResult(
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
            _synthesizer.Rate = (int)Math.Clamp((_speechRate - 1) * 10, -10, 10);
            
            // Set volume (0 to 100)
            _synthesizer.Volume = (int)Math.Clamp(_speechVolume, 0, 100);
        }

        private PromptBuilder BuildPrompt(string text)
        {
            var pb = new PromptBuilder();
            if (_preambleMs > 0)
            {
                pb.AppendBreak(TimeSpan.FromMilliseconds(_preambleMs));
            }
            pb.AppendText(text ?? string.Empty);
            return pb;
        }
    }
}
