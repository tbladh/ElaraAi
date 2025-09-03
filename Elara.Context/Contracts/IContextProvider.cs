using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Elara.Context.Contracts
{
    public interface IContextProvider
    {
        Task<IReadOnlyList<ChatMessage>> GetContextAsync(string currentPrompt, int n, CancellationToken ct = default);
    }
}
