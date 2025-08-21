using System.Text;
using System.Diagnostics;

namespace Elara.Updater.Dev
{
    public static class HtmlTemplates
    {
        public static string Index(bool running, int[] pids, string branch, string? commit, string lastAction, DateTimeOffset? lastAt)
        {
            var pidText = pids.Length > 0 ? string.Join(", ", pids) : "-";
            var when = lastAt?.ToString("u") ?? "-";
            var status = running ? "Running" : "Stopped";
            var color = running ? "#2e7d32" : "#c62828";
            var commitShort = string.IsNullOrWhiteSpace(commit) ? "-" : commit.Substring(0, Math.Min(7, commit.Length));
            var html = $@"<!doctype html>
<html>
<head>
<meta charset='utf-8'/>
<meta name='viewport' content='width=device-width, initial-scale=1'/>
<title>Elara Updater</title>
<style>
 body {{ font-family: Segoe UI, Roboto, Arial, sans-serif; margin: 20px; color: #222; }}
 .card {{ border: 1px solid #ddd; border-radius: 8px; padding: 16px; max-width: 720px; }}
 .row {{ display: flex; gap: 12px; flex-wrap: wrap; margin-top: 12px; }}
 .btn {{ padding: 10px 16px; border: none; border-radius: 6px; cursor: pointer; color: #fff; }}
 .btn-primary {{ background: #1976d2; }}
 .btn-warn {{ background: #ef6c00; }}
 .btn:disabled {{ opacity: 0.6; cursor: not-allowed; }}
 .kv {{ display: grid; grid-template-columns: 160px 1fr; row-gap: 6px; column-gap: 12px; }}
 .status {{ color: {color}; font-weight: 600; }}
  pre {{ background: #f7f7f7; border: 1px solid #eee; padding: 8px; border-radius: 6px; overflow:auto; }}
</style>
</head>
<body>
<div class='card'>
  <h2>Elara Updater</h2>
  <div class='kv'>
    <div>Status</div><div class='status'>{status}</div>
    <div>Process IDs</div><div>{pidText}</div>
    <div>Branch</div><div>{branch}</div>
    <div>Commit</div><div>{commitShort}</div>
    <div>Last Action</div><div>{lastAction} @ {when}</div>
  </div>
  <div class='row'>
    <button id='redeploy' class='btn btn-primary'>Redeploy (Pull + Restart)</button>
    <button id='restart' class='btn btn-warn'>Restart Only</button>
  </div>
  <div class='row'>
    <pre id='output'>Ready.</pre>
  </div>
</div>
<script>
 async function post(path) {{
   const res = await fetch(path, {{ method: 'POST' }});
   const text = await res.text();
   try {{ return JSON.parse(text); }} catch {{ return {{ ok: res.ok, text }}; }}
 }}
 const out = document.getElementById('output');
 const btnRedeploy = document.getElementById('redeploy');
 const btnRestart = document.getElementById('restart');
 btnRedeploy.onclick = async () => {{
   btnRedeploy.disabled = true; btnRestart.disabled = true; out.textContent = 'Redeploying...';
   const r = await post('/redeploy');
   out.textContent = JSON.stringify(r, null, 2);
   btnRedeploy.disabled = false; btnRestart.disabled = false;
   setTimeout(() => location.reload(), 800);
 }};
 btnRestart.onclick = async () => {{
   btnRedeploy.disabled = true; btnRestart.disabled = true; out.textContent = 'Restarting...';
   const r = await post('/restart');
   out.textContent = JSON.stringify(r, null, 2);
   btnRedeploy.disabled = false; btnRestart.disabled = false;
   setTimeout(() => location.reload(), 800);
 }};
</script>
</body>
</html>";
            return html;
        }
    }
}
