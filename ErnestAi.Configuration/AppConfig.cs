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
        /// Configuration for language model
        /// </summary>
        public LanguageModelConfig LanguageModel { get; set; } = new LanguageModelConfig();
        
        /// <summary>
        /// Configuration for text-to-speech
        /// </summary>
        public TextToSpeechConfig TextToSpeech { get; set; } = new TextToSpeechConfig();
        
        /// <summary>
        /// Configuration for audio processing
        /// </summary>
        public AudioConfig Audio { get; set; } = new AudioConfig();
        
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
            
            // Validate language model configuration
            if (string.IsNullOrWhiteSpace(config.LanguageModel?.ServiceUrl))
            {
                throw new InvalidOperationException($"Language model service URL is not configured in '{filePath}'.");
            }
            
            if (string.IsNullOrWhiteSpace(config.LanguageModel?.ModelName))
            {
                throw new InvalidOperationException($"Language model name is not configured in '{filePath}'.");
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
        public string ModelFileName { get; set; } = "ggml-base.en.bin";
        
        /// <summary>
        /// The URL to download the model from if it doesn't exist locally
        /// </summary>
        public string ModelUrl { get; set; } = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.en.bin";
        
        /// <summary>
        /// Whether to output transcribed text to the console
        /// </summary>
        public bool OutputTranscriptionToConsole { get; set; } = true;
    }
    
    /// <summary>
    /// Configuration for language model
    /// </summary>
    public class LanguageModelConfig
    {
        /// <summary>
        /// The base URL for the language model service
        /// </summary>
        public string ServiceUrl { get; set; } = "http://127.0.0.1:11434";
        
        /// <summary>
        /// The model to use for language model inference
        /// </summary>
        public string ModelName { get; set; } = "llama2";

        /// <summary>
        /// Warmup configuration for language model providers
        /// </summary>
        public WarmupConfig Warmup { get; set; } = new WarmupConfig();
    }
    
    /// <summary>
    /// Configuration for text-to-speech
    /// </summary>
    public class TextToSpeechConfig
    {
        /// <summary>
        /// The preferred voice name to use for text-to-speech
        /// </summary>
        public string PreferredVoice { get; set; } = "";
    }
    
    /// <summary>
    /// Configuration for audio processing
    /// </summary>
    public class AudioConfig
    {
        /// <summary>
        /// The sample rate to use for audio recording
        /// </summary>
        public int SampleRate { get; set; } = 16000;
        
        /// <summary>
        /// The number of channels to use for audio recording
        /// </summary>
        public int Channels { get; set; } = 1;
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
}
