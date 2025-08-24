using System.Threading.Channels;
using Elara.Host.Audio;
using Elara.Host.Speech;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using Elara.Host.Intelligence;
using LoggingLevel = Elara.Host.Logging.LogLevel;
using Elara.Host.Configuration;
using Elara.Host.Core.Interfaces;
using Elara.Host.Tools;
using Elara.Host.Logging;
using Elara.Host.Pipeline;
using Elara.Host.Utilities;

namespace Elara.Host
{
    /// <summary>
    /// Sandbox host wiring audio capture, transcription, conversation FSM, LLM, and TTS.
    /// The FSM governs transitions: Quiescent -> Listening -> Processing -> (Speaking) -> Listening.
    /// </summary>
    internal class Program
    {
        // Legacy constants (kept for reference; values now sourced from config)
        private const int ChunkMs = 3000;         // unused; segmentation uses config.Segmenter

        /// <summary>
        /// Entry point that composes the pipeline and runs until canceled.
        /// </summary>
        private static async Task Main(string[] args)
        {
            using var cts = new CancellationTokenSource();

            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            // Load configuration early so logging can be configured from settings
            var config = await ConfigLoader.LoadAsync();

            // Configure logging (file + console subscriber) based on config
            static string ResolveLogPath(string baseDir, string dirSetting)
            {
                return Path.IsPathRooted(dirSetting) ? dirSetting : Path.Combine(baseDir, dirSetting);
            }

            static string RenderFileNamePattern(string pattern)
            {
                // Supports {date:format} token
                const string token = "{date:";
                int idx = pattern.IndexOf(token, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    int end = pattern.IndexOf('}', idx + token.Length);
                    if (end > idx)
                    {
                        var fmt = pattern.Substring(idx + token.Length, end - (idx + token.Length));
                        var rendered = DateTimeOffset.Now.ToString(fmt);
                        return pattern.Substring(0, idx) + rendered + pattern.Substring(end + 1);
                    }
                }
                return pattern;
            }

            var resolvedLogDir = ResolveLogPath(AppContext.BaseDirectory, config.ElaraLogging.Directory);
            Directory.CreateDirectory(resolvedLogDir);
            var logFile = Path.Combine(resolvedLogDir, RenderFileNamePattern(config.ElaraLogging.FileNamePattern));

            // Map string level to enum
            LoggingLevel minLevel = config.ElaraLogging.Level?.ToLowerInvariant() switch
            {
                "debug" => LoggingLevel.Debug,
                "info" => LoggingLevel.Info,
                "warn" => LoggingLevel.Warn,
                "error" => LoggingLevel.Error,
                _ => LoggingLevel.Debug
            };

            Logger.Configure(logFile, minLevel);
            Logger.OnLog += (evt) =>
            {
                ConsoleColorizer.WithColorFor(evt.Source, evt.Level, () => Console.WriteLine(evt.ToString()));
            };

            // First message: how to terminate without closing the window
            Logger.Info("Program", "Press 'Q' to quit or use Ctrl+C to stop.");

            // Bind announcements from config
            var announcer = Announcements.FromOptions(config.Announcements);

            // Parse command-line: --record[=scenario]
            bool recordingEnabled = false;
            string? recordScenario = null;
            foreach (var a in args ?? Array.Empty<string>())
            {
                if (a.StartsWith("--record", StringComparison.OrdinalIgnoreCase))
                {
                    recordingEnabled = true;
                    var eq = a.IndexOf('=');
                    if (eq > 0 && eq < a.Length - 1)
                        recordScenario = a[(eq + 1)..];
                }
            }

            // Ensure STT model is present BEFORE DI wiring and processing (use cross-platform cache dir)
            var modelsDirPre = await AppPaths.GetModelCacheDirAsync();
            var modelPathPre = Path.Combine(modelsDirPre, config.Stt.ModelFile);
            if (!File.Exists(modelPathPre))
            {
                Logger.Info("STT", $"Downloading Whisper model '{config.Stt.ModelFile}'... URL: {config.Stt.ModelUrl} Path: {modelPathPre}");
                await FileDownloader.DownloadToFileAsync(modelPathPre, config.Stt.ModelUrl);
                Logger.Info("STT", $"Model ready: {modelPathPre}");
            }

            var host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddSimpleConsole(o => o.TimestampFormat = config.ElaraLogging.ConsoleTimestampFormat);
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
                    // Text-to-speech: use Windows System.Speech where available, otherwise NoOp
                    services.AddSingleton<ITextToSpeechService>(_ =>
                    {
                        if (OperatingSystem.IsWindows())
                        {
                            var ttsSvc = new TextToSpeechService();
                            ttsSvc.InitializeOnce(
                                config.TextToSpeech.Voice,
                                config.TextToSpeech.Rate,
                                config.TextToSpeech.Pitch,
                                config.TextToSpeech.PreambleMs);
                            return ttsSvc;
                        }
                        else
                        {
                            var ttsSvc = new NoOpTextToSpeechService();
                            ttsSvc.InitializeOnce(
                                config.TextToSpeech.Voice,
                                config.TextToSpeech.Rate,
                                config.TextToSpeech.Pitch,
                                config.TextToSpeech.PreambleMs);
                            return ttsSvc;
                        }
                    });
                })
                .Build();

