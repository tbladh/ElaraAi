using System.Threading;
using System.Threading.Tasks;

namespace Elara.Context.Contracts
{
    public interface IPromptBuilder
    {
        Task<Prompt> BuildAsync(string userInput, int desiredContextN, CancellationToken ct = default);
    }
}
