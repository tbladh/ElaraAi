using ErnestAi.Audio;
using ErnestAi.Configuration;
using ErnestAi.Core.Interfaces;
using ErnestAi.Intelligence;
using ErnestAi.Speech;
using ErnestAi.Tools;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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

            // Setup dependency injection
            var services = new ServiceCollection();
            ConfigureServices(services, config, selectedLlm);
            
            var serviceProvider = services.BuildServiceProvider();

            // Initialize and start the application components
            await RunErnestAiAsync(serviceProvider, config);
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
        
        /// <summary>
        /// Composes the runtime: initializes TTS/announcements, subscribes events, wires handlers, and starts listening.
        /// </summary>
        private static async Task RunErnestAiAsync(ServiceProvider serviceProvider, AppConfig config)
        {
            Console.WriteLine("Starting ErnestAi...");
            
            // Get services
            var wakeWordDetector = serviceProvider.GetRequiredService<IWakeWordDetector>();
            var audioProcessor = serviceProvider.GetRequiredService<IAudioProcessor>();
            var sttService = serviceProvider.GetRequiredService<ISpeechToTextService>();
            var llmService = serviceProvider.GetRequiredService<ILanguageModelService>();
            var ttsService = serviceProvider.GetRequiredService<ITextToSpeechService>();
            var warmupOrchestrator = serviceProvider.GetService<WarmupOrchestrator>();
            var announcement = serviceProvider.GetService<AnnouncementService>();

            // Initialize and seal TTS for process lifetime
            await InitializeAndSealTtsAsync(ttsService, config);

            // Initialize announcement service and optionally preload phrase
            await InitializeAnnouncementsAsync(announcement, ttsService, config);
            
            // Ensure wake word is set correctly from config
            wakeWordDetector.WakeWord = config.WakeWord.WakeWord.ToLower();

            // Informational: list available models for the connected provider and show the selected one
            await ListAvailableModelsAsync(llmService);
            
            // Subscribe to transcription events
            SubscribeToTranscriptions(sttService);
            
            // Setup wake word detection handler
            var cts = new CancellationTokenSource();

            // Start model warmup orchestrator (if enabled)
            await StartWarmupAsync(warmupOrchestrator, config, cts.Token);
            
            wakeWordDetector.WakeWordDetected += async (sender, e) =>
                await HandleWakeWordAsync(announcement, config, audioProcessor, sttService, llmService, ttsService, e.DetectedText);

            await StartListeningAndWaitForExitAsync(wakeWordDetector, cts, warmupOrchestrator);
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
        /// Handles the full prompt/response round-trip triggered by a wake word.
        /// Plays an announcement, records audio, transcribes, queries LLM, and speaks the response.
        /// </summary>
        private static async Task HandleWakeWordAsync(
            AnnouncementService? announcement,
            AppConfig config,
            IAudioProcessor audioProcessor,
            ISpeechToTextService sttService,
            ILanguageModelService llmService,
            ITextToSpeechService ttsService,
            string? detectedText)
        {
            Console.WriteLine($"Wake word detected: {detectedText}");

            try
            {
                // Play announcement (lets the user know we're ready)
                if (announcement != null && config.Acknowledgement?.Enabled == true && !string.IsNullOrWhiteSpace(config.Acknowledgement.Phrase))
                {
                    await announcement.PlayAsync(config.Acknowledgement.Phrase);
                    var pauseMs = config.Acknowledgement?.PauseAfterMs ?? 0;
                    if (pauseMs > 0)
                    {
                        await Task.Delay(pauseMs);
                    }
                }

                // Start recording
                Console.WriteLine("Recording...");
                await audioProcessor.StartRecordingAsync();

                // Record for 5 seconds
                await Task.Delay(5000);

                // Stop recording and get audio
                Console.WriteLine("Processing...");
                var audioStream = await audioProcessor.StopRecordingAsync();

                // Transcribe audio
                var transcription = await sttService.TranscribeAsync(audioStream);
                Console.WriteLine($"You said: {transcription}");

                // Get response from LLM
                Console.WriteLine("Thinking...");
                var response = await llmService.GetResponseAsync(transcription);
                Console.WriteLine($"AI: {response}");

                // Speak response
                await ttsService.SpeakToDefaultOutputAsync(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing request: {ex.Message}");
            }
        }

        /// <summary>
        /// Starts listening for the wake word and blocks until user presses a key, then performs orderly shutdown.
        /// </summary>
        private static async Task StartListeningAndWaitForExitAsync(
            IWakeWordDetector wakeWordDetector,
            CancellationTokenSource cts,
            WarmupOrchestrator? warmupOrchestrator)
        {
            // Start listening for wake word
            Console.WriteLine($"Listening for wake word: \"{wakeWordDetector.WakeWord}\"...");
            await wakeWordDetector.StartListeningAsync(cts.Token);

            // Wait for user to exit
            Console.WriteLine("Press any key to exit...");
            try { Console.ReadKey(); } catch { /* ignore */ }

            // Clean up
            await wakeWordDetector.StopListeningAsync();
            cts.Cancel();
            if (warmupOrchestrator != null)
            {
                await warmupOrchestrator.StopAsync();
            }
        }

        /// <summary>
        /// Enumerates available TTS voices, selects the preferred voice (if configured), and seals the TTS service
        /// so its parameters remain immutable for the lifetime of the process.
        /// </summary>
        private static async Task InitializeAndSealTtsAsync(ITextToSpeechService ttsService, AppConfig config)
        {
            try
            {
                var voices = (await ttsService.GetAvailableVoicesAsync()).ToList();
                Console.WriteLine("[TTS] Available voices:");
                foreach (var v in voices)
                {
                    Console.WriteLine($"  - {v}");
                }

                string? selectedVoice = null;
                var preferred = config.TextToSpeech?.PreferredVoice;
                if (!string.IsNullOrWhiteSpace(preferred))
                {
                    selectedVoice = voices.FirstOrDefault(v => string.Equals(v, preferred, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(selectedVoice))
                    {
                        Console.WriteLine($"[TTS] Selected voice: '{selectedVoice}'");
                    }
                    else
                    {
                        Console.WriteLine($"[TTS] PreferredVoice '{preferred}' not found. Using current voice '{ttsService.CurrentVoice ?? "<none>"}'.");
                    }
                }
                else
                {
                    Console.WriteLine($"[TTS] No PreferredVoice configured. Using current voice '{ttsService.CurrentVoice ?? "<none>"}'.");
                }

                if (ttsService is TextToSpeechService ttsConcrete)
                {
                    var voiceToUse = selectedVoice ?? ttsService.CurrentVoice;
                    ttsConcrete.InitializeOnce(voiceToUse, ttsService.SpeechRate, ttsService.SpeechPitch);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TTS] Failed to enumerate/select voices: {ex.Message}");
            }
        }

        /// <summary>
        /// Initializes the announcement subsystem and optionally preloads the configured phrase.
        /// </summary>
        private static async Task InitializeAnnouncementsAsync(AnnouncementService? announcement, ITextToSpeechService ttsService, AppConfig config)
        {
            if (announcement == null) return;
            try
            {
                var voice = ttsService.CurrentVoice ?? string.Empty;
                var rate = ttsService.SpeechRate;
                var pitch = ttsService.SpeechPitch;
                var cacheDir = config.Acknowledgement?.CacheDirectory;
                await announcement.InitializeAsync(voice, rate, pitch, cacheDir);

                if (config.Acknowledgement?.Enabled == true && !string.IsNullOrWhiteSpace(config.Acknowledgement.Phrase))
                {
                    await announcement.PreloadAsync(config.Acknowledgement.Phrase);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Ann] Initialization failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Lists available models from the connected provider and prints the selected one.
        /// </summary>
        private static async Task ListAvailableModelsAsync(ILanguageModelService llmService)
        {
            try
            {
                var models = await llmService.GetAvailableModelsAsync();
                Console.WriteLine("[LLM] Available models:");
                foreach (var m in models)
                {
                    Console.WriteLine($" - {m}");
                }
                Console.WriteLine($"[LLM] Selected model: {llmService.CurrentModel} (provider={llmService.ProviderName})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LLM] Failed to retrieve available models: {ex.Message}");
            }
        }

        /// <summary>
        /// Subscribes to STT transcription events and logs transcriptions to a local file.
        /// </summary>
        private static void SubscribeToTranscriptions(ISpeechToTextService sttService)
        {
            if (sttService is SpeechToTextService speechToTextService)
            {
                speechToTextService.TextTranscribed += (sender, e) =>
                {
                    try
                    {
                        File.AppendAllText("transcription_log.txt",
                            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {e.StartTime:F1}s-{e.EndTime:F1}s: {e.Text}\n");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error logging transcription: {ex.Message}");
                    }
                };
            }
        }

        /// <summary>
        /// Starts the warmup orchestrator if it is registered/enabled.
        /// </summary>
        private static async Task StartWarmupAsync(WarmupOrchestrator? warmupOrchestrator, AppConfig config, CancellationToken token)
        {
            if (warmupOrchestrator != null)
            {
                await warmupOrchestrator.StartAsync(config, token);
            }
        }
    }
}
