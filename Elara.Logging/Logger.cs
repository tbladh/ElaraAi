namespace Elara.Logging
{
    /// <summary>
    /// Minimal logger: writes to file and raises an event for console subscribers.
    /// Color is applied by subscribers, not by the logger itself.
    /// Async-first; sync wrappers provided.
    /// </summary>
    public static class Logger
    {
        private static readonly SemaphoreSlim _gate = new(1, 1);
        private static string? _logFilePath;
        private static LogLevel _minLevel = LogLevel.Debug;

        public static event Action<LogEvent>? OnLog;

        public static void Configure(string? logFilePath, LogLevel minLevel = LogLevel.Debug)
        {
            _logFilePath = logFilePath;
            _minLevel = minLevel;
            if (!string.IsNullOrWhiteSpace(_logFilePath))
            {
                var dir = Path.GetDirectoryName(_logFilePath);
                if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
            }
        }

        public static Task DebugAsync(string source, string message) => LogAsync(LogLevel.Debug, source, message);
        public static Task InfoAsync(string source, string message) => LogAsync(LogLevel.Info, source, message);
        public static Task WarnAsync(string source, string message) => LogAsync(LogLevel.Warn, source, message);
        public static Task ErrorAsync(string source, string message) => LogAsync(LogLevel.Error, source, message);
        public static Task MetricsAsync(string source, string message) => LogAsync(LogLevel.Metrics, source, message);

        public static void Debug(string source, string message) => Log(LogLevel.Debug, source, message);
        public static void Info(string source, string message) => Log(LogLevel.Info, source, message);
        public static void Warn(string source, string message) => Log(LogLevel.Warn, source, message);
        public static void Error(string source, string message) => Log(LogLevel.Error, source, message);
        public static void Metrics(string source, string message) => Log(LogLevel.Metrics, source, message);

        public static async Task LogAsync(LogLevel level, string source, string message)
        {
            if (level < _minLevel) return;
            var evt = new LogEvent(DateTimeOffset.UtcNow, level, source, message);
            try { OnLog?.Invoke(evt); } catch { /* ignore subscriber errors */ }

            var line = evt.ToString() + Environment.NewLine;
            if (string.IsNullOrWhiteSpace(_logFilePath)) return;

            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                await File.AppendAllTextAsync(_logFilePath!, line).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        public static void Log(LogLevel level, string source, string message)
        {
            LogAsync(level, source, message).GetAwaiter().GetResult();
        }
    }
}
