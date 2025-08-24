using System.Text.Json;
using FluentHosting;
using Elara.Updater.Dev;

var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
var cfg = AppConfig.Load(configPath);

string RepoRoot() => cfg.Repo.RootPath;
string HostExePath() => Path.Combine(RepoRoot(), cfg.Host.ExeRelativePath);
string HostWorkingDir() => Path.GetDirectoryName(HostExePath())!;

var state = new UpdaterState();
var mutex = new SemaphoreSlim(1, 1);

void Log(string msg)
{
    Console.WriteLine($"[{DateTimeOffset.Now:u}] {msg}");
}

string HostProjectPath()
{
    // Infer project path from ExeRelativePath (e.g., Elara.Host\\bin\\Debug\\net8.0\\Elara.Host.exe)
    var rel = (cfg.Host.ExeRelativePath ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
    var parts = rel.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
    var binIdx = Array.FindIndex(parts, p => string.Equals(p, "bin", StringComparison.OrdinalIgnoreCase));
    string projectDirRel;
    if (binIdx > 0)
        projectDirRel = string.Join(Path.DirectorySeparatorChar, parts.Take(binIdx));
    else
        projectDirRel = "Elara.Host"; // sensible default
    var projName = Path.GetFileName(projectDirRel.TrimEnd(Path.DirectorySeparatorChar));
    return Path.Combine(RepoRoot(), projectDirRel, projName + ".csproj");
}

string HostBuildConfiguration()
{
    var rel = (cfg.Host.ExeRelativePath ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
    var parts = rel.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
    var binIdx = Array.FindIndex(parts, p => string.Equals(p, "bin", StringComparison.OrdinalIgnoreCase));
    if (binIdx >= 0 && parts.Length > binIdx + 1)
        return parts[binIdx + 1]; // e.g., Debug or Release
    return "Debug";
}

async Task<(bool ok, string output)> BuildHostAsync()
{
    try
    {
        var projectPath = HostProjectPath();
        var config = HostBuildConfiguration();
        var workingDir = Path.GetDirectoryName(projectPath)!;
        Log($"Building host: {projectPath} -c {config}");
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"build \"{projectPath}\" -c {config}",
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        var proc = System.Diagnostics.Process.Start(psi);
        if (proc == null) return (false, "Failed to start dotnet build process");
        var stdout = new List<string>();
        var stderr = new List<string>();
        proc.OutputDataReceived += (_, e) => { if (e.Data != null) { stdout.Add(e.Data); Log($"[build] {e.Data}"); } };
        proc.ErrorDataReceived += (_, e) => { if (e.Data != null) { stderr.Add(e.Data); Log($"[build] {e.Data}"); } };
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        await proc.WaitForExitAsync();
        var ok = proc.ExitCode == 0;
        var output = string.Join(Environment.NewLine, ok ? stdout : stderr);
        return (ok, output);
    }
    catch (Exception ex)
    {
        return (false, ex.Message);
    }
}

bool PromptYesNo(string message, bool defaultNo = true)
{
    Console.Write(message + (defaultNo ? " [y/N]: " : " [Y/n]: "));
    var input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input)) return !defaultNo;
    input = input.Trim();
    return input.Equals("y", StringComparison.OrdinalIgnoreCase) || input.Equals("yes", StringComparison.OrdinalIgnoreCase);
}

// Ensure repository exists locally; if not, clone; then ensure branch is updated
async Task EnsureRepoAsync()
{
    var root = RepoRoot();
    if (!Directory.Exists(root))
    {
        Log($"Repo directory not found at '{root}'. Cloning from {cfg.Repo.Url} ...");
        var (okCloneNew, outCloneNew) = await GitUtils.CloneRepoAsync(cfg.Git.CliPath, cfg.Repo.Url, root, Log);
        if (!okCloneNew)
        {
            throw new InvalidOperationException($"Clone failed: {outCloneNew}");
        }
        Log("Clone completed.");
    }
    else if (!GitUtils.IsGitRepo(root))
    {
        // Safety prompt: directory exists but is not a git repo
        Log($"Directory exists but is not a git repository: '{root}'");
        Log($"Intended action: git clone {cfg.Repo.Url} \"{root}\"");
        Log($"Target branch after clone: {cfg.Repo.Branch}");
        var proceed = PromptYesNo("Proceed with cloning into the existing directory? This will not delete files, but git may fail if the directory is not empty.");
        if (!proceed)
        {
            throw new InvalidOperationException("User declined cloning into existing non-git directory.");
        }
        var (okClone, outClone) = await GitUtils.CloneRepoAsync(cfg.Git.CliPath, cfg.Repo.Url, root, Log);
        if (!okClone)
        {
            throw new InvalidOperationException($"Clone failed: {outClone}");
        }
        Log("Clone completed.");
    }
    var (okUpdate, msgUpdate) = await GitUtils.UpdateRepoAsync(cfg.Git.CliPath, root, cfg.Repo.Branch, Log);
    if (!okUpdate)
    {
        throw new InvalidOperationException($"Update failed: {msgUpdate}");
    }
    // If host is running, kill it to avoid file locks, then build
    if (ProcessUtils.IsProcessRunning(cfg.Host.ProcessName))
    {
        Log("Host is running; attempting to stop it before build...");
        var killed = await ProcessUtils.KillProcessesByNameAsync(cfg.Host.ProcessName, cfg.Ops.KillTimeoutMs, Log);
        if (!killed)
        {
            throw new InvalidOperationException("Failed to kill running host processes within timeout.");
        }
        Log("Host processes stopped.");
    }
    var (okBuild, buildOut) = await BuildHostAsync();
    if (!okBuild)
    {
        throw new InvalidOperationException($"Build failed: {buildOut}");
    }
    // Auto start host after successful build
    var (okStart, pid) = await ProcessUtils.StartProcessInNewWindowAsync(HostExePath(), cfg.Host.Args ?? string.Empty, HostWorkingDir(), Log);
    if (!okStart)
    {
        throw new InvalidOperationException("Failed to start Elara.Host after build.");
    }
    await Task.Delay(cfg.Ops.StartWaitMs);
    Log($"Started PID {pid} after build.");
}

