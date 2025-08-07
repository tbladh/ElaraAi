using ErnestAi.Audio;
using ErnestAi.Core.Interfaces;
using ErnestAi.Intelligence;
using ErnestAi.Speech;
using Microsoft.Extensions.DependencyInjection;
using System;
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
            
            // Setup dependency injection
            var services = new ServiceCollection();
            ConfigureServices(services);
            
            var serviceProvider = services.BuildServiceProvider();
            
            // Initialize and start the application components
            await RunErnestAiAsync(serviceProvider);
        }
        
        private static async Task RunErnestAiAsync(ServiceProvider serviceProvider)
        {
            Console.WriteLine("Starting ErnestAi...");
            
            // Get services
            var wakeWordDetector = serviceProvider.GetRequiredService<IWakeWordDetector>();
            var audioProcessor = serviceProvider.GetRequiredService<IAudioProcessor>();
            var sttService = serviceProvider.GetRequiredService<ISpeechToTextService>();
            var llmService = serviceProvider.GetRequiredService<ILanguageModelService>();
            var ttsService = serviceProvider.GetRequiredService<ITextToSpeechService>();
            
            // Setup wake word detection handler
            var cts = new CancellationTokenSource();
            
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
        }
        
        private static void ConfigureServices(IServiceCollection services)
        {
            // Register core services with their implementations
            services.AddSingleton<IWakeWordDetector>(provider => 
                new WakeWordDetector("ggml-tiny.en.bin", 
                "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.en.bin"));
                
            services.AddSingleton<IAudioProcessor>(provider => 
                new AudioProcessor(16000, 1));
                
            services.AddScoped<ISpeechToTextService>(provider => 
                new SpeechToTextService("ggml-base.en.bin", 
                "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.en.bin"));
                
            services.AddScoped<ILanguageModelService>(provider => 
                new OllamaLanguageModelService("http://127.0.0.1:11434"));
                
            services.AddTransient<ITextToSpeechService>(provider => 
                new TextToSpeechService());
        }
    }
}
