using Elara.Context.Contracts;
using Elara.Context;

namespace Elara.Context.UnitTests;

public class ConversationComponentsTests
{
    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ElaraAi.Context.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public async Task FileConversationStore_EncryptsAndDecrypts_Roundtrip()
    {
        var dir = CreateTempDir();
        var key = "test-key";
        var store = new FileConversationStore(dir, key);

        var now = DateTimeOffset.UtcNow;
        var msg1 = new ChatMessage { Role = ChatRole.User, Content = "hello", TimestampUtc = now };
        var msg2 = new ChatMessage { Role = ChatRole.Assistant, Content = "world", TimestampUtc = now.AddSeconds(1) };

        await store.AppendMessageAsync(msg1);
        await store.AppendMessageAsync(msg2);

        var tail = await store.ReadTailAsync(2);
        Assert.Equal(2, tail.Count);
        Assert.Equal("hello", tail[0].Content);
        Assert.Equal("world", tail[1].Content);

        // Ensure files are not plaintext JSON of ChatMessage
        var files = Directory.GetFiles(dir, "*.json");
        Assert.True(files.Length >= 2);
        var sample = await File.ReadAllTextAsync(files[0]);
        Assert.DoesNotContain("\"content\":\"hello\"", sample, StringComparison.OrdinalIgnoreCase);
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

    private sealed class CapturingProvider : IContextProvider
    {
        public string? LastPrompt { get; private set; }
        public int LastN { get; private set; }
        private readonly IReadOnlyList<ChatMessage> _toReturn;
        public CapturingProvider(params ChatMessage[] msgs) { _toReturn = msgs; }
        public Task<IReadOnlyList<ChatMessage>> GetContextAsync(string currentPrompt, int n, System.Threading.CancellationToken ct = default)
        {
            LastPrompt = currentPrompt;
            LastN = n;
            return Task.FromResult(_toReturn);
        }
    }

    [Fact]
    public async Task PromptBuilder_IncludesNowUtc_And_Context()
    {
        var sys = new SystemPromptProvider("sys");
        var msg = new ChatMessage { Role = ChatRole.System, Content = "ctx", TimestampUtc = DateTimeOffset.UtcNow };
        var cap = new CapturingProvider(msg);
        var builder = new PromptBuilder(sys, new[] { cap });

        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var prompt = await builder.BuildAsync("user-question", 7);
        var after = DateTimeOffset.UtcNow.AddSeconds(1);

        Assert.Equal("sys", prompt.System);
        Assert.Equal("user-question", prompt.UserInput);
        Assert.Single(prompt.Context);
        Assert.InRange(prompt.NowUtc, before, after);
        Assert.Equal("user-question", cap.LastPrompt);
        Assert.Equal(7, cap.LastN);
    }
}
