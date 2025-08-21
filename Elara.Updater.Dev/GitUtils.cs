using System.Diagnostics;

namespace Elara.Updater.Dev
{
    public static class GitUtils
    {
        // Async first
        public static async Task<(bool ok, string output)> RunGitAsync(string gitCli, string args, string workingDir, Action<string>? log = null)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = string.IsNullOrWhiteSpace(gitCli) ? "git" : gitCli,
                    Arguments = args,
                    WorkingDirectory = workingDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                var proc = Process.Start(psi);
                if (proc == null) return (false, "Failed to start git process");
                var stdout = new List<string>();
                var stderr = new List<string>();
                proc.OutputDataReceived += (_, e) => { if (e.Data != null) { stdout.Add(e.Data); log?.Invoke($"[git] {e.Data}"); } };
                proc.ErrorDataReceived += (_, e) => { if (e.Data != null) { stderr.Add(e.Data); log?.Invoke($"[git] {e.Data}"); } };
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                await proc.WaitForExitAsync();
                var ok = proc.ExitCode == 0;
                var output = string.Join(Environment.NewLine, ok ? stdout : stderr);
                return (ok, output);
            }
            catch (Exception ex)
            {
                log?.Invoke($"Git exception: {ex.Message}");
                return (false, ex.Message);
            }
        }

        public static (bool ok, string output) RunGit(string gitCli, string args, string workingDir, Action<string>? log = null)
        {
            return RunGitAsync(gitCli, args, workingDir, log).GetAwaiter().GetResult();
        }

        public static async Task<(bool ok, string message)> UpdateRepoAsync(string gitCli, string repoPath, string branch, Action<string>? log = null)
        {
            if (!Directory.Exists(repoPath)) return (false, $"Repo path not found: {repoPath}");
            var steps = new[]
            {
                $"fetch --all",
                $"checkout {branch}",
                $"pull --ff-only"
            };
            foreach (var step in steps)
            {
                var (ok, output) = await RunGitAsync(gitCli, step, repoPath, log);
                if (!ok) return (false, $"git {step} failed: {output}");
            }
            return (true, $"Updated to latest {branch}");
        }

        public static (bool ok, string message) UpdateRepo(string gitCli, string repoPath, string branch, Action<string>? log = null)
        {
            return UpdateRepoAsync(gitCli, repoPath, branch, log).GetAwaiter().GetResult();
        }

        public static async Task<(bool ok, string sha)> GetCurrentCommitAsync(string gitCli, string repoPath)
        {
            var (ok, output) = await RunGitAsync(gitCli, "rev-parse HEAD", repoPath);
            return (ok, ok ? output.Trim() : string.Empty);
        }

        public static (bool ok, string sha) GetCurrentCommit(string gitCli, string repoPath)
        {
            return GetCurrentCommitAsync(gitCli, repoPath).GetAwaiter().GetResult();
        }
    }
}
