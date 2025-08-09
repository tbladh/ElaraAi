using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace ErnestAi.Configuration
{
    /// <summary>
    /// Configuration for the ErnestAi application
    /// </summary>
    public class AppConfig
    {
        /// <summary>
        /// Configuration for wake word detection
        /// </summary>
        public WakeWordConfig WakeWord { get; set; } = new WakeWordConfig();
        
        /// <summary>
        /// Configuration for speech-to-text
        /// </summary>
        public SpeechToTextConfig SpeechToText { get; set; } = new SpeechToTextConfig();
        
        /// <summary>
        /// Configured language model entries ordered by Priority
        /// </summary>
        public LanguageModelEntry[] LanguageModels { get; set; } = Array.Empty<LanguageModelEntry>();
        
        /// <summary>
        /// Configuration for text-to-speech
        /// </summary>
        public TextToSpeechConfig TextToSpeech { get; set; } = new TextToSpeechConfig();
        
        /// <summary>
        /// Configuration for audio processing
        /// </summary>
        public AudioConfig Audio { get; set; } = new AudioConfig();

        /// <summary>
        /// Warmup configuration (moved to root to be provider-centric)
        /// </summary>
        public WarmupConfig Warmup { get; set; } = new WarmupConfig();
        
        /// <summary>
        /// Acknowledgement word/phrase configuration
        /// </summary>
        public AcknowledgementConfig Acknowledgement { get; set; } = new AcknowledgementConfig();
        
        /// <summary>
        /// Loads configuration from the specified file
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when configuration file is missing or invalid</exception>
        public static async Task<AppConfig> LoadAsync(string filePath = "appsettings.json")
        {
            if (!File.Exists(filePath))
            {
                throw new InvalidOperationException($"Configuration file '{filePath}' not found. Please create a valid configuration file.");
            }

            using var stream = File.OpenRead(filePath);
            var config = await JsonSerializer.DeserializeAsync<AppConfig>(stream) 
                ?? throw new InvalidOperationException($"Failed to deserialize configuration from '{filePath}'.");
            
            // Validate required configuration values
            ValidateConfig(config, filePath);            
            return config;
        }
        
        /// <summary>
        /// Saves configuration to the specified file
        /// </summary>
        public static async Task SaveAsync(AppConfig config, string filePath = "appsettings.json")
        {
            using var stream = File.Create(filePath);
            var options = new JsonSerializerOptions { WriteIndented = true };
            await JsonSerializer.SerializeAsync(stream, config, options);
        }
        
        /// <summary>
        /// Validates that all required configuration values are present
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when required configuration is missing</exception>
        private static void ValidateConfig(AppConfig config, string filePath)
        {
            // Validate wake word configuration
            if (string.IsNullOrWhiteSpace(config.WakeWord?.WakeWord))
            {
                throw new InvalidOperationException($"Wake word is not configured in '{filePath}'. Please set a valid wake word.");
            }
            
            if (string.IsNullOrWhiteSpace(config.WakeWord.ModelFileName))
            {
                throw new InvalidOperationException($"Wake word model filename is not configured in '{filePath}'.");
            }
            
            if (string.IsNullOrWhiteSpace(config.WakeWord.ModelUrl))
            {
                throw new InvalidOperationException($"Wake word model URL is not configured in '{filePath}'.");
            }
            
            // Validate speech-to-text configuration
            if (string.IsNullOrWhiteSpace(config.SpeechToText?.ModelFileName))
            {
                throw new InvalidOperationException($"Speech-to-text model filename is not configured in '{filePath}'.");
            }
            
            // Validate language models list
            if (config.LanguageModels == null || config.LanguageModels.Length == 0)
            {
                throw new InvalidOperationException($"No LanguageModels configured in '{filePath}'.");
            }
            foreach (var lm in config.LanguageModels)
            {
                if (string.IsNullOrWhiteSpace(lm?.Name))
                    throw new InvalidOperationException($"A LanguageModels entry is missing 'Name' in '{filePath}'.");
                if (string.IsNullOrWhiteSpace(lm.Provider))
                    throw new InvalidOperationException($"LanguageModels '{lm.Name}' is missing 'Provider'.");
                if (string.IsNullOrWhiteSpace(lm.ServiceUrl))
                    throw new InvalidOperationException($"LanguageModels '{lm.Name}' missing 'ServiceUrl' in '{filePath}'.");
                if (string.IsNullOrWhiteSpace(lm.ModelName))
                    throw new InvalidOperationException($"LanguageModels '{lm.Name}' missing 'ModelName' in '{filePath}'.");
                if (string.IsNullOrWhiteSpace(lm.SystemPrompt))
                    throw new InvalidOperationException($"LanguageModels '{lm.Name}' missing 'SystemPrompt' in '{filePath}'.");
                if (lm.Priority <= 0)
                    throw new InvalidOperationException($"LanguageModels '{lm.Name}' must have Priority >= 1 in '{filePath}'.");
            }

            // Validate audio configuration
            if (config.Audio == null || config.Audio.SampleRate <= 0)
            {
                throw new InvalidOperationException($"Audio sample rate must be specified and greater than 0 in '{filePath}'.");
            }
            if (config.Audio.Channels <= 0)
            {
                throw new InvalidOperationException($"Audio channels must be specified and greater than 0 in '{filePath}'.");
            }

            // Validate TTS configuration
            if (string.IsNullOrWhiteSpace(config.TextToSpeech?.PreferredVoice))
            {
                throw new InvalidOperationException($"Preferred voice for text-to-speech is not configured in '{filePath}'.");
            }

            // Validate acknowledgement configuration
            if (config.Acknowledgement != null && config.Acknowledgement.Enabled)
            {
                if (string.IsNullOrWhiteSpace(config.Acknowledgement.Phrase))
                {
                    throw new InvalidOperationException($"Acknowledgement.Phrase must be set when acknowledgement is enabled in '{filePath}'.");
                }
                if (config.Acknowledgement.PauseAfterMs < 0)
                {
                    throw new InvalidOperationException($"Acknowledgement.PauseAfterMs must be >= 0 in '{filePath}'.");
                }
            }
        }
    }
    
    /// <summary>
    /// Configuration for wake word detection
    /// </summary>
    public class WakeWordConfig
    {
        /// <summary>
        /// The wake word to listen for
        /// </summary>
        public string WakeWord { get; set; }
        
        /// <summary>
        /// The model file name for wake word detection
        /// </summary>
        public string ModelFileName { get; set; }
        
        /// <summary>
        /// The URL to download the model from if it doesn't exist locally
        /// </summary>
        public string ModelUrl { get; set; }
    }
    
    /// <summary>
    /// Configuration for speech-to-text
    /// </summary>
    public class SpeechToTextConfig
    {
        /// <summary>
        /// The model file name for speech-to-text
        /// </summary>
        public string ModelFileName { get; set; }
        
        /// <summary>
        /// The URL to download the model from if it doesn't exist locally
        /// </summary>
        public string ModelUrl { get; set; }
        
        /// <summary>
        /// Whether to output transcribed text to the console
        /// </summary>
        public bool OutputTranscriptionToConsole { get; set; }
    }
    
    /// <summary>
    /// A single language model candidate definition
    /// </summary>
    public class LanguageModelEntry
    {
        /// <summary>
        /// Friendly name to distinguish variants of the same model/provider
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Provider key, e.g. "ollama"
        /// </summary>
        public string Provider { get; set; }

        /// <summary>
        /// Base URL for the provider's API
        /// </summary>
        public string ServiceUrl { get; set; }

        /// <summary>
        /// Model identifier at the provider
        /// </summary>
        public string ModelName { get; set; }

        /// <summary>
        /// System prompt to use with this candidate
        /// </summary>
        public string SystemPrompt { get; set; }

        /// <summary>
        /// Priority (1 is highest)
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Optional regex filters applied to model outputs (as strings)
        /// </summary>
        public string[] Filter { get; set; }
    }
    
    /// <summary>
    /// Configuration for text-to-speech
    /// </summary>
    public class TextToSpeechConfig
    {
        /// <summary>
        /// The preferred voice name to use for text-to-speech
        /// </summary>
        public string PreferredVoice { get; set; }
    }
    
    /// <summary>
    /// Configuration for audio processing
    /// </summary>
    public class AudioConfig
    {
        /// <summary>
        /// The sample rate to use for audio recording
        /// </summary>
        public int SampleRate { get; set; }
        
        /// <summary>
        /// The number of channels to use for audio recording
        /// </summary>
        public int Channels { get; set; }
    }

    /// <summary>
    /// Warmup configuration container
    /// </summary>
    public class WarmupConfig
    {
        /// <summary>
        /// Enables or disables the warmup subsystem
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Default interval (in seconds) used when a provider does not specify one
        /// </summary>
        public int DefaultIntervalSeconds { get; set; } = 180;

        /// <summary>
        /// Per-provider warmup settings
        /// </summary>
        public ProviderWarmupConfig[] Providers { get; set; } = Array.Empty<ProviderWarmupConfig>();
    }

    /// <summary>
    /// Per-provider warmup settings
    /// </summary>
    public class ProviderWarmupConfig
    {
        /// <summary>
        /// Provider name (e.g., "ollama", "openai")
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Enables or disables warmup for this provider
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Optional interval (in seconds); if null or 0, uses DefaultIntervalSeconds
        /// </summary>
        public int? IntervalSeconds { get; set; }
    }

    /// <summary>
    /// Configuration for acknowledgement playback
    /// </summary>
    public class AcknowledgementConfig
    {
        /// <summary>
        /// Enables or disables acknowledgement playback on wake
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// The acknowledgement word or phrase (e.g., "Yes")
        /// </summary>
        public string Phrase { get; set; } = "Yes";

        /// <summary>
        /// Optional cache directory for generated audio files
        /// </summary>
        public string? CacheDirectory { get; set; } = null;

        /// <summary>
        /// Milliseconds to pause after playback before recording starts
        /// </summary>
        public int PauseAfterMs { get; set; } = 500;
    }
}
