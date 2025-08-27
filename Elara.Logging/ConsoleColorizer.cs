using System;

namespace Elara.Logging
{
    /// <summary>
    /// Applies console colors based on source and level. No file/IO. Purely cosmetic.
    /// </summary>
    public static class ConsoleColorizer
    {
        public static void WithColorFor(string source, LogLevel level, Action write)
        {
            var prev = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = PickColor(source, level);
                write();
            }
            finally
            {
                Console.ForegroundColor = prev;
            }
        }

        private static ConsoleColor PickColor(string source, LogLevel level)
        {
            // Level-first emphasis
            if (level == LogLevel.Error) return ConsoleColor.Red;
            if (level == LogLevel.Warn) return ConsoleColor.Yellow;
            if (level == LogLevel.Metrics) return ConsoleColor.DarkGray;

            // Source-based tinting for Info/Debug
            var s = (source ?? string.Empty).ToLowerInvariant();
            if (s.Contains("streamer")) return ConsoleColor.Cyan;
            if (s.Contains("transcriber")) return ConsoleColor.Green;
            if (s.Contains("state") || s.Contains("conversation")) return ConsoleColor.Magenta;
            if (s.Contains("stt")) return ConsoleColor.DarkCyan;
            if (s.Contains("tts")) return ConsoleColor.DarkYellow;
            if (s.Contains("llm") || s.Contains("ai")) return ConsoleColor.Blue;
            if (s.Contains("program")) return ConsoleColor.White;
            if (s.Contains("recorder")) return ConsoleColor.DarkGreen;
            if (s.Contains("metrics")) return ConsoleColor.DarkGray;

            return ConsoleColor.Gray;
        }
    }
}
