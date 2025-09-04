using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Elara.Context;
using Elara.Context.Contracts;
using Xunit;

namespace Elara.Context.UnitTests;

public class FileConversationStoreTests
{
    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ElaraAi.Context.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public async Task FileConversationStore_Plaintext_Roundtrip()
    {
        var dir = CreateTempDir();
        string? key = null; // disable encryption
        var store = new FileConversationStore(dir, key);

        var now = DateTimeOffset.UtcNow;
        var msg1 = new ChatMessage { Role = ChatRole.User, Content = "plain-hello", TimestampUtc = now };
        var msg2 = new ChatMessage { Role = ChatRole.Assistant, Content = "plain-world", TimestampUtc = now.AddSeconds(1) };

        await store.AppendMessageAsync(msg1);
        await store.AppendMessageAsync(msg2);

        var tail = await store.ReadTailAsync(2);
        Assert.Equal(2, tail.Count);
        Assert.Equal("plain-hello", tail[0].Content);
        Assert.Equal("plain-world", tail[1].Content);

        // Ensure files are PLAINTEXT envelopes (not raw ChatMessage) and Role is string name
        var files = Directory.GetFiles(dir, "*.json");
        Assert.True(files.Length >= 2);
        var sample = await File.ReadAllTextAsync(files[0]);
        Assert.Contains("\"Alg\":\"PLAINTEXT\"", sample, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"Role\":\"User\"", sample, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FileConversationStore_MixedEncryptedAndPlaintext_ReadsAllWithKey()
    {
        var dir = CreateTempDir();
        var key = "mixed-key";

        // Writer without key (plaintext)
        var plainStore = new FileConversationStore(dir, (string?)null);

        // Writer with key (encrypted)
        var encStore = new FileConversationStore(dir, key);

        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-1);
        // Write alternating: plain, enc, plain, enc
        await plainStore.AppendMessageAsync(new ChatMessage { Role = ChatRole.User, Content = "p0", TimestampUtc = baseTime.AddSeconds(0) });
        await encStore.AppendMessageAsync(new ChatMessage { Role = ChatRole.Assistant, Content = "e1", TimestampUtc = baseTime.AddSeconds(1) });
        await plainStore.AppendMessageAsync(new ChatMessage { Role = ChatRole.User, Content = "p2", TimestampUtc = baseTime.AddSeconds(2) });
        await encStore.AppendMessageAsync(new ChatMessage { Role = ChatRole.Assistant, Content = "e3", TimestampUtc = baseTime.AddSeconds(3) });

        // Reader with key should read both encrypted and plaintext messages
        var reader = new FileConversationStore(dir, key);
        var tail = await reader.ReadTailAsync(10);

        Assert.Equal(4, tail.Count);
        Assert.Equal("p0", tail[0].Content);
        Assert.Equal("e1", tail[1].Content);
        Assert.Equal("p2", tail[2].Content);
        Assert.Equal("e3", tail[3].Content);

        // Verify on-disk mix: at least one AES envelope and one PLAINTEXT envelope
        var files = Directory.GetFiles(dir, "*.json");
        Assert.True(files.Length >= 4);
        var contents = await Task.WhenAll(files.Select(f => File.ReadAllTextAsync(f)));
        Assert.Contains(contents, c => c.Contains("\"Alg\":\"AES-256-GCM\"", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(contents, c => c.Contains("\"Alg\":\"PLAINTEXT\"", StringComparison.OrdinalIgnoreCase));
        // And ensure PLAINTEXT content uses Role as string name
        Assert.Contains(contents, c => c.Contains("\"Role\":\"User\"", StringComparison.Ordinal) || c.Contains("\"Role\":\"Assistant\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FileConversationStore_MixedWithTwoKeys_UnknownKeyIsSkipped_KnownAndPlainRead()
    {
        var dir = CreateTempDir();
        var knownKey = "known-key";
        var unknownKey = "unknown-key";

        // Writers
        var plainStore = new FileConversationStore(dir, (string?)null);              // plaintext
        var knownEncStore = new FileConversationStore(dir, knownKey);                // encrypted with known key
        var unknownEncStore = new FileConversationStore(dir, unknownKey);            // encrypted with unknown key

        var t0 = DateTimeOffset.UtcNow.AddMinutes(-2);
        // Interleave messages: plain, known, unknown, plain, known, unknown
        await plainStore.AppendMessageAsync(new ChatMessage { Role = ChatRole.User, Content = "p0", TimestampUtc = t0.AddSeconds(0) });
        await knownEncStore.AppendMessageAsync(new ChatMessage { Role = ChatRole.Assistant, Content = "k1", TimestampUtc = t0.AddSeconds(1) });
        await unknownEncStore.AppendMessageAsync(new ChatMessage { Role = ChatRole.Assistant, Content = "u2", TimestampUtc = t0.AddSeconds(2) });
        await plainStore.AppendMessageAsync(new ChatMessage { Role = ChatRole.User, Content = "p3", TimestampUtc = t0.AddSeconds(3) });
        await knownEncStore.AppendMessageAsync(new ChatMessage { Role = ChatRole.Assistant, Content = "k4", TimestampUtc = t0.AddSeconds(4) });
        await unknownEncStore.AppendMessageAsync(new ChatMessage { Role = ChatRole.Assistant, Content = "u5", TimestampUtc = t0.AddSeconds(5) });

        // Reader with only the known key: should read plaintext and known-encrypted; skip unknown-encrypted silently
        var reader = new FileConversationStore(dir, knownKey);
        var tail = await reader.ReadTailAsync(10);

        // Expecting 4 items: p0, k1, p3, k4 (u2 and u5 skipped)
        Assert.Equal(4, tail.Count);
        Assert.Equal("p0", tail[0].Content);
        Assert.Equal("k1", tail[1].Content);
        Assert.Equal("p3", tail[2].Content);
        Assert.Equal("k4", tail[3].Content);

        // Sanity: ensure unknown-encrypted artifacts exist on disk (envelopes), but they were not returned
        var files = Directory.GetFiles(dir, "*.json");
        Assert.True(files.Length >= 6);
        var contents = await Task.WhenAll(files.Select(f => File.ReadAllTextAsync(f)));
        Assert.Contains(contents, c => c.Contains("\"Alg\":\"AES-256-GCM\"", StringComparison.OrdinalIgnoreCase));
        // And ensure at least one PLAINTEXT envelope exists with Role as string name
        Assert.Contains(contents, c => c.Contains("\"Alg\":\"PLAINTEXT\"", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(contents, c => c.Contains("\"Role\":\"User\"", StringComparison.Ordinal) || c.Contains("\"Role\":\"Assistant\"", StringComparison.Ordinal));
    }
}
