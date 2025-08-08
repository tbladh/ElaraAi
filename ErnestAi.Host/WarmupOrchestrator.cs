using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErnestAi.Configuration;
using ErnestAi.Core;
using ErnestAi.Core.Interfaces;

namespace ErnestAi.Host
{
    public class WarmupOrchestrator
    {
        private readonly IEnumerable<ILanguageModelService> _modelServices;
        private readonly List<Task> _runningTasks = new();
        private CancellationTokenSource _cts;

        public WarmupOrchestrator(IEnumerable<ILanguageModelService> modelServices)
        {
            _modelServices = modelServices;
        }

        public Task StartAsync(AppConfig config, CancellationToken appCancellationToken)
        {
            if (config?.LanguageModel?.Warmup == null || !config.LanguageModel.Warmup.Enabled)
            {
                return Task.CompletedTask;
            }

            _cts = CancellationTokenSource.CreateLinkedTokenSource(appCancellationToken);
            var ct = _cts.Token;

            var providers = config.LanguageModel.Warmup.Providers ?? Array.Empty<ProviderWarmupConfig>();
            if (providers.Length == 0) return Task.CompletedTask;

            // Map available services by ProviderName (case-insensitive)
            var serviceByName = _modelServices
                .GroupBy(s => s.ProviderName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var p in providers.Where(p => p.Enabled))
            {
                if (!serviceByName.TryGetValue(p.Name ?? string.Empty, out var svc))
                {
                    continue; // no service for this provider
                }

                var intervalSecs = p.IntervalSeconds.HasValue && p.IntervalSeconds.Value > 0
                    ? p.IntervalSeconds.Value
                    : Math.Max(1, config.LanguageModel.Warmup.DefaultIntervalSeconds);

                _runningTasks.Add(RunProviderLoopAsync(svc, p.Name, TimeSpan.FromSeconds(intervalSecs), ct));
            }

            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            try
            {
                _cts?.Cancel();
                if (_runningTasks.Count > 0)
                {
                    await Task.WhenAll(_runningTasks.ToArray());
                }
            }
            catch
            {
                // swallow exceptions on shutdown
            }
            finally
            {
                _cts?.Dispose();
                _runningTasks.Clear();
            }
        }

        private static async Task RunProviderLoopAsync(ILanguageModelService service, string providerName, TimeSpan interval, CancellationToken ct)
        {
            // Small random jitter before first run
            var jitterMs = new Random().Next(0, 10_000);
            try { await Task.Delay(jitterMs, ct); } catch { }

            using var timer = new PeriodicTimer(interval);

            // Immediate first ping
            await SendPingAsync(service, providerName, ct);

            while (await timer.WaitForNextTickAsync(ct))
            {
                await SendPingAsync(service, providerName, ct);
            }
        }

        private static async Task SendPingAsync(ILanguageModelService service, string providerName, CancellationToken ct)
        {
            try
            {
                // Prefer barebones prompt without system prompt if the implementation supports it
                if (service is ErnestAi.Intelligence.OllamaLanguageModelService ollama)
                {
                    await ollama.BarePromptAsync(Globals.WarmupPrompt, ct);
                }
                else
                {
                    // Fallback to normal call
                    await service.GetResponseAsync(Globals.WarmupPrompt, ct);
                }
                Console.WriteLine($"[Warmup] Ping sent to {providerName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Warmup] Ping failed for {providerName}: {ex.Message}");
            }
        }
    }
}
