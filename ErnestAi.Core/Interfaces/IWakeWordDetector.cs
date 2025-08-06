namespace ErnestAi.Core.Interfaces
{
    /// <summary>
    /// Interface for wake word detection components
    /// </summary>
    public interface IWakeWordDetector
    {
        /// <summary>
        /// Event triggered when the wake word is detected
        /// </summary>
        event EventHandler<WakeWordDetectedEventArgs> WakeWordDetected;
        
        /// <summary>
        /// Starts listening for the wake word
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the listening operation</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task StartListeningAsync(CancellationToken cancellationToken);
        
        /// <summary>
        /// Stops listening for the wake word
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        Task StopListeningAsync();
        
        /// <summary>
        /// Gets or sets the wake word to listen for
        /// </summary>
        string WakeWord { get; set; }
        
        /// <summary>
        /// Gets or sets the confidence threshold for wake word detection
        /// </summary>
        float ConfidenceThreshold { get; set; }
    }
    
    /// <summary>
    /// Event arguments for wake word detection
    /// </summary>
    public class WakeWordDetectedEventArgs : EventArgs
    {
        /// <summary>
        /// The detected wake word
        /// </summary>
        public string DetectedText { get; set; }
        
        /// <summary>
        /// The confidence score of the detection
        /// </summary>
        public float Confidence { get; set; }
        
        /// <summary>
        /// The timestamp when the wake word was detected
        /// </summary>
        public DateTime Timestamp { get; set; }
    }
}
