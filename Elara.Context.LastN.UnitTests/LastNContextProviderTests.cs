using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Elara.Context;
using Elara.Context.Contracts;
using Elara.Context.LastN;
using Xunit;

namespace Elara.Context.LastN.UnitTests;

public sealed class LastNContextProviderTests
{
    [Fact]
    public async Task GetContextAsync_ForwardsRequestToStore()
    {
        var store = new StubConversationStore();
        var provider = new LastNContextProvider(store);

        var result = await provider.GetContextAsync("prompt", 3, CancellationToken.None);

        Assert.True(store.ReadTailCalled);
        Assert.Equal(3, store.LastRequestedCount);
        Assert.Single(result);
        Assert.Equal("test", result[0].Content);
    }

    private sealed class StubConversationStore : IConversationStore
    {
        public bool ReadTailCalled { get; private set; }
        public int LastRequestedCount { get; private set; }

        public Task AppendMessageAsync(ChatMessage message, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<ChatMessage>> ReadTailAsync(int count, CancellationToken ct = default)
        {
            ReadTailCalled = true;
            LastRequestedCount = count;
            IReadOnlyList<ChatMessage> messages = new[]
            {
                new ChatMessage
                {
                    Role = ChatRole.User,
                    Content = "test",
                    TimestampUtc = DateTimeOffset.UtcNow
                }
            };
            return Task.FromResult(messages);
        }
    }
}
