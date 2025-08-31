using System.Text.Json;
using Elara.Speech;
using Elara.Core.Paths;

namespace Elara.UnitTests
{
    public sealed class SampleRunTests
    {
        private sealed class ExpectedSchema
        {
            public Tolerances tolerances { get; set; } = new();
            public Settings settings { get; set; } = new();
            public List<TranscriptItem> transcripts { get; set; } = new();
        }
        private sealed class Tolerances { public double cer { get; set; } = 0.25; public double wer { get; set; } = 0.4; }
        private sealed class Settings
        {
            public string wakeWord { get; set; } = string.Empty;
            public SegmenterCfg segmenter { get; set; } = new();
            public SttCfg stt { get; set; } = new();
        }
        private sealed class SegmenterCfg { public int SampleRate { get; set; } public int Channels { get; set; } }
        private sealed class SttCfg { public string modelFile { get; set; } = string.Empty; public string modelUrl { get; set; } = string.Empty; }
        private sealed class TranscriptItem { public long Sequence { get; set; } public DateTimeOffset TimestampUtc { get; set; } public string Text { get; set; } = string.Empty; public bool IsMeaningful { get; set; } public int WordCount { get; set; } }

        public static IEnumerable<object[]> DiscoverRuns()
        {
            var sampleRunsDir = GetSampleRunsPaths();
            if (!Directory.Exists(sampleRunsDir)) yield break;
            foreach (var scenario in Directory.EnumerateDirectories(sampleRunsDir))
            {
                var wav = Path.Combine(scenario, TestConstants.Paths.AudioFileName);
                var json = Path.Combine(scenario, TestConstants.Paths.ExpectedJsonFileName);
                if (!File.Exists(wav) || !File.Exists(json))
                    continue;

                // Only yield scenarios whose required model file already exists in the shared cache
                bool runnable = false;
                try
                {
                    var expectedJson = File.ReadAllText(json);
                    var expected = JsonSerializer.Deserialize<ExpectedSchema>(expectedJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    var modelFile = expected?.settings?.stt?.modelFile ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(modelFile))
                    {
                        var modelsDir = ModelPaths.GetWhisperModelsDir();
                        var modelPath = Path.Combine(modelsDir, modelFile);
                        runnable = File.Exists(modelPath);
                    }
                }
                catch
                {
                    // Ignore malformed scenarios
                }

                if (runnable)
                {
                    yield return new object[] { scenario, wav, json };
                }
            }
        }

        private static string GetSampleRunsPaths()
        {
            var testsDir = new DirectoryInfo(AppContext.BaseDirectory);
            return Path.Combine(testsDir.FullName, TestConstants.Paths.SampleRunsFolder);
        }

        [Fact]
        public void EnsureSampleRunsDiscovered()
        {
            var any = DiscoverRuns().Any();
            Assert.True(any,
                "No runnable SampleRuns found. Run Elara.Host once to populate the Whisper model cache, then re-run tests. " +
                "Tests reuse the existing model directory and do not download models.");
        }

        [Theory]
        [MemberData(nameof(DiscoverRuns))]
        public async Task Transcription_should_match_expected_within_tolerances(string runDir, string wavPath, string expectedJsonPath)
        {
            // Load expected
            var json = await File.ReadAllTextAsync(expectedJsonPath);
            var expected = JsonSerializer.Deserialize<ExpectedSchema>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })!;

            // Model path in shared model cache directory (populated by Host)
            var outModelsDir = ModelPaths.GetWhisperModelsDir();
            var modelFile = expected.settings.stt.modelFile ?? string.Empty;
            var dstModelPath = Path.Combine(outModelsDir, modelFile);
            // Should exist because discovery filtered scenarios by availability
            Assert.True(File.Exists(dstModelPath), $"Required model '{modelFile}' not found in '{outModelsDir}'. Run Elara.Host once to populate the model cache.");

            // Compose expected text as concatenation of meaningful items
            var expectedText = string.Join(" ", expected.transcripts
                .Where(t => t.IsMeaningful && !string.IsNullOrWhiteSpace(t.Text))
                .OrderBy(t => t.Sequence)
                .Select(t => t.Text.Trim()));

            // Run STT
            var stt = new SpeechToTextService(dstModelPath);
            var actualText = await stt.TranscribeFileAsync(wavPath);

            // Compute metrics
            var cer = TextDistance.CharacterErrorRate(expectedText, actualText);
            var wer = TextDistance.WordErrorRate(expectedText, actualText);

            // Assertions with tolerances from expected.json
            Assert.True(cer <= expected.tolerances.cer, $"CER {cer:F3} exceeded tolerance {expected.tolerances.cer:F3} for run {runDir}\nExpected: {expectedText}\nActual:   {actualText}");
            Assert.True(wer <= expected.tolerances.wer, $"WER {wer:F3} exceeded tolerance {expected.tolerances.wer:F3} for run {runDir}\nExpected: {expectedText}\nActual:   {actualText}");
        }
    }
}
