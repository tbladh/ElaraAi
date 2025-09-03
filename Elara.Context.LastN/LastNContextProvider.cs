using Elara.Context.Contracts;

namespace Elara.Context.LastN
{
    public sealed class LastNContextProvider : IContextProvider
    {
        private readonly IConversationStore _store;
        public LastNContextProvider(IConversationStore store) { _store = store; }
        public Task<IReadOnlyList<ChatMessage>> GetContextAsync(string currentPrompt, int n, CancellationToken ct = default)
            => _store.ReadTailAsync(n, ct);
    }
}
