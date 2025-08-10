using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ErnestAi.Core.Interfaces;
using ErnestAi.Audio;
using ErnestAi.Speech;
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
                    // Use a slightly larger Whisper model for improved recognition. Adjust if needed.
                    services.AddSingleton<ISpeechToTextService>(_ => new SpeechToTextService(
                        modelFileName: "ggml-base.en.bin",
                        modelUrl: "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.en.bin"));
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

            var transcriber = new Transcriber(
                host.Services.GetRequiredService<ISpeechToTextService>(),
                audioChannel.Reader);

            Console.WriteLine("Sandbox: Recording chunks and printing transcriptions. Press Ctrl+C to stop.");

            var recordTask = recorder.RunAsync(cts.Token);
            var transcribeTask = transcriber.RunAsync(cts.Token);

            await Task.WhenAll(recordTask, transcribeTask);
        }
    }
}
