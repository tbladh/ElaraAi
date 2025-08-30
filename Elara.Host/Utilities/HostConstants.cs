namespace Elara.Host.Utilities
{
    /// <summary>
    /// Host-scoped constants. Keep host UX, paths, tokens, and identifiers here.
    /// </summary>
    public static class HostConstants
    {
        public static class Log
        {
            public const string Program = "Program";
            public const string Stt = "STT";
            public const string Ai = "AI";
            public const string Recorder = "Recorder";
            public const string Transcriber = "Transcriber";
            public const string Streamer = "Streamer";
            public const string Conversation = "Conversation";
        }

        public static class Cli
        {
            public const string RecordFlag = "--record";
        }

        public static class Announcements
        {
            public const string Wake = "Wake";
            public const string Prompt = "Prompt";
            public const string Quiescence = "Quiescence";
            public const string Startup = "Startup";
        }

        public static class Placeholders
        {
            public const string WakeWord = "{WakeWord}";
            public const string ModelName = "{ModelName}";
            public const string ModelBaseUrl = "{ModelBaseUrl}";
            public const string Voice = "{Voice}";
        }

        public static class LoggingPattern
        {
            public const string DateTokenPrefix = "{date:";
            public const char TokenCloseBrace = '}';
        }

        public static class Paths
        {
            // App/cache/model folders
            public const string AppFolderName = "ElaraAi";
            public const string CacheFolderName = "Cache";
            public const string ModelsFolderName = "Models";
            public const string WhisperFolderName = "Whisper";

            // Platform-specific segments and env vars
            public const string MacLibraryFolder = "Library";
            public const string MacCachesFolder = "Caches";
            public const string UnixDotCacheFolder = ".cache";
            public const string XdgCacheHomeEnv = "XDG_CACHE_HOME";
        }

        public static class Files
        {
            public const string TempFileSuffix = ".tmp";
        }

        public static class ConsoleText
        {
            public const string QuitHint = "Press 'Q' to quit or use Ctrl+C to stop.";
            public const string SandboxIntro = "Sandbox: Recording chunks and printing transcriptions. Press 'Q' to quit or Ctrl+C to stop.";
        }
    }
}
