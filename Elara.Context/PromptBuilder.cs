using Elara.Context.Contracts;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Elara.Context
{
    public sealed class PromptBuilder : IPromptBuilder
    {
        private readonly ISystemPromptProvider _system;
        private readonly IEnumerable<IContextProvider> _contextProviders; // ordered

        public PromptBuilder(ISystemPromptProvider system, IEnumerable<IContextProvider> contextProviders)
        {
            _system = system;
            _contextProviders = contextProviders;
        }

        public async Task<Prompt> BuildAsync(string userInput, int desiredContextN, CancellationToken ct = default)
        {
            var sys = await _system.GetSystemPromptAsync(ct).ConfigureAwait(false);
            var context = new List<ChatMessage>();
            foreach (var p in _contextProviders)
            {
                var part = await p.GetContextAsync(userInput, desiredContextN, ct).ConfigureAwait(false);
                if (part != null && part.Count > 0) context.AddRange(part);
            }
            return new Prompt
            {
                System = sys,
                Context = context,
                UserInput = userInput,
                NowUtc = DateTimeOffset.UtcNow
            };
        }
    }
}
