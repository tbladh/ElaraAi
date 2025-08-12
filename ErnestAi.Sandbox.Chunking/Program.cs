using System.Threading.Channels;
using ErnestAi.Sandbox.Chunking.Core.Interfaces;
using ErnestAi.Sandbox.Chunking.Audio;
using ErnestAi.Sandbox.Chunking.Speech;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ErnestAi.Sandbox.Chunking
{
    internal class Program
    {
        // Tunables for this sandbox
        private const int ChunkMs = 3000;         // 3 seconds per chunk (better context)
        private const int AudioQueueCapacity = 16;
        private const string WakeWord = "anna"; // simple wake word for sandbox
        private const int ProcessingSilenceSeconds = 5;  // after 5s of silence, enter processing
        private const int EndSilenceSeconds = 60;        // after 60s of silence, return to quiescent

        private static async Task Main(string[] args)
        {
            using var cts = new CancellationTokenSource();

            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            var host = Host.CreateDefaultBuilder(args)
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddSimpleConsole(o => o.TimestampFormat = "HH:mm:ss ");
                })
                .ConfigureServices(services =>
                {
                    // Minimal concrete implementations
                    services.AddSingleton<IAudioProcessor>(_ => new AudioProcessor());
                    // Configure Whisper model for sandbox
                    services.AddSingleton<ISpeechToTextService>(_ => new SpeechToTextService(
                        modelFileName: "ggml-base.en.bin",
                        modelUrl: "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.en.bin"));
                })
                .Build();

            var audioChannel = Channel.CreateBounded<AudioChunk>(new BoundedChannelOptions(AudioQueueCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = true
            });

            var recorder = new Recorder(
                host.Services.GetRequiredService<IAudioProcessor>(),
                audioChannel.Writer,
                ChunkMs);

            var console = new CompactConsole();

            var fsm = new ConversationStateMachine(
                WakeWord,
                TimeSpan.FromSeconds(ProcessingSilenceSeconds),
                TimeSpan.FromSeconds(EndSilenceSeconds),
                console);

            var transcriber = new Transcriber(
                host.Services.GetRequiredService<ISpeechToTextService>(),
                audioChannel.Reader,
                fsm,
                console);

            console.WriteSpeechLine("Sandbox: Recording chunks and printing transcriptions. Press Ctrl+C to stop.");
            console.WriteSpeechLine($"Wake word: '{WakeWord}', processing after {ProcessingSilenceSeconds}s silence, end after {EndSilenceSeconds}s silence.");

            var recordTask = recorder.RunAsync(cts.Token);
            var transcribeTask = transcriber.RunAsync(cts.Token);

            await Task.WhenAll(recordTask, transcribeTask);
        }
    }
}
