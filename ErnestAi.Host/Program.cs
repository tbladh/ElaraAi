using ErnestAi.Audio;
using ErnestAi.Configuration;
using ErnestAi.Core.Interfaces;
using ErnestAi.Intelligence;
using ErnestAi.Speech;
using Microsoft.Extensions.DependencyInjection;

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
            Console.WriteLine($"LLM Service URL: {config.LanguageModel.ServiceUrl}");
            Console.WriteLine($"LLM Model: {config.LanguageModel.ModelName}");
            Console.WriteLine($"STT Console Output: {(config.SpeechToText.OutputTranscriptionToConsole ? "Enabled" : "Disabled")}");
            
            // Setup dependency injection
            var services = new ServiceCollection();
            ConfigureServices(services, config);
            
            var serviceProvider = services.BuildServiceProvider();
            
            // Initialize and start the application components
            await RunErnestAiAsync(serviceProvider, config);
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

            // List available models and validate configured model before starting warmup or listeners
            try
            {
                var models = await llmService.GetAvailableModelsAsync();
                Console.WriteLine("[LLM] Available models:");
                foreach (var m in models)
                {
                    Console.WriteLine($" - {m}");
                }
                Console.WriteLine($"[LLM] Configured model: {config.LanguageModel.ModelName}");

                var exists = models.Any(m => string.Equals(m, config.LanguageModel.ModelName, StringComparison.OrdinalIgnoreCase));
                if (!exists)
                {
                    Console.WriteLine($"[LLM] Configured model '{config.LanguageModel.ModelName}' is NOT available at {config.LanguageModel.ServiceUrl}.");
                    Console.WriteLine("Please update appsettings.json to one of the available models above.");
                    PromptAndExit();
                    return;
                }
                Console.WriteLine($"[LLM] Using model: {config.LanguageModel.ModelName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LLM] Failed to retrieve available models: {ex.Message}");
                PromptAndExit();
                return;
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
        
        private static void ConfigureServices(IServiceCollection services, AppConfig config)
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
                
            services.AddSingleton<ILanguageModelService>(provider =>
            {
                var svc = new OllamaLanguageModelService(config.LanguageModel.ServiceUrl)
                {
                    CurrentModel = config.LanguageModel.ModelName,
                    SystemPrompt = config.LanguageModel.SystemPrompt
                };
                return svc;
            });
                
            services.AddTransient<ITextToSpeechService>(provider => 
                new TextToSpeechService());

            // Warmup orchestrator
            services.AddSingleton<WarmupOrchestrator>();
        }
    }
}
