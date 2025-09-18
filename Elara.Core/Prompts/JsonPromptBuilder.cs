using System;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Elara.Core.Prompts
{
    public sealed class PromptSerializationResult
    {
        public required string PromptJson { get; init; }
        public required string LogJson { get; init; }
    }

    public static class JsonPromptBuilder
    {
        private sealed class JsonMessage
        {
            [JsonPropertyName("role")] public required string Role { get; init; }
            [JsonPropertyName("content")] public required string Content { get; init; }
            [JsonPropertyName("timestampUtc")] public required string TimestampUtc { get; init; }
        }

        private sealed class JsonPrompt
        {
            [JsonPropertyName("systemPrompt")] public required string SystemPrompt { get; init; }
            [JsonPropertyName("history")] public required IReadOnlyList<JsonMessage> History { get; init; }
            [JsonPropertyName("user")] public required JsonMessage User { get; init; }
            [JsonPropertyName("hints")] public required IReadOnlyDictionary<string, string> Hints { get; init; }
        }

        public static PromptSerializationResult Build(StructuredPrompt prompt)
        {
            if (prompt is null)
            {
                throw new ArgumentNullException(nameof(prompt));
            }

            var history = new List<JsonMessage>();
            if (prompt.Context != null)
            {
                foreach (var message in prompt.Context)
                {
                    history.Add(ToJsonMessage(message));
                }
            }

            var userMessage = ToJsonMessage(prompt.User);
            var hints = prompt.Hints != null && prompt.Hints.Count > 0
                ? new Dictionary<string, string>(prompt.Hints)
                : new Dictionary<string, string>();

            var payload = new JsonPrompt
            {
                SystemPrompt = prompt.SystemPrompt,
                History = history,
                User = userMessage,
                Hints = hints
            };

            var rawOptions = new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = false
            };
            var promptJson = JsonSerializer.Serialize(payload, rawOptions);

            var logOptions = new JsonSerializerOptions(rawOptions)
            {
                WriteIndented = true
            };
            var logJson = JsonSerializer.Serialize(payload, logOptions);

            return new PromptSerializationResult
            {
                PromptJson = promptJson,
                LogJson = logJson
            };
        }

        private static JsonMessage ToJsonMessage(PromptMessage message)
        {
            if (message is null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return new JsonMessage
            {
                Role = message.Role,
                Content = message.Content,
                TimestampUtc = message.TimestampUtc.ToUniversalTime().ToString("o")
            };
        }
    }
}
