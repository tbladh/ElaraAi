namespace Elara.Configuration
{
    /// <summary>
    /// Configuration options for Announcements. Bind this to a configuration section, e.g. "Announcements".
    /// </summary>
    public sealed class AnnouncementsOptions
    {
        /// <summary>Randomized phrases acknowledging the wake word.</summary>
        public IEnumerable<string>? AcknowledgeWakeWord { get; init; }
        /// <summary>Randomized phrases acknowledging a prompt before LLM processing.</summary>
        public IEnumerable<string>? AcknowledgePrompt { get; init; }

        public IEnumerable<string>? AcknowledgeQuiescence { get; init; }

        /// <summary>
        /// List of startup templates to randomly select from. If both this and StartupTemplate are null/empty,
        /// defaults will be used.
        /// </summary>
        public IEnumerable<string>? StartupTemplates { get; init; }
    }
}
