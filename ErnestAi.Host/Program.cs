using ErnestAi.Audio;
using ErnestAi.Configuration;
using ErnestAi.Core.Interfaces;
using ErnestAi.Intelligence;
using ErnestAi.Speech;
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
                    ILanguageModelService? svc = lm.Provider.Equals("ollama", StringComparison.OrdinalIgnoreCase)
                        ? new OllamaLanguageModelService(lm.ServiceUrl)
                        {
                            CurrentModel = lm.ModelName,
                            SystemPrompt = lm.SystemPrompt,
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
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    if (svc is OllamaLanguageModelService ollama)
                    {
                        await ollama.BarePromptAsync(ErnestAi.Core.Globals.WarmupPrompt, cts.Token);
                    }
                    else
                    {
                        await svc.GetResponseAsync(ErnestAi.Core.Globals.WarmupPrompt, cts.Token);
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
            
            // Ensure wake word is set correctly from config
            wakeWordDetector.WakeWord = config.WakeWord.WakeWord.ToLower();

            // Informational: list available models for the connected provider and show the selected one
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
            
            // Subscribe to transcription events
            if (sttService is SpeechToTextService speechToTextService)
            {
                speechToTextService.TextTranscribed += (sender, e) =>
                {
                    // This event handler will be called whenever text is transcribed
                    // You can use this to update a UI, log to a file, etc.
                    
                    // Example: Log to a file
                    try
                    {
                        File.AppendAllText("transcription_log.txt", 
                            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {e.StartTime:F1}s-{e.EndTime:F1}s: {e.Text}\n");
                    }
                    catch (Exception ex)
                    {
                        // Silently handle file access errors
                        Console.WriteLine($"Error logging transcription: {ex.Message}");
                    }
                    
                    // Example: You could send this to a UI, analytics service, etc.
                };
            }
            
            // Setup wake word detection handler
            var cts = new CancellationTokenSource();

            // Start model warmup orchestrator (if enabled)
            if (warmupOrchestrator != null)
            {
                await warmupOrchestrator.StartAsync(config, cts.Token);
            }
            
            wakeWordDetector.WakeWordDetected += async (sender, e) =>
            {
                Console.WriteLine($"Wake word detected: {e.DetectedText}");
                
                try
                {
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
            };
            
            // Start listening for wake word
            Console.WriteLine($"Listening for wake word: \"{wakeWordDetector.WakeWord}\"...");
            await wakeWordDetector.StartListeningAsync(cts.Token);
            
            // Wait for user to exit
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            
            // Clean up
            await wakeWordDetector.StopListeningAsync();
            cts.Cancel();
            if (warmupOrchestrator != null)
            {
                await warmupOrchestrator.StopAsync();
            }
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
                
            services.AddTransient<ITextToSpeechService>(provider => 
                new TextToSpeechService());

            // Warmup orchestrator
            services.AddSingleton<WarmupOrchestrator>();
        }
    }
}
