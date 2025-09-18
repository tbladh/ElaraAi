using System;
using System.Collections.Generic;

namespace Elara.Core.Prompts
{
    /// <summary>
    /// Represents a single message to be supplied to a language model.
    /// </summary>
    public sealed class PromptMessage
    {
        /// <summary>Role of the speaker (user, assistant, system, etc.).</summary>
        public required string Role { get; init; }
        /// <summary>Natural language content of the message.</summary>
        public required string Content { get; init; }
        /// <summary>UTC timestamp associated with the message.</summary>
        public required DateTimeOffset TimestampUtc { get; init; }
    }

    /// <summary>
    /// Structured prompt passed to language model services.
    /// Contains the configured system prompt, prior context, the current user turn, and optional hints.
    /// </summary>
    public sealed class StructuredPrompt
    {
        /// <summary>System instructions for the model.</summary>
        public required string SystemPrompt { get; init; }
        /// <summary>Current user message (the turn being answered).</summary>
        public required PromptMessage User { get; init; }
        /// <summary>Ordered list of previous conversation turns.</summary>
        public IReadOnlyList<PromptMessage> Context { get; init; } = Array.Empty<PromptMessage>();
        /// <summary>Optional implementation-specific hints.</summary>
        public IDictionary<string, string>? Hints { get; init; }
    }
}