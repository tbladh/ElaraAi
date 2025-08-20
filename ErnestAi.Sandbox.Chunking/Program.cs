using System.Threading.Channels;
using System.IO;
using System.Text.Json;
using ErnestAi.Sandbox.Chunking.Core.Interfaces;
using ErnestAi.Sandbox.Chunking.Audio;
using ErnestAi.Sandbox.Chunking.Speech;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ErnestAi.Sandbox.Chunking.Tools;
using NAudio.Wave;
using ErnestAi.Sandbox.Chunking.Logging;
using ErnestAi.Sandbox.Chunking.Configuration;
using ErnestAi.Sandbox.Chunking.Intelligence;
using LoggingLevel = ErnestAi.Sandbox.Chunking.Logging.LogLevel;

namespace ErnestAi.Sandbox.Chunking
{
    internal class Program
    {
        // Tunables for this sandbox
        private const int ChunkMs = 3000;         // 3 seconds per chunk (better context)
        private const int AudioQueueCapacity = 16;
        private const string WakeWord = "anna"; // simple wake word for sandbox
        private const int ProcessingSilenceSeconds = 8;  // after 5s of silence, enter processing
        private const int EndSilenceSeconds = 60;        // after 60s of silence, return to quiescent

        private static async Task Main(string[] args)
        {
            using var cts = new CancellationTokenSource();

            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            // Configure logging (file + console subscriber)
            var logDir = Path.Combine(AppContext.BaseDirectory, "Logs");
            Directory.CreateDirectory(logDir);
            var logFile = Path.Combine(logDir, $"sandbox-{DateTimeOffset.Now:yyyyMMdd}.log");
            Logger.Configure(logFile, LoggingLevel.Debug); // allow debug by default
            Logger.OnLog += (evt) =>
            {
                ConsoleColorizer.WithColorFor(evt.Source, evt.Level, () => Console.WriteLine(evt.ToString()));
            };

            // First message: how to terminate without closing the window
            Logger.Info("Program", "Press 'Q' to quit or use Ctrl+C to stop.");

            // Load sandbox configuration
            Logger.Info("Program", "Loading configuration (appsettings.sandbox.json or defaults)...");
            var config = await ConfigLoader.LoadAsync();

            // Ensure STT model is present BEFORE DI wiring and processing (use cross-platform cache dir)
            var modelsDirPre = await AppPaths.GetModelCacheDirAsync();
            var modelPathPre = Path.Combine(modelsDirPre, config.Stt.ModelFile);
            if (!File.Exists(modelPathPre))
            {
                Logger.Info("STT", $"Downloading Whisper model '{config.Stt.ModelFile}'... URL: {config.Stt.ModelUrl} Path: {modelPathPre}");
                await FileDownloader.DownloadToFileAsync(modelPathPre, config.Stt.ModelUrl);
                Logger.Info("STT", $"Model ready: {modelPathPre}");
            }

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
                    // Construct STT with provided local model path (no downloading in service)
                    services.AddSingleton<ISpeechToTextService>(_ => new SpeechToTextService(modelPathPre));
                    // Language model (Ollama)
                    services.AddSingleton<ILanguageModelService>(_ =>
                    {
                        var svc = new OllamaLanguageModelService(config.LanguageModel.BaseUrl);
                        svc.CurrentModel = config.LanguageModel.ModelName;
                        svc.SystemPrompt = config.LanguageModel.SystemPrompt;
                        svc.OutputFilters = new List<string>(config.LanguageModel.OutputFilters ?? Array.Empty<string>());
                        return svc;
                    });
                    // Text-to-speech (System.Speech)
                    services.AddSingleton<ITextToSpeechService>(_ =>
                    {
                        var ttsSvc = new TextToSpeechService();
                        ttsSvc.InitializeOnce(config.TextToSpeech.Voice, config.TextToSpeech.Rate, config.TextToSpeech.Pitch);
                        return ttsSvc;
                    });
                })
                .Build();

            Logger.Debug("Program", "Host built and services configured.");

            var audioChannel = Channel.CreateBounded<AudioChunk>(new BoundedChannelOptions(AudioQueueCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = true
            });

