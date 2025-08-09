using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErnestAi.Configuration;
using ErnestAi.Core.Interfaces;
using ErnestAi.Intelligence;
using ErnestAi.Speech;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ErnestAi.Host
{
    /// <summary>
    /// Background worker that composes the wake-word -> record -> STT -> LLM -> TTS flow
    /// and manages warmup and announcement playback. Observes host lifetime for graceful shutdown.
    /// </summary>
    public class WakeWordWorker : BackgroundService
    {
        private readonly IWakeWordDetector _wakeWordDetector;
        private readonly IAudioProcessor _audioProcessor;
        private readonly ISpeechToTextService _sttService;
        private readonly ILanguageModelService _llmService;
        private readonly ITextToSpeechService _ttsService;
        private readonly WarmupOrchestrator? _warmupOrchestrator;
        private readonly AnnouncementService? _announcement;
        private readonly AppConfig _config;
        private readonly ILogger<WakeWordWorker> _logger;

        public WakeWordWorker(
            IWakeWordDetector wakeWordDetector,
            IAudioProcessor audioProcessor,
            ISpeechToTextService sttService,
            ILanguageModelService llmService,
            ITextToSpeechService ttsService,
            WarmupOrchestrator? warmupOrchestrator,
            AnnouncementService? announcement,
            AppConfig config,
            ILogger<WakeWordWorker> logger)
        {
            _wakeWordDetector = wakeWordDetector;
            _audioProcessor = audioProcessor;
            _sttService = sttService;
            _llmService = llmService;
            _ttsService = ttsService;
            _warmupOrchestrator = warmupOrchestrator;
            _announcement = announcement;
            _config = config;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("WakeWordWorker starting...");

            // Prepare TTS and announcements
            await InitializeAndSealTtsAsync();
            await InitializeAnnouncementsAsync();

            // Ensure wake word from config
            _wakeWordDetector.WakeWord = _config.WakeWord.WakeWord.ToLower();

            // Informational model listing
            await ListAvailableModelsAsync();

            // Start warmup (if enabled)
            if (_warmupOrchestrator != null)
            {
                await _warmupOrchestrator.StartAsync(_config, stoppingToken);
            }

            // Wire wake-word handler
            _wakeWordDetector.WakeWordDetected += async (sender, e) =>
            {
                _logger.LogInformation("Wake word detected: {text}", e.DetectedText);
                try
                {
                    // Acknowledgement
                    if (_announcement != null && _config.Acknowledgement?.Enabled == true && !string.IsNullOrWhiteSpace(_config.Acknowledgement.Phrase))
                    {
                        await _announcement.PlayAsync(_config.Acknowledgement.Phrase);
                        var pauseMs = _config.Acknowledgement?.PauseAfterMs ?? 0;
                        if (pauseMs > 0)
                        {
                            await Task.Delay(pauseMs, stoppingToken);
                        }
                    }

                    // Record fixed window
                    _logger.LogInformation("Recording...");
                    await _audioProcessor.StartRecordingAsync();
                    await Task.Delay(5000, stoppingToken);

                    _logger.LogInformation("Processing...");
                    var audioStream = await _audioProcessor.StopRecordingAsync();

                    // STT
                    var transcription = await _sttService.TranscribeAsync(audioStream);
                    _logger.LogInformation("You said: {text}", transcription);
                    File.AppendAllText("transcription_log.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ?: {transcription}\n");

                    // LLM
                    _logger.LogInformation("Thinking...");
                    var response = await _llmService.GetResponseAsync(transcription);
                    _logger.LogInformation("AI: {text}", response);

                    // TTS
                    await _ttsService.SpeakToDefaultOutputAsync(response);
                }
                catch (Exception ex)
                {
                    // Non-fatal path: surface to console/log; continue running
                    _logger.LogError(ex, "Error processing request");
                    Console.WriteLine($"Error processing request: {ex.Message}");
                }
            };

            // Start listening and await cancellation
            Console.WriteLine($"Listening for wake word: \"{_wakeWordDetector.WakeWord}\"...");
            await _wakeWordDetector.StartListeningAsync(stoppingToken);

            // Block until cancellation requested
            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // expected
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("WakeWordWorker stopping...");
            try
            {
                await _wakeWordDetector.StopListeningAsync();
                if (_warmupOrchestrator != null)
                {
                    await _warmupOrchestrator.StopAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during shutdown");
            }
        }

        private async Task InitializeAndSealTtsAsync()
        {
            try
            {
                var voices = (await _ttsService.GetAvailableVoicesAsync()).ToList();
                Console.WriteLine("[TTS] Available voices:");
                foreach (var v in voices)
                {
                    Console.WriteLine($"  - {v}");
                }

                string? selectedVoice = null;
                var preferred = _config.TextToSpeech?.PreferredVoice;
                if (!string.IsNullOrWhiteSpace(preferred))
                {
                    selectedVoice = voices.FirstOrDefault(v => string.Equals(v, preferred, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(selectedVoice))
                    {
                        Console.WriteLine($"[TTS] Selected voice: '{selectedVoice}'");
                    }
                    else
                    {
                        Console.WriteLine($"[TTS] PreferredVoice '{preferred}' not found. Using current voice '{_ttsService.CurrentVoice ?? "<none>"}'.");
                    }
                }
                else
                {
                    Console.WriteLine($"[TTS] No PreferredVoice configured. Using current voice '{_ttsService.CurrentVoice ?? "<none>"}'.");
                }

                if (_ttsService is TextToSpeechService ttsConcrete)
                {
                    var voiceToUse = selectedVoice ?? _ttsService.CurrentVoice;
                    ttsConcrete.InitializeOnce(voiceToUse, _ttsService.SpeechRate, _ttsService.SpeechPitch);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TTS] Failed to enumerate/select voices");
            }
        }

        private async Task InitializeAnnouncementsAsync()
        {
            if (_announcement == null) return;
            try
            {
                var voice = _ttsService.CurrentVoice ?? string.Empty;
                var rate = _ttsService.SpeechRate;
                var pitch = _ttsService.SpeechPitch;
                var cacheDir = _config.Acknowledgement?.CacheDirectory;
                await _announcement.InitializeAsync(voice, rate, pitch, cacheDir);

                if (_config.Acknowledgement?.Enabled == true && !string.IsNullOrWhiteSpace(_config.Acknowledgement.Phrase))
                {
                    await _announcement.PreloadAsync(_config.Acknowledgement.Phrase);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Ann] Initialization failed");
            }
        }

        private async Task ListAvailableModelsAsync()
        {
            try
            {
                var models = await _llmService.GetAvailableModelsAsync();
                Console.WriteLine("[LLM] Available models:");
                foreach (var m in models)
                {
                    Console.WriteLine($" - {m}");
                }
                Console.WriteLine($"[LLM] Selected model: {_llmService.CurrentModel} (provider={_llmService.ProviderName})");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LLM] Failed to retrieve available models");
            }
        }
    }
}
