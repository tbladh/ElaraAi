using Elara.Context.Contracts;
using System.Threading;
using System.Threading.Tasks;

namespace Elara.Context
{
    public sealed class SystemPromptProvider : ISystemPromptProvider
    {
        private readonly string _basePrompt;
        public SystemPromptProvider(string basePrompt) { _basePrompt = basePrompt ?? string.Empty; }
        public Task<string> GetSystemPromptAsync(CancellationToken ct = default)
            => Task.FromResult(_basePrompt);
    }
}