            // Transcription pipeline channel (producer: Transcriber, consumer: future aggregator)
            var transcriptionChannel = Channel.CreateBounded<TranscriptionItem>(new BoundedChannelOptions(64)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = true
            });

            var streamer = new Streamer(
                host.Services.GetRequiredService<IAudioProcessor>(),
                audioChannel.Writer,
                config.Segmenter,
                new ComponentLogger("Streamer"));

            var csm = new ConversationStateMachine(
                WakeWord,
                TimeSpan.FromSeconds(ProcessingSilenceSeconds),
                TimeSpan.FromSeconds(EndSilenceSeconds),
                new ComponentLogger("Conversation"));

            var transcriber = new Transcriber(
                host.Services.GetRequiredService<ISpeechToTextService>(),
                audioChannel.Reader,
                transcriptionChannel.Writer,
                new ComponentLogger("Transcriber"));

            // Wire LLM + optional TTS on prompt ready
            var llm = host.Services.GetRequiredService<ILanguageModelService>();
            var tts = host.Services.GetRequiredService<ITextToSpeechService>();
            var ttsEnabled = config.TextToSpeech.Enabled;
            csm.PromptReady += (prompt) =>
            {
                Task.Run(async () =>
                {
                    try
                    {
                        Logger.Info("AI", "Calling language model...");
                        var reply = await llm.GetResponseAsync(prompt, cts.Token);
                        Logger.Info("AI", $"Response: {reply}");
                        if (ttsEnabled)
                        {
                            await tts.SpeakToDefaultOutputAsync(reply);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("AI", $"Error handling prompt: {ex.Message}");
                    }
                }, cts.Token);
            };

            Console.WriteLine("Sandbox: Recording chunks and printing transcriptions. Press 'Q' to quit or Ctrl+C to stop.");
            Console.WriteLine($"Wake word: '{WakeWord}', processing after {ProcessingSilenceSeconds}s silence, end after {EndSilenceSeconds}s silence.");
            Logger.Info("Program", $"Configured wake word='{WakeWord}', processingSilence={ProcessingSilenceSeconds}s, endSilence={EndSilenceSeconds}s.");

            // Optional full-session recording (from app start until quit)
            Console.Write("Record this session to a WAV file and collect transcriptions? (yes/no) ");
            var recAns = (Console.ReadLine() ?? string.Empty).Trim().ToLowerInvariant();
            List<TranscriptionItem>? recordedItems = null;
            string? sessionJsonPath = null;
            if (recAns == "y" || recAns == "yes")
            {
                Console.Write("Scenario folder (e.g., short-wake-word): ");
                var scenario = (Console.ReadLine() ?? "session").Trim();
                if (string.IsNullOrWhiteSpace(scenario)) scenario = "session";

                var stamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
                var baseDir = Path.Combine(AppContext.BaseDirectory, "SampleRuns", scenario, stamp);
                Directory.CreateDirectory(baseDir);

                var wavPath = Path.Combine(baseDir, "audio.wav");
                sessionJsonPath = Path.Combine(baseDir, "expected.json");

                // Create session writer with sandbox segmenter format
                var fmt = new WaveFormat(config.Segmenter.SampleRate, config.Segmenter.Channels);
                var sessionWriter = new WaveFileWriter(wavPath, fmt);
                streamer.SetSessionWriter(sessionWriter);

                recordedItems = new List<TranscriptionItem>(capacity: 256);
                Logger.Info("Recorder", $"Writing full session to: {wavPath}");
            }

            // Key listener task for graceful termination via single keypress
            var keyTask = Task.Run(() =>
            {
                try
                {
                    while (!cts.IsCancellationRequested)
                    {
                        if (Console.KeyAvailable)
                        {
                            var key = Console.ReadKey(true);
                            if (key.Key == ConsoleKey.Q || key.Key == ConsoleKey.Escape)
                            {
                                cts.Cancel();
                                break;
                            }
                        }
                        Thread.Sleep(50);
                    }
                }
                catch { /* ignore */ }
            }, cts.Token);

            // CSM consumer loop for transcription items
            var fsmTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (var item in transcriptionChannel.Reader.ReadAllAsync(cts.Token))
                    {
                        csm.HandleTranscription(item);
                        if (recordedItems != null)
                        {
                            recordedItems.Add(item);
                        }
                    }
                }
                catch (OperationCanceledException) { }
            }, cts.Token);

            // Lightweight ticker to advance silence timers even when no new items arrive
            var tickerTask = Task.Run(async () =>
            {
                try
                {
                    while (!cts.IsCancellationRequested)
                    {
                        csm.Tick(DateTimeOffset.UtcNow);
                        await Task.Delay(200, cts.Token);
                    }
                }
                catch (OperationCanceledException) { }
            }, cts.Token);

            var recordTask = streamer.RunAsync(cts.Token);
            var transcribeTask = transcriber.RunAsync(cts.Token);

            Logger.Debug("Program", "Tasks started: recorder, transcriber, FSM consumer, ticker, key listener.");
            await Task.WhenAll(recordTask, transcribeTask, fsmTask, tickerTask, keyTask);

            // If session recording was enabled, write expected.json from collected transcriptions and settings
            if (recordedItems != null && sessionJsonPath != null)
            {
                var expectedObj = new
                {
                    settings = new
                    {
                        wakeWord = WakeWord,
                        segmenter = config.Segmenter,
                        stt = new { modelFile = config.Stt.ModelFile }
                    },
                    transcripts = recordedItems,
                    tolerances = new { cer = 0.25, wer = 0.4 }
                };
                var json = JsonSerializer.Serialize(expectedObj, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(sessionJsonPath, json);
                Logger.Info("Recorder", $"Wrote expected.json with {recordedItems.Count} items");
            }

            // Keep window open after shutdown
            Logger.Info("Program", "Stopped. Press any key to close...");
            try { Console.ReadKey(true); } catch { }
        }
    }
}
