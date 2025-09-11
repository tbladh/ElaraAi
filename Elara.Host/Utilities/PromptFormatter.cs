using System.Text;
using Elara.Context;

namespace Elara.Host.Utilities
{
    /// <summary>
    /// Renders a simple textual prompt by prefixing prior context messages followed by the current user input.
    /// </summary>
    public static class PromptFormatter
    {
        public static string FormatWithContext(IReadOnlyList<ChatMessage>? context, string userPrompt)
        {
            static string RenderRole(ChatRole role) => role switch
            {
                ChatRole.User => "user",
                ChatRole.Assistant => "assistant",
                ChatRole.System => "system",
                _ => role.ToString().ToLowerInvariant()
            };

            var sb = new StringBuilder();
            if (context != null && context.Count > 0)
            {
                foreach (var m in context)
                {
                    sb.Append(RenderRole(m.Role)).Append(':').Append(' ').AppendLine(m.Content);
                }
            }
            sb.Append("user: ").AppendLine(userPrompt);
            return sb.ToString();
        }
    }
}