            Logger.Debug("Program", "Host built and services configured.");

            // Channel for audio chunks from Streamer -> Transcriber
            var audioChannel = Channel.CreateBounded<AudioChunk>(new BoundedChannelOptions(config.Host.AudioQueueCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = true
            });

            // Transcription pipeline channel (producer: Transcriber, consumer: FSM consumer loop)
            var transcriptionChannel = Channel.CreateBounded<TranscriptionItem>(new BoundedChannelOptions(config.Host.TranscriptionQueueCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = true
            });

            // Streamer records and segments audio according to config.Segmenter and writes to audioChannel
            var streamer = new Streamer(
                host.Services.GetRequiredService<IAudioProcessor>(),
                audioChannel.Writer,
                config.Segmenter,
                new ComponentLogger("Streamer"));

            // FSM manages conversation transitions; Transcriber feeds it via transcriptionChannel
            var csm = new ConversationStateMachine(
                config.Host.WakeWord,
                TimeSpan.FromSeconds(config.Host.ProcessingSilenceSeconds),
                TimeSpan.FromSeconds(config.Host.EndSilenceSeconds),
                new ComponentLogger("Conversation"));

            // Transcriber consumes audio chunks -> STT -> TranscriptionItem -> transcriptionChannel
            var transcriber = new Transcriber(
                host.Services.GetRequiredService<ISpeechToTextService>(),
                audioChannel.Reader,
                transcriptionChannel.Writer,
                new ComponentLogger("Transcriber"));

            // Wire LLM + optional TTS on prompt ready
            var llm = host.Services.GetRequiredService<ILanguageModelService>();
            var tts = host.Services.GetRequiredService<ITextToSpeechService>();
            var ttsEnabled = config.TextToSpeech.Enabled;
            var announcerPlayer = new AnnouncementPlayer(tts);

            // Suppression window to ignore transcriptions captured while the AI is Processing/Speaking
            // This prevents feedback where speaker output is picked up by the mic and later processed.
            var suppressTail = TimeSpan.FromMilliseconds(300); // small tail to cover playback trailing audio
            int suppressFlag = 0; // 0 = off, 1 = on
            DateTimeOffset? suppressStart = null;
            DateTimeOffset? suppressEnd = null;

            // Announcements listener for state transitions
            csm.StateChanged += async (from, to, reason, at) =>
            {
                try
                {
                    if (!ttsEnabled) return;
                    if (from == ConversationMode.Quiescent && to == ConversationMode.Listening)
                    {
                        await announcerPlayer.PlayAsync("Wake", announcer.AcknowledgeWakeWord);
                    }
                    else if (from == ConversationMode.Listening && to == ConversationMode.Processing)
                    {
                        await announcerPlayer.PlayAsync("Prompt", announcer.AcknowledgePrompt);
                    }
                    else if (from == ConversationMode.Listening && to == ConversationMode.Quiescent)
                    {
                        await announcerPlayer.PlayAsync("Quiescence", announcer.AcknowledgeQuiescence);
                    }
                }
                catch { /* best-effort announcement */ }
            };

