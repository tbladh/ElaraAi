using System;
using System.Collections.Generic;

namespace Elara.Context
{
    public sealed class ChatMessage
    {
        public required ChatRole Role { get; init; }
        public required string Content { get; init; }
        public required DateTimeOffset TimestampUtc { get; init; }
        public IDictionary<string, string>? Metadata { get; init; }
    }
}
