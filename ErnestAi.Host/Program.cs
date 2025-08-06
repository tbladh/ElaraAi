using Microsoft.Extensions.DependencyInjection;

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
            
            // TODO: Initialize and start the application components
            
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
        
        private static void ConfigureServices(IServiceCollection services)
        {
            // TODO: Register all services for dependency injection
            
            // Core services
            // services.AddSingleton<IWakeWordDetector, WhisperWakeWordDetector>();
            // services.AddSingleton<IAudioProcessor, AudioProcessor>();
            // services.AddScoped<ISpeechToTextService, WhisperSttService>();
            // services.AddScoped<ILanguageModelService, OllamaService>();
            // services.AddTransient<IToolExecutor, PluginToolExecutor>();
            
            // Configuration services
            // services.AddSingleton<IAudioConfiguration, AudioConfiguration>();
            // services.AddSingleton<IModelConfiguration, ModelConfiguration>();
            // services.AddSingleton<IPersonalityConfiguration, PersonalityConfiguration>();
            
            // Storage services
            // services.AddSingleton<IConversationHistory, ConversationHistory>();
            // services.AddSingleton<IUserPreferences, UserPreferences>();
            // services.AddSingleton<IModelCache, ModelCache>();
        }
    }
}
