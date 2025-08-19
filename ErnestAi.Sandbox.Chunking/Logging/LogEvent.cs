using System;

namespace ErnestAi.Sandbox.Chunking.Logging
{
    public readonly struct LogEvent
    {
        public DateTimeOffset Timestamp { get; }
        public LogLevel Level { get; }
        public string Source { get; }
        public string Message { get; }

        public LogEvent(DateTimeOffset timestamp, LogLevel level, string source, string message)
        {
            Timestamp = timestamp;
            Level = level;
            Source = source ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public override string ToString()
        {
            var ts = Timestamp.ToLocalTime().ToString("HH:mm:ss");
            return $"[{ts}] {Source}, {Level}, {Message}";
        }
    }
}
