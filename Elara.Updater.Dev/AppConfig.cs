using System.Text.Json;

namespace Elara.Updater.Dev
{
    public class AppConfig
    {
        public ServerConfig Server { get; set; } = new();
        public RepoConfig Repo { get; set; } = new();
        public HostConfig Host { get; set; } = new();
        public GitConfig Git { get; set; } = new();
        public OpsConfig Ops { get; set; } = new();

        public static AppConfig Load(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Config file not found: {path}");
            }
            var json = File.ReadAllText(path);
            var cfg = JsonSerializer.Deserialize<AppConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return cfg ?? new AppConfig();
        }

        public class ServerConfig
        {
            public int Port { get; set; } = 5055;
        }

        public class RepoConfig
        {
            public string Path { get; set; } = @"C:\\Repos\\ElaraAI";
            public string Branch { get; set; } = "main";
        }

        public class HostConfig
        {
            public string ProcessName { get; set; } = "Elara.Host";
            public string ExePath { get; set; } = @"C:\\Repos\\ElaraAI\\Elara.Host\\bin\\Debug\\net8.0\\Elara.Host.exe";
            public string Args { get; set; } = string.Empty;
        }

        public class GitConfig
        {
            public string CliPath { get; set; } = "git"; // expect on PATH
        }

        public class OpsConfig
        {
            public int KillTimeoutMs { get; set; } = 5000;
            public int StartWaitMs { get; set; } = 1500;
            public string MutexKey { get; set; } = "Elara.Updater.Dev.Mutex";
        }
    }
}
