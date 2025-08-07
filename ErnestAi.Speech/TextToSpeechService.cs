using ErnestAi.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Speech.Synthesis;
using System.Threading.Tasks;

namespace ErnestAi.Speech
{
    /// <summary>
    /// Implementation of ITextToSpeechService using System.Speech for text-to-speech synthesis
    /// </summary>
    public class TextToSpeechService : ITextToSpeechService
    {
        private readonly SpeechSynthesizer _synthesizer;

        /// <summary>
        /// Gets the name of the text-to-speech service provider
        /// </summary>
        public string ProviderName => "System.Speech";

        /// <summary>
        /// Gets or sets the currently selected voice
        /// </summary>
        public string CurrentVoice { get; set; }

        /// <summary>
        /// Gets or sets the speech rate
        /// </summary>
        public float SpeechRate { get; set; } = 1.0f;

        /// <summary>
        /// Gets or sets the speech pitch
        /// </summary>
        public float SpeechPitch { get; set; } = 1.0f;

        /// <summary>
        /// Gets or sets the speech volume
        /// </summary>
        public float SpeechVolume { get; set; } = 100.0f;

        /// <summary>
        /// Creates a new instance of the TextToSpeechService
        /// </summary>
        public TextToSpeechService()
        {
            _synthesizer = new SpeechSynthesizer();
            
            // Set default voice
            try
            {
                _synthesizer.SelectVoiceByHints(VoiceGender.NotSet, VoiceAge.NotSet);
                CurrentVoice = _synthesizer.Voice.Name;
            }
            catch
            {
                // If no voice is available, we'll handle this gracefully
                CurrentVoice = null;
            }
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
                    Console.WriteLine($"Error setting voice: {ex.Message}");
                }
            }

            // Set rate (-10 to 10)
            _synthesizer.Rate = (int)Math.Clamp((SpeechRate - 1) * 10, -10, 10);
            
            // Set volume (0 to 100)
            _synthesizer.Volume = (int)Math.Clamp(SpeechVolume, 0, 100);
        }
    }
}
