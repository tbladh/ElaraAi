using System.Threading;
using System.Threading.Tasks;

namespace Elara.Context.Contracts
{
    public interface IPromptRenderer<TProviderRequest>
    {
        Task<TProviderRequest> RenderAsync(Prompt prompt, CancellationToken ct = default);
    }
}
