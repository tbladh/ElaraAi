using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Elara.Context
{
    // Ensures ChatRole is written as its enum name, and can be read from either name or numeric value for backward compatibility.
    public sealed class ChatRoleJsonConverter : JsonConverter<ChatRole>
    {
        public override ChatRole Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var s = reader.GetString();
                if (Enum.TryParse<ChatRole>(s, ignoreCase: true, out var result))
                    return result;
                throw new JsonException($"Invalid ChatRole value: {s}");
            }
            if (reader.TokenType == JsonTokenType.Number)
            {
                if (reader.TryGetInt32(out var n) && Enum.IsDefined(typeof(ChatRole), n))
                    return (ChatRole)n;
                throw new JsonException("Invalid numeric ChatRole value");
            }
            throw new JsonException("Expected string or number for ChatRole");
        }

        public override void Write(Utf8JsonWriter writer, ChatRole value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
