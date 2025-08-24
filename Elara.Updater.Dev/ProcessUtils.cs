using System.Diagnostics;
using System.Text;
using System.Linq;

namespace Elara.Updater.Dev
{
    public static class ProcessUtils
    {
        // Async first
        public static async Task<bool> KillProcessesByNameAsync(string processName, int timeoutMs, Action<string>? log = null)
        {
            var procs = Process.GetProcessesByName(processName);
            if (procs.Length == 0) return true;
            foreach (var p in procs)
            {
                try
                {
                    log?.Invoke($"Killing {p.ProcessName} (PID {p.Id})...");
                    p.Kill(true);
                }
                catch (Exception ex)
                {
                    log?.Invoke($"Failed to kill PID {p.Id}: {ex.Message}");
                }
            }
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                await Task.Delay(100);
                if (!IsProcessRunning(processName)) return true;
            }
            return !IsProcessRunning(processName);
        }

        public static bool KillProcessesByName(string processName, int timeoutMs, Action<string>? log = null)
        {
            return KillProcessesByNameAsync(processName, timeoutMs, log).GetAwaiter().GetResult();
        }

        public static async Task<(bool ok, int pid)> StartProcessAsync(string exePath, string args, string workingDir, Action<string>? log = null)
        {
            if (!File.Exists(exePath))
            {
                log?.Invoke($"Executable not found: {exePath}");
                return (false, 0);
            }
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = args ?? string.Empty,
                    WorkingDirectory = string.IsNullOrWhiteSpace(workingDir) ? Path.GetDirectoryName(exePath)! : workingDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                var proc = Process.Start(psi);
                if (proc == null)
                {
                    log?.Invoke("Failed to start process.");
                    return (false, 0);
                }
                _ = Task.Run(async () =>
                {
                    try
                    {
                        while (!proc.StandardOutput.EndOfStream)
                        {
                            var line = await proc.StandardOutput.ReadLineAsync();
                            if (line != null) log?.Invoke($"[HOST OUT] {line}");
                        }
                    }
                    catch { }
                });
                _ = Task.Run(async () =>
                {
                    try
                    {
                        while (!proc.StandardError.EndOfStream)
                        {
                            var line = await proc.StandardError.ReadLineAsync();
                            if (line != null) log?.Invoke($"[HOST ERR] {line}");
                        }
                    }
                    catch { }
                });
                // Allow brief warmup
                await Task.Delay(200);
                return (true, proc.Id);
            }
            catch (Exception ex)
            {
                log?.Invoke($"Exception starting process: {ex.Message}");
                return (false, 0);
            }
        }

        public static (bool ok, int pid) StartProcess(string exePath, string args, string workingDir, Action<string>? log = null)
        {
            return StartProcessAsync(exePath, args, workingDir, log).GetAwaiter().GetResult();
        }

        // Launches a process in a NEW console window and attempts to return the child process PID.
        // This uses `cmd.exe /c start` to spawn a detached console window.
        public static async Task<(bool ok, int pid)> StartProcessInNewWindowAsync(string exePath, string args, string workingDir, Action<string>? log = null)
        {
            if (!File.Exists(exePath))
            {
                log?.Invoke($"Executable not found: {exePath}");
                return (false, 0);
            }
            try
            {
                var targetWorkingDir = string.IsNullOrWhiteSpace(workingDir) ? Path.GetDirectoryName(exePath)! : workingDir;

                // Capture existing PIDs to identify the newly spawned process
                var procName = Path.GetFileNameWithoutExtension(exePath);
                var beforePids = GetProcessIds(procName).ToHashSet();

                // Use cmd to open a new console window for the target process
                var title = "Elara.Host";
                var quotedExe = "\"" + exePath + "\"";
                var cmdArgs = $"/c start \"{title}\" {quotedExe} {args ?? string.Empty}".Trim();
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = cmdArgs,
                    WorkingDirectory = targetWorkingDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    CreateNoWindow = false,
                    WindowStyle = ProcessWindowStyle.Normal
                };

                var starter = Process.Start(psi);
                if (starter == null)
                {
                    log?.Invoke("Failed to invoke cmd.exe to start process.");
                    return (false, 0);
                }

                // Poll briefly to find the new child process PID
                var sw = Stopwatch.StartNew();
                int newPid = 0;
                while (sw.ElapsedMilliseconds < 5000)
                {
                    await Task.Delay(200);
                    var afterPids = GetProcessIds(procName);
                    newPid = afterPids.FirstOrDefault(pid => !beforePids.Contains(pid));
                    if (newPid != 0) break;
                }

                if (newPid == 0)
                {
                    log?.Invoke("Started process in new window, but could not identify PID.");
                    return (true, 0);
                }

                return (true, newPid);
            }
            catch (Exception ex)
            {
                log?.Invoke($"Exception starting process (new window): {ex.Message}");
                return (false, 0);
            }
        }

        public static (bool ok, int pid) StartProcessInNewWindow(string exePath, string args, string workingDir, Action<string>? log = null)
        {
            return StartProcessInNewWindowAsync(exePath, args, workingDir, log).GetAwaiter().GetResult();
        }

        public static bool IsProcessRunning(string processName)
        {
            return Process.GetProcessesByName(processName).Any();
        }

        public static int[] GetProcessIds(string processName)
        {
            try
            {
                return Process.GetProcessesByName(processName).Select(p => p.Id).ToArray();
            }
            catch
            {
                return Array.Empty<int>();
            }
        }
    }
}
