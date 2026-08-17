using System.Reflection;
using Elara.Host.Intelligence;
using Xunit;

namespace Elara.Intelligence.UnitTests;

public sealed class OllamaLanguageModelServiceTests
{
    [Fact]
    public void ProviderName_IsOllama()
    {
        var service = new OllamaLanguageModelService("http://localhost");
        Assert.Equal("ollama", service.ProviderName);
    }

    [Fact]
    public void ApplyFilters_RemovesMatchesAndIgnoresInvalidPatterns()
    {
        var service = new OllamaLanguageModelService("http://localhost")
        {
            OutputFilters = { "noise", "(" }
        };

        var method = typeof(OllamaLanguageModelService).GetMethod("ApplyFilters", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        var filtered = (string)method!.Invoke(service, new object?[] { "clean noise text" })!;

        Assert.Equal("clean  text", filtered);
    }
}
