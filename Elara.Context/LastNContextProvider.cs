using Elara.Context.Contracts;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Elara.Context
{
    public sealed class LastNContextProvider : IContextProvider
    {
        private readonly IConversationStore _store;
        public LastNContextProvider(IConversationStore store) { _store = store; }
        public Task<IReadOnlyList<ChatMessage>> GetContextAsync(string currentPrompt, int n, CancellationToken ct = default)
            => _store.ReadTailAsync(n, ct);
    }
}
