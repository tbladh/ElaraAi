namespace ErnestAi.Core.Interfaces.Configuration
{
    /// <summary>
    /// Interface for audio configuration settings
    /// </summary>
    public interface IAudioConfiguration
    {
        /// <summary>
        /// Gets or sets the input device name
        /// </summary>
        string InputDeviceName { get; set; }
        
        /// <summary>
        /// Gets or sets the output device name
        /// </summary>
        string OutputDeviceName { get; set; }
        
        /// <summary>
        /// Gets or sets the sample rate for audio recording
        /// </summary>
        int SampleRate { get; set; }
        
        /// <summary>
        /// Gets or sets the number of bits per sample
        /// </summary>
        int BitsPerSample { get; set; }
        
        /// <summary>
        /// Gets or sets the number of audio channels
        /// </summary>
        int Channels { get; set; }
        
        /// <summary>
        /// Gets or sets the recording volume level (0.0 to 1.0)
        /// </summary>
        float RecordingVolume { get; set; }
        
        /// <summary>
        /// Gets or sets the playback volume level (0.0 to 1.0)
        /// </summary>
        float PlaybackVolume { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether to use voice activity detection
        /// </summary>
        bool UseVoiceActivityDetection { get; set; }
        
        /// <summary>
        /// Gets or sets the silence threshold for voice activity detection
        /// </summary>
        float SilenceThreshold { get; set; }
        
        /// <summary>
        /// Gets or sets the maximum recording duration in seconds
        /// </summary>
        int MaxRecordingDurationSeconds { get; set; }
    }
}
