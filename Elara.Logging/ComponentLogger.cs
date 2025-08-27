using System;

namespace Elara.Logging
{
    /// <summary>
    /// Instance logger injected into components. Delegates to global Logger with a fixed source.
    /// Keeps components testable and consistent.
    /// </summary>
    public sealed class ComponentLogger : ILog
    {
        public string Source { get; }

        public ComponentLogger(string source)
        {
            Source = string.IsNullOrWhiteSpace(source) ? "Component" : source;
        }

        public void Debug(string message) => Logger.Debug(Source, message);
        public void Info(string message)  => Logger.Info(Source, message);
        public void Warn(string message)  => Logger.Warn(Source, message);
        public void Error(string message) => Logger.Error(Source, message);
        public void Metrics(string message) => Logger.Metrics(Source, message);
    }
}
