using System.Text.Json;
using Elara.Context;
using Xunit;

namespace Elara.Context.UnitTests;

public sealed class ChatRoleJsonConverterTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new ChatRoleJsonConverter() }
    };

    [Fact]
    public void Serialize_WritesEnumName()
    {
        var json = JsonSerializer.Serialize(ChatRole.Assistant, Options);
        Assert.Equal("\"Assistant\"", json);
    }

    [Fact]
    public void Deserialize_StringValue_IgnoresCase()
    {
        var role = JsonSerializer.Deserialize<ChatRole>("\"system\"", Options);
        Assert.Equal(ChatRole.System, role);
    }

    [Fact]
    public void Deserialize_NumericValue_ReturnsMatchingEnum()
    {
        var role = JsonSerializer.Deserialize<ChatRole>("1", Options);
        Assert.Equal(ChatRole.Assistant, role);
    }

    [Fact]
    public void Deserialize_InvalidValue_Throws()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ChatRole>("\"invalid\"", Options));
    }
}
