using System.Text.Json;
using System.Text.Json.Serialization;

namespace Elara.Host.Extensions
{
    /// <summary>
    /// JSON-related extensions.
    /// </summary>
    public static class JsonExtensions
    {
        private static readonly JsonSerializerOptions PrettyJsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Serialize an object as indented JSON with sensible defaults.
        /// </summary>
        public static string ToPrettyJson(this object value)
        {
            return JsonSerializer.Serialize(value, PrettyJsonOptions);
        }
    }
}
