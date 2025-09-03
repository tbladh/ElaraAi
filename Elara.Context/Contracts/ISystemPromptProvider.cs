using System.Threading;
using System.Threading.Tasks;

namespace Elara.Context.Contracts
{
    public interface ISystemPromptProvider
    {
        Task<string> GetSystemPromptAsync(CancellationToken ct = default);
    }
}
