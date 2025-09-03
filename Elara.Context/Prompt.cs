using System;
using System.Collections.Generic;

namespace Elara.Context
{
    public sealed class Prompt
    {
        public required string System { get; init; }
        public required IReadOnlyList<ChatMessage> Context { get; init; }
        public required string UserInput { get; init; }
        public required DateTimeOffset NowUtc { get; init; }
        public IDictionary<string, string>? Hints { get; init; }
    }
}
