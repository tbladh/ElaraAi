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

        // Start host
        var (okStart, pid) = await ProcessUtils.StartProcessAsync(HostExePath(), cfg.Host.Args ?? string.Empty, HostWorkingDir(), Log);
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
        var (okStart, pid) = await ProcessUtils.StartProcessAsync(HostExePath(), cfg.Host.Args ?? string.Empty, HostWorkingDir(), Log);
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