            // Maintain suppression flags/interval on any state change
            csm.StateChanged += (from, to, reason, at) =>
            {
                if (to == ConversationMode.Processing || to == ConversationMode.Speaking)
                {
                    System.Threading.Interlocked.Exchange(ref suppressFlag, 1);
                    suppressStart = at;
                    suppressEnd = null;
                }
                else if (from == ConversationMode.Processing || from == ConversationMode.Speaking)
                {
                    System.Threading.Interlocked.Exchange(ref suppressFlag, 0);
                    suppressEnd = at;
                }
            };
            csm.PromptReady += (prompt) =>
            {
                Task.Run(async () =>
                {
                    try
                    {
                        Logger.Info("AI", "Calling language model...");
                        var reply = await llm.GetResponseAsync(prompt, cts.Token); // Processing -> model call
                        Logger.Info("AI", $"Response: {reply}");
                        if (ttsEnabled)
                        {
                            // Transition: Processing -> Speaking for audio output
                            csm.BeginSpeaking();
                            try
                            {
                                await tts.SpeakToDefaultOutputAsync(reply);
                            }
                            finally
                            {
                                csm.EndSpeaking(); // Speaking -> Listening
                            }
                        }
                        else
                        {
                            // No audio output: end processing explicitly (Processing -> Listening)
                            csm.EndProcessing();
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("AI", $"Error handling prompt: {ex.Message}");
                        // Ensure we leave current active phase on error
                        if (csm.IsSpeaking) csm.EndSpeaking();
                        else csm.EndProcessing();
                    }
                }, cts.Token);
            };

            Console.WriteLine("Sandbox: Recording chunks and printing transcriptions. Press 'Q' to quit or Ctrl+C to stop.");
            Console.WriteLine($"Wake word: '{config.Host.WakeWord}', processing after {config.Host.ProcessingSilenceSeconds}s silence, end after {config.Host.EndSilenceSeconds}s silence.");
            Logger.Info("Program", $"Configured wake word='{config.Host.WakeWord}', processingSilence={config.Host.ProcessingSilenceSeconds}s, endSilence={config.Host.EndSilenceSeconds}s.");

            // Startup deterministic announcement to pre-warm TTS and announce key settings
            if (ttsEnabled)
            {
                var startup = announcer.RenderStartup(
                    config.Host.WakeWord,
                    (llm as OllamaLanguageModelService)?.CurrentModel ?? "<model>",
                    config.LanguageModel.BaseUrl,
                    tts.CurrentVoice);
                await announcerPlayer.PlayAsync("Startup", startup);
            }

            // Optional full-session recording (from app start until quit), controlled by --record
            SessionRecording? session = null;
            if (recordingEnabled)
            {
                var scenario = string.IsNullOrWhiteSpace(recordScenario) ? config.Host.SessionRecording.DefaultScenario : recordScenario!;
                var fmt = new WaveFormat(config.Segmenter.SampleRate, config.Segmenter.Channels);
                session = SessionRecording.Start(config.Host.SessionRecording.BaseDirectory, scenario, fmt, streamer, config.Host.SessionRecording.Tolerances);
                Logger.Info("Recorder", $"Writing full session to: {session.AudioWavPath}");
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
                        // Drop any items captured during the last Processing/Speaking window (with a small tail)
                        bool inSuppressedInterval = false;
                        var ts = item.TimestampUtc;
                        if (Volatile.Read(ref suppressFlag) != 1 || !suppressStart.HasValue)
                        {
                            if (suppressStart.HasValue && suppressEnd.HasValue)
                            {
                                inSuppressedInterval = ts >= suppressStart.Value && ts <= (suppressEnd.Value + suppressTail);
                            }
                        }
                        else
                        {
                            inSuppressedInterval = ts >= suppressStart.Value;
                        }

                        if (!inSuppressedInterval)
                        {
                            csm.HandleTranscription(item);
                            session?.Add(item);
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
                        csm.Tick(DateTimeOffset.UtcNow); // Drives silence-based transitions during idle periods
                        await Task.Delay(config.Host.TickerIntervalMs, cts.Token);
                    }
                }
                catch (OperationCanceledException) { }
            }, cts.Token);

            var recordTask = streamer.RunAsync(cts.Token);
            var transcribeTask = transcriber.RunAsync(cts.Token);

            Logger.Debug("Program", "Tasks started: recorder, transcriber, FSM consumer, ticker, key listener.");
            await Task.WhenAll(recordTask, transcribeTask, fsmTask, tickerTask, keyTask);

            // If session recording was enabled, write expected.json from collected transcriptions and settings
            if (session != null)
            {
                await session.WriteExpectedAsync(config);
                session.Dispose();
            }

            // Keep window open after shutdown
            Logger.Info("Program", "Stopped. Press any key to close...");
            try { Console.ReadKey(true); } catch { }
        }
    }
}