async Task<object> RedeployAsync()
{
    await mutex.WaitAsync();
    try
    {
        using var _ = state.BeginAction("redeploy");
        Log("Redeploy requested.");

        // Kill host
        var killed = await ProcessUtils.KillProcessesByNameAsync(cfg.Host.ProcessName, cfg.Ops.KillTimeoutMs, Log);
        if (!killed)
        {
            var msg = "Failed to kill all host processes within timeout.";
            Log(msg);
            return new { ok = false, message = msg };
        }

        // Update repo
        var (okUpdate, msgUpdate) = await GitUtils.UpdateRepoAsync(cfg.Git.CliPath, RepoRoot(), cfg.Repo.Branch, Log);
        if (!okUpdate)
        {
            Log(msgUpdate);
            return new { ok = false, message = msgUpdate };
        }

        // Build host after pull
        var (okBuild, buildOut) = await BuildHostAsync();
        if (!okBuild)
        {
            Log(buildOut);
            return new { ok = false, message = "Build failed." };
        }

        // Start host (new console window)
        var (okStart, pid) = await ProcessUtils.StartProcessInNewWindowAsync(HostExePath(), cfg.Host.Args ?? string.Empty, HostWorkingDir(), Log);
        if (!okStart)
        {
            return new { ok = false, message = "Failed to start Elara.Host." };
        }

        await Task.Delay(cfg.Ops.StartWaitMs);

        var (okSha, sha) = await GitUtils.GetCurrentCommitAsync(cfg.Git.CliPath, RepoRoot());
        var message = okSha ? $"Started PID {pid} at {cfg.Repo.Branch}@{sha}" : $"Started PID {pid}";
        Log(message);
        return new { ok = true, pid, branch = cfg.Repo.Branch, commit = okSha ? sha : null };
    }
    finally
    {
        mutex.Release();
    }
}

async Task<object> RestartAsync()
{
    await mutex.WaitAsync();
    try
    {
        using var _ = state.BeginAction("restart");
        Log("Restart requested.");
        var killed = await ProcessUtils.KillProcessesByNameAsync(cfg.Host.ProcessName, cfg.Ops.KillTimeoutMs, Log);
        if (!killed)
        {
            var msg = "Failed to kill all host processes within timeout.";
            Log(msg);
            return new { ok = false, message = msg };
        }
        var (okStart, pid) = await ProcessUtils.StartProcessInNewWindowAsync(HostExePath(), cfg.Host.Args ?? string.Empty, HostWorkingDir(), Log);
        if (!okStart)
        {
            return new { ok = false, message = "Failed to start Elara.Host." };
        }
        await Task.Delay(cfg.Ops.StartWaitMs);
        Log($"Started PID {pid}");
        return new { ok = true, pid };
    }
    finally
    {
        mutex.Release();
    }
}

object Status()
{
    var running = ProcessUtils.IsProcessRunning(cfg.Host.ProcessName);
    var pids = ProcessUtils.GetProcessIds(cfg.Host.ProcessName);
    return new
    {
        running,
        pids,
        branch = cfg.Repo.Branch,
        lastAction = new { state = state.LastAction, at = state.LastActionAt },
    };
}

// Ensure repo on startup
try
{
    await EnsureRepoAsync();
}
catch (Exception ex)
{
    Log($"Startup repo ensure failed: {ex.Message}");
}

var host = new FluentHost("http://localhost", cfg.Server.Port)
    .Handles("/", Verb.Get, ctx =>
    {
        var running = ProcessUtils.IsProcessRunning(cfg.Host.ProcessName);
        var pids = ProcessUtils.GetProcessIds(cfg.Host.ProcessName);
        var commit = GitUtils.GetCurrentCommit(cfg.Git.CliPath, RepoRoot()).sha;
        var html = HtmlTemplates.Index(running, pids, cfg.Repo.Branch, commit, state.LastAction, state.LastActionAt);
        return new StringResponse(html, contentType: "text/html; charset=utf-8");
    })
    .Handles("/status", Verb.Get, ctx =>
    {
        var json = JsonSerializer.Serialize(Status());
        return new StringResponse(json, contentType: "application/json; charset=utf-8");
    })
    .Handles("/health", Verb.Get, ctx => new StringResponse("OK"))
    .Handles("/restart", Verb.Post, ctx =>
    {
        var task = RestartAsync();
        var result = task.GetAwaiter().GetResult();
        return new JsonResponse<object>(result);
    })
    .Handles("/redeploy", Verb.Post, ctx =>
    {
        var task = RedeployAsync();
        var result = task.GetAwaiter().GetResult();
        return new JsonResponse<object>(result);
    })
    .Start();

Console.WriteLine($"Elara.Updater.Dev listening on http://localhost:{cfg.Server.Port}");
Console.WriteLine("Press Ctrl+C to exit.");

// Keep running
await Task.Delay(Timeout.Infinite);
