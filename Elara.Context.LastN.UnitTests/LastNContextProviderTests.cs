namespace Elara.Context.LastN.UnitTests;

public class LastNContextProviderTests
{
    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ElaraAi.Context.LastN.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public async Task LastNContextProvider_RespectsN()
    {
        var dir = CreateTempDir();
        var key = "another-key";
        var store = new FileConversationStore(dir, key);
        var provider = new LastNContextProvider(store);

        var t0 = DateTimeOffset.UtcNow.AddMinutes(-1);
        for (int i = 0; i < 5; i++)
        {
            await store.AppendMessageAsync(new ChatMessage { Role = ChatRole.User, Content = $"m{i}", TimestampUtc = t0.AddSeconds(i) });
        }

        var ctx2 = await provider.GetContextAsync("prompt", 2);
        Assert.Equal(2, ctx2.Count);
        Assert.Equal("m3", ctx2[0].Content);
        Assert.Equal("m4", ctx2[1].Content);

        var ctx4 = await provider.GetContextAsync("prompt", 4);
        Assert.Equal(4, ctx4.Count);
        Assert.Equal("m1", ctx4[0].Content);
        Assert.Equal("m4", ctx4[^1].Content);
    }
}
