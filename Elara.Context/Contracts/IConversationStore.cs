using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Elara.Context.Contracts
{
    public interface IConversationStore
    {
        Task AppendMessageAsync(ChatMessage message, CancellationToken ct = default);
        Task<IReadOnlyList<ChatMessage>> ReadTailAsync(int n, CancellationToken ct = default);
    }
}
