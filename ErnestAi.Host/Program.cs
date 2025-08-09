using ErnestAi.Audio;
using ErnestAi.Configuration;
using ErnestAi.Core.Interfaces;
using ErnestAi.Intelligence;
using ErnestAi.Speech;
using ErnestAi.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ErnestAi.Host
{
    internal class Program
    {
        /// <summary>
        /// Application entry point.
        /// Performs configuration load, language model selection, DI composition, and starts the runtime loop.
        /// </summary>
        static async Task Main(string[] args)
        {
            Console.WriteLine("ErnestAi - Local AI Home Assistant");
            Console.WriteLine("----------------------------------");
            ConfigureGlobalExceptionHandling();
            
            // Load configuration
            Console.WriteLine("Loading configuration...");
            var config = await AppConfig.LoadAsync();
            
            // Display configuration information
            Console.WriteLine($"Wake Word: {config.WakeWord.WakeWord}");
            Console.WriteLine($"STT Model: {config.SpeechToText.ModelFileName}");
            Console.WriteLine($"LM candidates: {config.LanguageModels.Length}");
            Console.WriteLine($"STT Console Output: {(config.SpeechToText.OutputTranscriptionToConsole ? "Enabled" : "Disabled")}");
            Console.WriteLine($"System Prompt (root): {(!string.IsNullOrWhiteSpace(config.SystemPrompt) ? config.SystemPrompt : "<none>")}");

            // Select the first responsive language model by priority
            var selectedLlm = await SelectLanguageModelAsync(config);
            if (selectedLlm == null)
            {
                PromptAndExit();
                return;
            }

            // Build and run the Generic Host
            var host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddSimpleConsole(o =>
                    {
                        o.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
                    });
                })
                .ConfigureServices(services =>
                {
                    ConfigureServices(services, config, selectedLlm);
                    services.AddHostedService<WakeWordWorker>();
                })
                .UseConsoleLifetime()
                .Build();

            await host.RunAsync();
        }

        private static async Task<ILanguageModelService?> SelectLanguageModelAsync(AppConfig config)
        {
            Console.WriteLine("Selecting language model by priority...");
            var ordered = config.LanguageModels
                .OrderBy(lm => lm.Priority)
                .ThenBy(lm => lm.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach (var lm in ordered)
            {
                Console.WriteLine($"[LM] Trying {lm.Name} (provider={lm.Provider}, model={lm.ModelName})...");
                try
                {
                    var effectivePrompt = !string.IsNullOrWhiteSpace(lm.SystemPrompt)
                        ? lm.SystemPrompt
                        : config.SystemPrompt;

                    ILanguageModelService? svc = lm.Provider.Equals("ollama", StringComparison.OrdinalIgnoreCase)
                        ? new OllamaLanguageModelService(lm.ServiceUrl)
                        {
                            CurrentModel = lm.ModelName,
                            SystemPrompt = effectivePrompt ?? string.Empty,
                            OutputFilters = (lm.Filter != null) ? new List<string>(lm.Filter) : new List<string>()
                        }
                        : null;

                    if (svc == null)
                    {
                        Console.WriteLine($"[LM] Provider '{lm.Provider}' not supported yet. Skipping.");
                        continue;
                    }

                    // Verify model exists at provider
                    var models = await svc.GetAvailableModelsAsync();
                    var exists = models.Any(m => string.Equals(m, lm.ModelName, StringComparison.OrdinalIgnoreCase));
                    if (!exists)
                    {
                        Console.WriteLine($"[LM] Model '{lm.ModelName}' not found at {lm.ServiceUrl}. Skipping.");
                        continue;
                    }

                    // Quick ping to ensure responsiveness
                    // Warmup the selected provider with a generous timeout to accommodate initial model load
                    async Task WarmAsync(int timeoutSeconds)
                    {
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                        if (svc is OllamaLanguageModelService ollama)
                        {
                            await ollama.BarePromptAsync(Core.Globals.WarmupPrompt, cts.Token);
                        }
                        else
                        {
                            await svc.GetResponseAsync(Core.Globals.WarmupPrompt, cts.Token);
                        }
                    }

                    try
                    {
                        await WarmAsync(45);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[LM] Warmup attempt 1 failed for {lm.Name}: {ex.Message}. Retrying...");
                        await WarmAsync(90);
                    }

                    Console.WriteLine($"[LM] Selected: {lm.Name} (provider={lm.Provider}, model={lm.ModelName})");
                    return svc;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LM] Candidate '{lm.Name}' failed: {ex.Message}");
                }
            }

            Console.WriteLine("[LM] No language model candidates responded successfully. Please check your configuration or providers.");
            return null;
        }

        private static void PromptAndExit()
        {
            Console.WriteLine("Press any key to close...");
            try { Console.ReadKey(true); } catch { /* ignore */ }
            Environment.Exit(1);
        }

        private static void ConfigureServices(IServiceCollection services, AppConfig config, ILanguageModelService selectedLlm)
        {
            // Register configuration
            services.AddSingleton(config);
            
            // Register core services with their implementations
            services.AddSingleton<IWakeWordDetector>(provider => 
                new WakeWordDetector(
                    config.WakeWord.ModelFileName, 
                    config.WakeWord.ModelUrl,
                    config.WakeWord));
                
            services.AddSingleton<IAudioProcessor>(provider => 
                new AudioProcessor(
                    config.Audio.SampleRate, 
                    config.Audio.Channels));
                
            services.AddScoped<ISpeechToTextService>(provider => 
                new SpeechToTextService(
                    config.SpeechToText.ModelFileName, 
                    config.SpeechToText.ModelUrl,
                    config.SpeechToText));
                
            services.AddSingleton<ILanguageModelService>(provider => selectedLlm);
                
            services.AddSingleton<ITextToSpeechService>(provider => 
                new TextToSpeechService());

            // Utilities and announcement
            services.AddSingleton<IAudioPlayer, AudioPlayer>();
            services.AddSingleton<ICacheService, FileCacheService>();
            services.AddSingleton<IContentHashProvider, Md5HashProvider>();
            services.AddSingleton<AnnouncementService>();

            // Warmup orchestrator
            services.AddSingleton<WarmupOrchestrator>();
        }

        /// <summary>
        /// Sets up global exception handling for fatal and unobserved exceptions.
        /// Use local try/catch only for expected, recoverable scenarios.
        /// </summary>
        private static void ConfigureGlobalExceptionHandling()
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            Console.Error.WriteLine("[FATAL] Unhandled exception: " + (ex?.ToString() ?? "<unknown>"));
            try
            {
                File.AppendAllText("fatal.log",
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] FATAL: {ex}\n");
            }
            catch { /* If logging fails, there's nothing more we can do */ }
            Environment.Exit(1);
        }

        private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            Console.Error.WriteLine("[FATAL] Unobserved task exception: " + e.Exception);
            try
            {
                File.AppendAllText("fatal.log",
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] UNOBSERVED: {e.Exception}\n");
            }
            catch { }
            e.SetObserved();
        }
    }
}
